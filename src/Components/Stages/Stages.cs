using SonarDotNet.Core;

namespace SonarDotNet.Components;

/// <summary>Presentation metadata for the four conceptual processing stages.</summary>
internal sealed record StageInfo(
    StudioStage Stage,
    string Number,
    string Name,
    string Short,
    string Description,
    string Icon);

/// <summary>The stage list shown down the left side of the workspace.</summary>
internal static class Stages
{
    public static readonly StageInfo[] All =
    [
        new(
            StudioStage.Noise,
            "01",
            "Noise",
            "Room & breath",
            "Sets the floor. Room tone and breath are eased away while your voice stays intact.",
            Icons.Wind),
        new(
            StudioStage.Tone,
            "02",
            "Tone",
            "Shape & colour",
            "Move the curve until your voice sounds like you. Low rumble, body, presence and air.",
            Icons.Tone),
        new(
            StudioStage.Dynamics,
            "03",
            "Dynamics",
            "Steady & strong",
            "Holds the loud moments close and lifts the quiet ones, without flattening expression.",
            Icons.Dynamics),
        new(
            StudioStage.Character,
            "04",
            "Character",
            "Warmth & space",
            "A little harmonic weight and room. Enough to feel human, never a costume.",
            Icons.Character),
    ];

    public static StageInfo Get(StudioStage stage) => All.First(info => info.Stage == stage);
}
