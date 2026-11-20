using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The slim identity and preset bar.</summary>
internal sealed class TopBar : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Header(new() { ClassName = "topbar" },
            Div(new() { ClassName = "brand" },
                Div(new() { ClassName = "brand-mark" },
                    Span(new() { ClassName = "brand-bar b1" }),
                    Span(new() { ClassName = "brand-bar b2" }),
                    Span(new() { ClassName = "brand-bar b3" }),
                    Span(new() { ClassName = "brand-bar b4" }),
                    Span(new() { ClassName = "brand-bar b5" })),
                Div(new() { ClassName = "brand-copy" },
                    Span(new() { ClassName = "brand-name" }, Text("Sonus"), Span(new() { ClassName = "brand-net" }, Text(".NET"))),
                    Span(new() { ClassName = "brand-tag" }, Text("VOICE STUDIO")))),
            Div(new() { ClassName = "top-sep" }),
            Button(new()
            {
                ClassName = session.ShowPresets ? "preset-trigger is-open" : "preset-trigger",
                Title = "Choose a preset",
                OnClick = session.TogglePresets,
            },
            Span(new() { ClassName = "preset-kicker" }, Text("PRESET")),
            Span(new() { ClassName = "preset-name" }, Text(session.PresetName)),
            Ui.Icon(Icons.ChevronDown, "preset-chevron")),
            Div(new() { ClassName = "top-spacer" }),
            Div(new() { ClassName = "local-badge", Title = "Audio never leaves this computer" },
                Ui.Icon(Icons.Shield),
                Span(new() { ClassName = "local-copy" }, Text("LOCAL ONLY"))),
            Ui.IconButton(Icons.Save, "Save the current sound as a preset", session.OpenSavePreset, className: "icon-btn top-btn"),
            Ui.IconButton(Icons.Help, "How Sonus works", session.ToggleHelp, className: "icon-btn top-btn"),
            Ui.IconButton(Icons.Settings, "Audio and virtual microphone settings", session.ToggleSettings, className: "icon-btn top-btn"));
    }
}

/// <summary>The microphone, headphones, virtual output and live controls.</summary>
internal sealed class DeviceBar : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        List<(string Value, string Label)> inputs = session.Inputs
            .Select(device => (device.Id, device.Name))
            .ToList();
        if (inputs.Count == 0)
        {
            inputs.Add(("", "No microphone found"));
        }

        List<(string Value, string Label)> headphones = session.Outputs
            .Where(device => !device.IsVirtualCable)
            .Select(device => (device.Id, device.Name))
            .ToList();
        if (headphones.Count == 0)
        {
            headphones.Add(("", "No output found"));
        }

        return Div(new() { ClassName = "devicebar" },
            Div(new() { ClassName = "device-cluster" },
                Ui.Choice(session, "Microphone", session.InputDeviceId, inputs, session.SelectInput, Icons.Microphone)),
            Div(new() { ClassName = "device-cluster" },
                Ui.Choice(session, "Headphones", session.MonitorDeviceId, headphones, session.SelectMonitor, Icons.Headphones),
                Ui.Switch(session.MonitoringEnabled, "Mute or unmute headphone monitoring", session.ToggleMonitoring)),
            Div(new() { ClassName = "device-cluster virtual-cluster" },
                Div(new() { ClassName = "virtual-device", Title = "Choose this microphone in Discord, OBS or Teams" },
                    Ui.Icon(Icons.Nodes, "virtual-icon"),
                    Div(new() { ClassName = "virtual-copy" },
                        Span(new() { ClassName = "virtual-label" }, Text("Virtual microphone")),
                        Span(new() { ClassName = "virtual-name" }, Text(session.VirtualMicrophoneName))),
                    Ui.Switch(session.VirtualEnabled, "Enable or disable the virtual microphone", session.ToggleVirtual, session.VirtualDevice is not null)),
                session.HasVirtualCable
                    ? Span(new() { ClassName = "virtual-state" }, Text("READY"))
                    : Button(new() { ClassName = "virtual-state is-warn", Title = "Set up a virtual cable", OnClick = session.ToggleSettings }, Text("SET UP"))),
            Div(new() { ClassName = "device-spacer" }),
            Button(new()
            {
                ClassName = session.IsLive ? "live-btn is-on" : "live-btn",
                Title = session.IsLive ? "Stop microphone and outputs" : "Start listening",
                OnClick = session.ToggleLive,
            },
            Ui.Icon(Icons.Power),
            Span(new() { ClassName = "live-label" }, Text(session.IsLive ? "LIVE" : "START"))));
    }
}
