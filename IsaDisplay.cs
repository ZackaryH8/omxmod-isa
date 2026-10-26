using System.Numerics;
using OMX.ModAPI;

namespace OMX.Mods.IntelligentSpeedAssistance;

public enum IsaStatus { Disabled, Unavailable, Active, Restricted, Overspeed, Fault }

public static class IsaDisplay
{
    public static HudPanel Create(IsaStatus status, float? limitKmh, string unit, double elapsedSeconds)
    {
        bool off = status == IsaStatus.Disabled;
        string icon = status switch
        {
            IsaStatus.Disabled or IsaStatus.Unavailable => "white",
            IsaStatus.Fault => "amber",
            _ => "green",
        };
        string label = status switch
        {
            IsaStatus.Disabled => "Off",
            IsaStatus.Unavailable => "Unavailable",
            IsaStatus.Restricted => "Limiting",
            IsaStatus.Overspeed => "Overspeed",
            IsaStatus.Fault => "Fault",
            _ => "Active",
        };
        float factor = unit == "mph" ? 1f / 1.609344f : 1f;
        string limit = limitKmh is { } value && float.IsFinite(value) && value > 0f
            ? $"{value * factor:0}" : "—";
        // 2 Hz, 50% duty cycle, within the reference's 1–5 Hz visual warning. No sound.
        bool signVisible = status != IsaStatus.Overspeed || elapsedSeconds % 0.5d < 0.25d;
        return new HudPanel
        {
            Title = "Speed assistance",
            Subtitle = status switch
            {
                IsaStatus.Disabled => "ISA is switched off",
                IsaStatus.Unavailable => "Waiting for road speed data",
                IsaStatus.Overspeed => "Brake to reduce your speed",
                IsaStatus.Fault => "ISA unavailable — input released",
                _ => $"Current road limit · {unit}",
            },
            Status = label,
            StatusTone = status switch
            {
                IsaStatus.Fault => HudTone.Warning,
                IsaStatus.Overspeed => HudTone.Negative,
                IsaStatus.Active or IsaStatus.Restricted => HudTone.Positive,
                _ => HudTone.Neutral,
            },
            Visuals =
            [
                new HudImage
                {
                    Path = $"assets/isa-{icon}.png", AltText = $"ISA {label}", Size = new Vector2(64f, 48f),
                    ColourKey = Vector3.Zero, ColourKeyTolerance = 0.08f,
                },
                new HudBadge
                {
                    Text = limit, Size = new Vector2(48f), Visible = signVisible,
                    BorderColour = new Vector4(0.9f, 0.13f, 0.18f, 1f), BorderThickness = 4.5f,
                    BackgroundColour = Vector4.One, TextColour = new Vector4(0.04f, 0.04f, 0.04f, 1f),
                },
            ],
            Actions = [new HudButton("toggle", off ? "Off" : "On", off ? HudTone.Neutral : HudTone.Positive),
                new HudButton("units", unit, HudTone.Neutral)],
        };
    }
}
