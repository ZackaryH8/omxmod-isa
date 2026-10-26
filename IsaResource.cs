using System.Diagnostics;
using System.Text.Json;
using OMX.ModAPI;
using OMX.ModAPI.Client;

namespace OMX.Mods.IntelligentSpeedAssistance;

public sealed class IsaResource : ClientResource
{
    private IsaConfiguration _configuration = new();
    private SpeedAssistanceController _controller = new(new IsaConfiguration());
    private EntityId _vehicle;
    private bool _enabled;
    private string _unit = "mph";
    private RoadLimitMemory _roadMemory = new(5d);
    private bool _stopping;
    private readonly long _started = Stopwatch.GetTimestamp();
    private Task _save = Task.CompletedTask;
    private float _speed;
    private float? _limit;
    private bool _hasVehicle;

    protected override async ValueTask OnStartAsync(CancellationToken cancellationToken)
    {
        const ResourceCapabilities required = ResourceCapabilities.RoadPosition | ResourceCapabilities.VehicleInputLimits |
            ResourceCapabilities.HudVisuals;
        if ((Runtime.Capabilities & required) != required)
            throw new NotSupportedException("ISA requires road position, input limits and HUD visuals (OMX API 2.1).");
        _configuration = Storage.Configuration.ValueKind == JsonValueKind.Object
            ? Storage.ConfigurationAs<IsaConfiguration>() ?? new() : new();
        _configuration.Validate();
        _roadMemory = new RoadLimitMemory(_configuration.RoadDataGraceSeconds);
        _enabled = _configuration.Enabled;
        _unit = _configuration.SpeedUnit;
        try
        {
            if (await Storage.ReadJsonAsync<Preferences>("preferences.json", cancellationToken) is { } saved)
            {
                _enabled = saved.Enabled;
                if (saved.SpeedUnit is "mph" or "km/h") _unit = saved.SpeedUnit;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        { Log.Warn("Could not read ISA preferences: " + exception.Message); }
        _controller = new SpeedAssistanceController(_configuration with { Enabled = _enabled });
        Hud.ActionInvoked.Subscribe(OnAction);
        Log.Info("ISA started; road speed rules and accelerator limiting, with live controls.");
    }

    protected override void OnTick(TimeSpan delta)
    {
        if (_stopping) return;
        ILocalVehicle? bus = Vehicles.Local;
        _hasVehicle = bus is { IsValid: true, IsSpawned: true };
        if (bus?.Id != _vehicle)
        {
            _vehicle = bus?.Id ?? EntityId.None;
            _controller.Reset();
            _roadMemory.Reset();
        }
        _speed = _hasVehicle ? bus!.Velocity.Length() * 3.6f : 0f;
        if (_hasVehicle) _limit = _roadMemory.Update(bus!.Road?.SpeedLimitKmh, delta.TotalSeconds);
        else { _roadMemory.Reset(); _limit = null; }
        Update(delta.TotalSeconds);
    }

    private void Update(double seconds)
    {
        Input.ThrottleLimit = _controller.Update(_speed, _limit, seconds);
        if (!_configuration.ShowHud) return;
        bool fault = !float.IsFinite(_speed) || _limit is { } value && (!float.IsFinite(value) || value <= 0f);
        IsaStatus status = !_enabled ? IsaStatus.Disabled : fault ? IsaStatus.Fault
            : !_hasVehicle || _limit is null ? IsaStatus.Unavailable
            : _speed > _limit + 0.1f ? IsaStatus.Overspeed
            : Input.ThrottleLimit < 0.999f ? IsaStatus.Restricted : IsaStatus.Active;
        Hud.SetPanel("isa", IsaDisplay.Create(status, _limit, _unit,
            Stopwatch.GetElapsedTime(_started).TotalSeconds), new HudPanelOptions
        {
            Anchor = HudAnchor.BottomRight, Offset = new System.Numerics.Vector2(12f),
            Width = 148f, ActionsInline = true, Minimal = true,
        });
    }

    private void OnAction(HudAction action)
    {
        if (_stopping || action.PanelId != "isa") return;
        switch (action.ActionId)
        {
            case "toggle":
                _enabled = !_enabled;
                _controller = new SpeedAssistanceController(_configuration with { Enabled = _enabled });
                break;
            case "units": _unit = _unit == "mph" ? "km/h" : "mph"; break;
            default: return;
        }
        Update(0d);
        _save = SaveAsync(_save, new Preferences(_enabled, _unit));
    }

    private async Task SaveAsync(Task preceding, Preferences preferences)
    {
        await preceding;
        try { await Storage.WriteJsonAsync("preferences.json", preferences); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Log.Warn("Could not save ISA preferences: " + exception.Message); }
    }

    protected override void OnSessionChanged(SessionKind session) => Reset();
    protected override void OnMapUnloaded(string mapName) => Reset();
    protected override async ValueTask OnStopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        Reset();
        Hud.RemovePanel("isa");
        await _save;
    }

    private void Reset()
    {
        Input.ThrottleLimit = 1f;
        _controller.Reset();
        _roadMemory.Reset();
        _vehicle = EntityId.None;
        _limit = null;
        _hasVehicle = false;
    }

    private sealed record Preferences(bool Enabled, string SpeedUnit);
}
