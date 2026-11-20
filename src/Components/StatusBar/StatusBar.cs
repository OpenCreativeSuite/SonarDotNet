using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The bottom status line: engine state, current message and shortcuts.</summary>
internal sealed class StatusBar : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        string engine = session.IsLive ? "Engine live" : "Engine idle";
        string rate = $"{session.Telemetry.SampleRate / 1000.0:0.#} kHz";
        string buffer = session.IsLive ? $"{session.Telemetry.BufferFrames} frames" : "256 frames";
        return Footer(new() { ClassName = "statusbar" },
            Span(new() { ClassName = session.StatusIsError ? "status-led is-error" : session.IsLive ? "status-led is-on" : "status-led" }),
            Span(new() { ClassName = "status-item is-strong" }, Text(engine)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item" }, Text(rate)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text(buffer)),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text(session.Telemetry.LatencyMs > 1f ? $"{session.Telemetry.LatencyMs:0} ms monitor" : "monitor muted")),
            Div(new() { ClassName = "status-spacer" }),
            Span(new() { ClassName = "status-message" }, Text(session.Status)),
            Div(new() { ClassName = "status-spacer" }),
            Span(new() { ClassName = "status-item is-muted" }, Text("Space  live")),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text("R  record")),
            Span(new() { ClassName = "status-sep" }),
            Span(new() { ClassName = "status-item is-muted" }, Text("1–5  stage")));
    }
}
