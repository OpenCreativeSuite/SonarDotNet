using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The four-stage signal path on the left of the workspace.</summary>
internal sealed class StageRail : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Aside(new() { ClassName = "rail" },
            Div(new() { ClassName = "rail-head" },
                Span(new() { ClassName = "rail-kicker" }, Text("SIGNAL PATH")),
                Span(new() { ClassName = "rail-title" }, Text("Five stages. One clear voice."))),
            Div(new() { ClassName = "stage-list" },
                Each(Stages.All, info => (Node)Card(session, info))),
            Div(new() { ClassName = "rail-note" },
                Ui.Icon(Icons.Shield),
                Span(new() { ClassName = "rail-note-copy" }, Text("Audio stays on this computer."))));
    }

    private static Node Card(StudioSession session, StageInfo info)
    {
        bool active = session.ActiveStage == info.Stage;
        bool enabled = Enabled(session, info.Stage);
        List<string> classes = ["stage-card"];
        if (active)
        {
            classes.Add("is-active");
        }

        if (!enabled)
        {
            classes.Add("is-off");
        }

        return Button(new()
        {
            ClassName = string.Join(' ', classes),
            Title = $"Open the {info.Name} stage",
            OnClick = () => session.SelectStage(info.Stage),
            OnPointerDownEvent = _ => session.SelectStage(info.Stage),
        },
        Div(new() { ClassName = "stage-rail-line" }),
        Span(new() { ClassName = "stage-number" }, Text(info.Number)),
        Span(new() { ClassName = "stage-icon" }, Ui.Icon(info.Icon)),
        Div(new() { ClassName = "stage-names" },
            Span(new() { ClassName = "stage-name" }, Text(info.Name)),
            Span(new() { ClassName = "stage-short" }, Text(info.Short))),
        Span(new() { ClassName = enabled ? "stage-state-dot is-on" : "stage-state-dot" }));
    }

    internal static bool Enabled(StudioSession session, StudioStage stage) => stage switch
    {
        StudioStage.Clean => session.Settings.Noise.Enabled,
        StudioStage.Tone => session.Settings.Tone.Enabled,
        StudioStage.Dynamics => session.Settings.Dynamics.Enabled,
        StudioStage.Voice => session.Settings.Character.Enabled,
        StudioStage.Space => session.Settings.Character.SpaceEnabled,
        _ => true,
    };

    internal static void Toggle(StudioSession session, StudioStage stage)
    {
        switch (stage)
        {
            case StudioStage.Clean:
                session.UpdateNoise(settings => settings with { Enabled = !settings.Enabled });
                break;
            case StudioStage.Tone:
                session.UpdateTone(settings => settings with { Enabled = !settings.Enabled });
                break;
            case StudioStage.Dynamics:
                session.UpdateDynamics(settings => settings with { Enabled = !settings.Enabled });
                break;
            case StudioStage.Voice:
                session.UpdateCharacter(settings => settings with { Enabled = !settings.Enabled });
                break;
            case StudioStage.Space:
                session.UpdateCharacter(settings => settings with { SpaceEnabled = !settings.SpaceEnabled });
                break;
        }
    }
}
