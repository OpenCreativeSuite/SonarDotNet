using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The large editor for the selected processing stage.</summary>
internal sealed class StageEditor : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        StageInfo info = Stages.Get(session.ActiveStage);
        bool enabled = StageRail.Enabled(session, info.Stage);

        return Section(new() { ClassName = "editor" },
            Header(session, info, enabled),
            Div(new() { ClassName = "editor-body" },
                Div(new() { ClassName = "graph-card" },
                    Div(new() { ClassName = "graph-head" },
                        Span(new() { ClassName = "graph-title" }, Text(GraphTitle(info.Stage))),
                        Span(new() { ClassName = "graph-note" }, Text(GraphNote(session, info.Stage)))),
                    GraphFor(info.Stage)),
                Controls(session, info.Stage)));
    }

    private static Node Header(StudioSession session, StageInfo info, bool enabled)
    {
        return Div(new() { ClassName = "editor-head" },
            Div(new() { ClassName = "editor-title-block" },
                Span(new() { ClassName = "editor-eyebrow" }, Text($"STAGE {info.Number}")),
                Div(new() { ClassName = "editor-title-row" },
                    H1(new() { ClassName = "editor-title" }, Text(info.Name)),
                    Span(new() { ClassName = "editor-tag" }, Text(info.Short))),
                P(new() { ClassName = "editor-description" }, Text(info.Description))),
            Div(new() { ClassName = "editor-actions" },
                Div(new() { ClassName = "editor-enable" },
                    Span(new() { ClassName = "editor-enable-label" }, Text(enabled ? "In path" : "Bypassed")),
                    Ui.Switch(enabled, $"{(enabled ? "Bypass" : "Enable")} {info.Name}", () => StageRail.Toggle(session, info.Stage))),
                Ui.IconButton(Icons.Reset, $"Reset {info.Name}", session.ResetStage, className: "icon-btn action-btn"),
                Div(new() { ClassName = "stage-accent" })));
    }

    private static Node GraphFor(StudioStage stage) => stage switch
    {
        StudioStage.Clean => C<NoiseGraph>(),
        StudioStage.Tone => C<ToneGraph>(),
        StudioStage.Dynamics => C<DynamicsGraph>(),
        StudioStage.Voice => C<CharacterGraph>(),
        _ => C<SpaceGraph>(),
    };

    private static string GraphTitle(StudioStage stage) => stage switch
    {
        StudioStage.Clean => "Gate response",
        StudioStage.Tone => "Frequency response",
        StudioStage.Dynamics => "Compression curve",
        StudioStage.Voice => "Vocal shape",
        _ => "Room & echo",
    };

    private static string GraphNote(StudioSession session, StudioStage stage) => stage switch
    {
        StudioStage.Clean => $"{session.Settings.Noise.ReductionDb:0} dB of room reduction",
        StudioStage.Tone => $"{session.Settings.Tone.Bands.Length} movable points · 20 Hz–20 kHz",
        StudioStage.Dynamics => $"{session.Settings.Dynamics.Ratio:0.0}:1 · {session.Settings.Dynamics.ThresholdDb:0} dB threshold",
        StudioStage.Voice => $"{Ui.Semitones(session.Settings.Character.PitchSemitones)} pitch · {Ui.Semitones(session.Settings.Character.FormantSemitones)} formant",
        _ => $"{session.Settings.Character.Space:0}% room · {session.Settings.Character.DelayMix * 100f:0}% echo",
    };

    private static Node Controls(StudioSession session, StudioStage stage) => stage switch
    {
        StudioStage.Clean => NoiseControls(session),
        StudioStage.Tone => ToneControls(session),
        StudioStage.Dynamics => DynamicsControls(session),
        StudioStage.Voice => VoiceControls(session),
        _ => SpaceControls(session),
    };

    private static Node NoiseControls(StudioSession session)
    {
        NoiseSettings noise = session.Settings.Noise;
        return Div(new() { ClassName = "control-deck is-three" },
            Ui.Slider(session, "Threshold", noise.ThresholdDb, -70f, -12f, 1f, $"{noise.ThresholdDb:0} dB", value =>
                session.UpdateNoise(settings => settings with { ThresholdDb = Ui.Parse(value, -70f, -12f, noise.ThresholdDb) }),
                "The level below which the gate starts closing."),
            Ui.Slider(session, "Reduction", noise.ReductionDb, 0f, 60f, 1f, $"{noise.ReductionDb:0} dB", value =>
                session.UpdateNoise(settings => settings with { ReductionDb = Ui.Parse(value, 0f, 60f, noise.ReductionDb) }),
                "How quiet the room becomes between phrases."),
            ToggleBlock("Gentle expansion", "Keep quiet tails natural", noise.Suppress, () =>
                session.UpdateNoise(settings => settings with { Suppress = !settings.Suppress })));
    }

    private static Node ToneControls(StudioSession session)
    {
        ToneSettings tone = session.Settings.Tone;
        return Div(new() { ClassName = "control-deck is-two" },
            Ui.Slider(session, "Low cut", tone.LowCutHz, 30f, 300f, 1f, Ui.Hz(tone.LowCutHz), value =>
                session.UpdateTone(settings => settings with { LowCutHz = Ui.Parse(value, 30f, 300f, tone.LowCutHz) }),
                "Removes rumble and handling noise."),
            Ui.Slider(session, "High cut", tone.HighCutHz, 6000f, 20000f, 100f, Ui.Hz(tone.HighCutHz), value =>
                session.UpdateTone(settings => settings with { HighCutHz = Ui.Parse(value, 6000f, 20000f, tone.HighCutHz) }),
                "Softens hiss and harshness above the voice."));
    }

    private static Node DynamicsControls(StudioSession session)
    {
        DynamicsSettings dynamics = session.Settings.Dynamics;
        return Div(new() { ClassName = "control-deck" },
            Ui.Slider(session, "Threshold", dynamics.ThresholdDb, -50f, -2f, 1f, $"{dynamics.ThresholdDb:0} dB", value =>
                session.UpdateDynamics(settings => settings with { ThresholdDb = Ui.Parse(value, -50f, -2f, dynamics.ThresholdDb) }),
                "Where steady control begins."),
            Ui.Slider(session, "Ratio", dynamics.Ratio, 1f, 12f, 0.1f, $"{dynamics.Ratio:0.0}:1", value =>
                session.UpdateDynamics(settings => settings with { Ratio = Ui.Parse(value, 1f, 12f, dynamics.Ratio) }),
                "How firmly loud peaks are held."),
            Ui.Slider(session, "Lift", dynamics.MakeupDb, 0f, 18f, 0.5f, Ui.Db(dynamics.MakeupDb), value =>
                session.UpdateDynamics(settings => settings with { MakeupDb = Ui.Parse(value, 0f, 18f, dynamics.MakeupDb) }),
                "Make-up gain after compression."),
            ToggleBlock("Output limiter", "Keeps peaks below −1 dBFS", dynamics.Limiter, () =>
                session.UpdateDynamics(settings => settings with { Limiter = !settings.Limiter })));
    }

    private static Node VoiceControls(StudioSession session)
    {
        CharacterSettings character = session.Settings.Character;
        float vocalSize = 50f + (character.FormantSemitones / 12f * 50f);
        return Div(new() { ClassName = "control-deck is-five" },
            Ui.Slider(session, "Pitch", character.PitchSemitones, -12f, 12f, 1f, Ui.Semitones(character.PitchSemitones), value =>
                session.UpdateCharacter(settings => settings with { PitchSemitones = MathF.Round(Ui.Parse(value, -12f, 12f, character.PitchSemitones)) }),
                "Moves the fundamental pitch without changing speed."),
            Ui.Slider(session, "Vocal size", vocalSize, 0f, 100f, 1f, $"{vocalSize:0}%", value =>
                session.UpdateCharacter(settings => settings with { FormantSemitones = (Ui.Parse(value, 0f, 100f, vocalSize) - 50f) / 50f * 12f }),
                "Moves the formants: smaller sounds younger, larger sounds deeper."),
            Ui.Slider(session, "Resonance", character.Resonance * 50f + 50f, 0f, 100f, 1f, $"{character.Resonance * 50f + 50f:0}%", value =>
                session.UpdateCharacter(settings => settings with { Resonance = (Ui.Parse(value, 0f, 100f, character.Resonance * 50f + 50f) - 50f) / 50f }),
                "The weight and placement around the vocal body."),
            Ui.Slider(session, "Brightness", character.Brightness * 50f + 50f, 0f, 100f, 1f, $"{character.Brightness * 50f + 50f:0}%", value =>
                session.UpdateCharacter(settings => settings with { Brightness = (Ui.Parse(value, 0f, 100f, character.Brightness * 50f + 50f) - 50f) / 50f }),
                "Darkens or opens the top of the voice."),
            Ui.Slider(session, "Texture", character.Warmth, 0f, 100f, 1f, Ui.Percent(character.Warmth), value =>
                session.UpdateCharacter(settings => settings with { Warmth = Ui.Parse(value, 0f, 100f, character.Warmth) }),
                "Adds harmonic grip, from smooth to rough."));
    }

    private static Node SpaceControls(StudioSession session)
    {
        CharacterSettings character = session.Settings.Character;
        return Div(new() { ClassName = "control-deck" },
            Ui.Slider(session, "Room", character.Space, 0f, 100f, 1f, Ui.Percent(character.Space), value =>
                session.UpdateCharacter(settings => settings with { Space = Ui.Parse(value, 0f, 100f, character.Space) }),
                "How much of the short room you hear."),
            Ui.Slider(session, "Room mix", character.SpaceMix * 100f, 0f, 100f, 1f, Ui.Percent(character.SpaceMix * 100f), value =>
                session.UpdateCharacter(settings => settings with { SpaceMix = Ui.Parse(value, 0f, 100f, character.SpaceMix * 100f) / 100f }),
                "The balance between the dry voice and the room."),
            Ui.Slider(session, "Echo", character.DelayMix * 100f, 0f, 100f, 1f, Ui.Percent(character.DelayMix * 100f), value =>
                session.UpdateCharacter(settings => settings with { DelayMix = Ui.Parse(value, 0f, 100f, character.DelayMix * 100f) / 100f }),
                "A single repeat behind the voice."),
            Ui.Slider(session, "Echo time", character.DelayMs, 80f, 450f, 5f, $"{character.DelayMs:0} ms", value =>
                session.UpdateCharacter(settings => settings with { DelayMs = Ui.Parse(value, 80f, 450f, character.DelayMs) }),
                "The distance between the voice and its echo."));
    }

    private static Node ToggleBlock(string title, string subtitle, bool on, Action toggle) =>
        Div(new() { ClassName = "toggle-block" },
            Div(new() { ClassName = "toggle-copy" },
                Span(new() { ClassName = "toggle-title" }, Text(title)),
                Span(new() { ClassName = "toggle-subtitle" }, Text(subtitle))),
            Ui.Switch(on, title, toggle));
}
