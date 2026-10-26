namespace OMX.Mods.IntelligentSpeedAssistance;

public sealed record IsaConfiguration
{
    public bool Enabled { get; init; } = true;
    public string SpeedUnit { get; init; } = "mph";
    public float ApproachBandKmh { get; init; } = 1.5f;
    public float ThrottleRecoveryPerSecond { get; init; } = 2f;
    public bool ShowHud { get; init; } = true;
    public float RoadDataGraceSeconds { get; init; } = 5f;

    public void Validate()
    {
        if (SpeedUnit is not ("mph" or "km/h"))
        {
            throw new ArgumentException("speedUnit must be 'mph' or 'km/h'.");
        }

        if (!float.IsFinite(RoadDataGraceSeconds) || RoadDataGraceSeconds is < 0f or > 60f)
            throw new ArgumentOutOfRangeException(nameof(RoadDataGraceSeconds), "roadDataGraceSeconds must be 0..60.");

        if (!float.IsFinite(ApproachBandKmh) || ApproachBandKmh is <= 0f or > 20f)
        {
            throw new ArgumentOutOfRangeException(nameof(ApproachBandKmh), "approachBandKmh must be greater than zero and at most 20.");
        }

        if (!float.IsFinite(ThrottleRecoveryPerSecond) || ThrottleRecoveryPerSecond is <= 0f or > 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(ThrottleRecoveryPerSecond), "throttleRecoveryPerSecond must be greater than zero and at most 100.");
        }
    }
}

/// <summary>Retains the last known road limit through brief gaps in lane matching.</summary>
public sealed class RoadLimitMemory(double graceSeconds)
{
    private float? _lastLimit;
    private double _missingSeconds;

    public float? Update(float? limit, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0d) { Reset(); return null; }
        if (limit is { } value)
        {
            if (!float.IsFinite(value) || value <= 0f) { Reset(); return limit; }
            _lastLimit = limit;
            _missingSeconds = 0d;
            return limit;
        }
        _missingSeconds += seconds;
        if (_missingSeconds >= graceSeconds) _lastLimit = null;
        return _lastLimit;
    }

    public void Reset()
    {
        _lastLimit = null;
        _missingSeconds = 0d;
    }
}

/// <summary>ISA policy belongs to the resource; the SDK only supplies road data and an input cap.</summary>
public sealed class SpeedAssistanceController
{
    private readonly IsaConfiguration _configuration;
    public float ThrottleLimit { get; private set; } = 1f;

    public SpeedAssistanceController(IsaConfiguration configuration)
    {
        configuration.Validate();
        _configuration = configuration;
    }

    public float Update(float speedKmh, float? roadLimitKmh, double seconds)
    {
        if (!_configuration.Enabled || roadLimitKmh is not { } limit || !float.IsFinite(limit) || limit <= 0f ||
            !float.IsFinite(speedKmh) || speedKmh < 0f || !double.IsFinite(seconds) || seconds < 0d)
        {
            return ThrottleLimit = 1f;
        }

        float target = Math.Clamp((limit - speedKmh) / _configuration.ApproachBandKmh, 0f, 1f);
        // Falling caps act immediately; power returns progressively when the limit rises or speed falls.
        return ThrottleLimit = target <= ThrottleLimit ? target
            : Math.Min(target, ThrottleLimit + (float)Math.Min(seconds, 1d) * _configuration.ThrottleRecoveryPerSecond);
    }

    public void Reset() => ThrottleLimit = 1f;
}
