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
            StudioStage.Clean,
            "01",
            "Clean",
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
            StudioStage.Voice,
            "04",
            "Voice",
            "Size & character",
            "Pitch, formants, resonance and texture. Build a different voice without leaving yours behind.",
            Icons.Feather),
        new(
            StudioStage.Space,
            "05",
            "Space",
            "Room & echo",
            "Place the voice in a room, a hallway or somewhere stranger. Space is part of the character.",
            Icons.Music),
    ];

    public static StageInfo Get(StudioStage stage) => All.First(info => info.Stage == stage);
}
