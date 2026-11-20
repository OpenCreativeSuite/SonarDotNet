namespace SonarDotNet.Core;

/// <summary>One row in a custom dropdown.</summary>
public sealed record ChoiceOption(string Value, string Label, string Detail = "");

/// <summary>The currently open dropdown, anchored to the control that opened it.</summary>
public sealed record ChoiceMenuState(
    string Title,
    string Icon,
    string Selected,
    IReadOnlyList<ChoiceOption> Options,
    Action<string> Choose,
    float X,
    float Y,
    float Width);
