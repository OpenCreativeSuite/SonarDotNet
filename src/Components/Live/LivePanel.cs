using Donkeh.UILab;
using SonarDotNet.Core;
using SonarDotNet.Core.Audio;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The right-hand live signal and recording panel.</summary>
internal sealed class LivePanel : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Aside(new() { ClassName = "live" },
            Div(new() { ClassName = "live-head" },
                Div(new() { ClassName = "live-title-block" },
                    Span(new() { ClassName = "live-kicker" }, Text("LIVE SIGNAL")),
                    Span(new() { ClassName = "live-title" }, Text("Hear the result"))),
                Ui.StatusDot(session.IsLive, session.IsLive ? "LIVE" : "IDLE")),
            SignalCard(session),
            MonitorCard(session),
            RecordingCard(session));
    }

    private static Node SignalCard(StudioSession session) =>
        Div(new() { ClassName = "live-card scope-card" },
            Div(new() { ClassName = "card-label-row" },
                Span(new() { ClassName = "card-label" }, Text("WAVEFORM")),
                Span(new() { ClassName = "card-meta" }, Text(session.IsLive ? "processed" : "standby"))),
            C<LiveScope>(),
            C<LevelMeters>());

    private static Node MonitorCard(StudioSession session) =>
        Div(new() { ClassName = "live-card" },
            Div(new() { ClassName = "card-label-row" },
                Span(new() { ClassName = "card-label" }, Text("MONITOR")),
                Span(new() { ClassName = "card-meta" }, Text(session.MonitorDevice?.ShortName ?? "No headphones"))),
            Div(new() { ClassName = "monitor-row" },
                Div(new() { ClassName = "monitor-icon" }, Ui.Icon(session.MonitoringEnabled ? Icons.Headphones : Icons.VolumeOff)),
                Ui.Slider(
                    session,
                    "Headphone level",
                    session.MonitorVolume * 100f,
                    0f,
                    100f,
                    1f,
                    Ui.Percent(session.MonitorVolume * 100f),
                    value => session.SetMonitorVolume(Ui.Parse(value, 0f, 100f, session.MonitorVolume * 100f) / 100f),
                    "Independent from the virtual microphone."),
                Button(new() { ClassName = "monitor-test", Title = "Play a short chime through the selected headphones", OnClick = session.TestMonitor }, Text("TEST")),
                Ui.Switch(session.MonitoringEnabled, "Mute or unmute monitoring", session.ToggleMonitoring)));

    private static Node RecordingCard(StudioSession session)
    {
        bool recording = session.Recording is not null;
        return Div(new() { ClassName = recording ? "live-card record-card is-recording" : "live-card record-card" },
            Div(new() { ClassName = "card-label-row" },
                Span(new() { ClassName = "card-label" }, Text("TAKES")),
                Span(new() { ClassName = "card-meta" }, Text("raw + processed WAV"))),
            Div(new() { ClassName = "record-action" },
                Button(new()
                {
                    ClassName = recording ? "record-btn is-recording" : "record-btn",
                    Title = recording ? "Finish this take" : "Record raw and processed audio",
                    OnClick = () => { if (recording) { session.StopRecording(); } else { session.StartRecording(); } },
                },
                Span(new() { ClassName = "record-dot" },
                    Ui.Icon(recording ? Icons.Stop : Icons.Record)),
                Span(new() { ClassName = "record-copy" },
                    Span(new() { ClassName = "record-title" }, Text(recording ? "Finish take" : "Record a take")),
                    Span(new() { ClassName = "record-sub" }, Text(recording ? "Both files are being written" : "Keep both versions to compare"))),
                Span(new() { ClassName = "record-timer" }, Text(recording ? Ui.Clock(session.RecordingElapsed.TotalSeconds) : "00:00")))),
            session.Takes.Count == 0
                ? Div(new() { ClassName = "empty-takes" },
                    Ui.Icon(Icons.Wave, "empty-icon"),
                    Span(new() { ClassName = "empty-title" }, Text("No takes yet")),
                    Span(new() { ClassName = "empty-copy" }, Text("Record once and compare raw with processed.")))
                : Div(new() { ClassName = "take-list" },
                    session.Takes.Take(3).Select((take, index) => (Node)TakeRow(session, take, index)).ToArray()),
            session.SelectedTake is not null ? Transport(session, session.SelectedTake) : null);
    }

    private static Node TakeRow(StudioSession session, RecordingResult take, int index)
    {
        bool selected = ReferenceEquals(session.SelectedTake, take);
        string name = $"Take {session.Takes.Count - index:00}";
        return Div(new() { ClassName = selected ? "take-row is-selected" : "take-row" },
            Span(new() { ClassName = "take-index" }, Text(name)),
            Span(new() { ClassName = "take-time" }, Text(Ui.Clock(take.Duration.TotalSeconds))),
            Div(new() { ClassName = "take-actions" },
                Button(new()
                {
                    ClassName = selected && session.PlayingProcessed && session.IsPlaying ? "take-play is-on" : "take-play",
                    Title = "Play the processed take",
                    OnClick = () => session.ToggleTake(take, true),
                }, Ui.Icon(session.PlayingProcessed && selected && session.IsPlaying ? Icons.Pause : Icons.Play)),
                Button(new()
                {
                    ClassName = selected && !session.PlayingProcessed && session.IsPlaying ? "take-play is-on" : "take-play",
                    Title = "Play the raw take",
                    OnClick = () => session.ToggleTake(take, false),
                }, Span(new() { ClassName = "raw-label" }, Text("RAW")))));
    }

    private static Node Transport(StudioSession session, RecordingResult take)
    {
        double duration = session.PlaybackDuration > 0 ? session.PlaybackDuration : take.Duration.TotalSeconds;
        double position = Math.Min(session.PlaybackPosition, duration);
        float fraction = duration > 0 ? (float)(position / duration) : 0f;
        return Div(new() { ClassName = "transport" },
            Button(new()
            {
                ClassName = "transport-play",
                Title = session.IsPlaying ? "Pause" : "Play",
                OnClick = session.TogglePlayback,
            }, Ui.Icon(session.IsPlaying ? Icons.Pause : Icons.Play)),
            Div(new() { ClassName = "transport-track" },
                Input(new()
                {
                    ClassName = "transport-seek",
                    Type = "range",
                    Value = Ui.Number(fraction * 100f),
                    OnInput = value => session.SeekPlayback(Ui.Parse(value, 0f, 100f, fraction * 100f) / 100f),
                    Attributes = new() { ["min"] = "0", ["max"] = "100", ["step"] = "0.1" },
                })),
            Span(new() { ClassName = "transport-time" }, Text($"{Ui.Clock(position)} / {Ui.Clock(duration)}")),
            Button(new()
            {
                ClassName = "transport-mode",
                Title = "Switch between raw and processed",
                OnClick = () => session.ToggleTake(take, !session.PlayingProcessed),
            }, Text(session.PlayingProcessed ? "PROCESSED" : "RAW")));
    }
}
