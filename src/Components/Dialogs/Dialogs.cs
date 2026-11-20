using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The preset chooser, save form, audio settings and help sheet.</summary>
internal sealed class PresetPopover : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Div(new()
        {
            ClassName = "popover-layer",
            OnClick = session.CloseLayers,
        },
        Div(new()
        {
            ClassName = "preset-popover",
            OnClick = () => { },
        },
        Div(new() { ClassName = "popover-head" },
            Div(new() { ClassName = "popover-copy" },
                Span(new() { ClassName = "popover-kicker" }, Text("SOUND LIBRARY")),
                Span(new() { ClassName = "popover-title" }, Text("Choose a starting point"))),
            Ui.IconButton(Icons.Close, "Close presets", session.CloseLayers, className: "icon-btn popover-close")),
        Div(new() { ClassName = "preset-list" },
            Each(session.Presets, preset => (Node)PresetRow(session, preset))),
        Div(new() { ClassName = "popover-foot" },
            Button(new() { ClassName = "btn is-quiet", OnClick = session.OpenSavePreset },
                Ui.Icon(Icons.Plus, "btn-icon"),
                Text("Save current sound")),
            Span(new() { ClassName = "popover-note" }, Text("Presets are plain JSON on this computer.")))));
    }

    private static Node PresetRow(StudioSession session, VoicePreset preset)
    {
        bool active = session.PresetName == preset.Name;
        return Div(new() { ClassName = active ? "preset-row is-active" : "preset-row" },
            Button(new()
            {
                ClassName = "preset-apply",
                OnClick = () =>
                {
                    session.ApplyPreset(preset);
                    session.CloseLayers();
                },
            },
            Span(new() { ClassName = "preset-row-mark" },
                Ui.Icon(active ? Icons.Check : (preset.BuiltIn ? Icons.Wave : Icons.Take))),
            Div(new() { ClassName = "preset-row-copy" },
                Div(new() { ClassName = "preset-row-title" },
                    Span(new() { ClassName = "preset-row-name" }, Text(preset.Name)),
                    preset.BuiltIn ? Span(new() { ClassName = "preset-tag" }, Text("BUILT IN")) : Span(new() { ClassName = "preset-tag is-user" }, Text("YOURS"))),
                Span(new() { ClassName = "preset-row-description" }, Text(preset.Description)))),
            preset.BuiltIn ? null : Ui.IconButton(Icons.Trash, "Delete this preset", () => session.DeletePreset(preset), className: "icon-btn preset-delete"));
    }
}

/// <summary>The save-as-preset sheet.</summary>
internal sealed class SavePresetDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Div(new()
        {
            ClassName = "modal-backdrop",
            OnClick = session.CloseLayers,
        },
        Div(new()
        {
            ClassName = "modal save-modal",
            OnClick = () => { },
        },
        Ui.ModalTitle(Icons.Save, "Save this sound", "A named preset in your local library.", session.CloseLayers),
        Div(new() { ClassName = "modal-body" },
            Span(new() { ClassName = "field-label" }, Text("Preset name")),
            Input(new()
            {
                ClassName = "text-field big-field",
                Type = "text",
                Value = session.PresetDraftName,
                Placeholder = "My Voice",
                OnInput = session.SetPresetDraft,
                OnFocus = () => session.IsTyping = true,
                OnBlur = () => session.IsTyping = false,
                OnKeyDown = chord =>
                {
                    if (chord == "Enter")
                    {
                        session.SavePreset(session.PresetDraftName);
                    }
                },
            }),
            Ui.Hint("The current tone, noise, dynamics and character values are stored together.")),
        Div(new() { ClassName = "modal-actions" },
            Button(new() { ClassName = "btn is-quiet", OnClick = session.CloseLayers }, Text("Cancel")),
            Button(new() { ClassName = "btn is-primary", OnClick = () => session.SavePreset(session.PresetDraftName) },
                Ui.Icon(Icons.Save, "btn-icon"),
                Text("Save preset")))));
    }
}

/// <summary>Audio and virtual-cable configuration.</summary>
internal sealed class SettingsDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        List<(string Value, string Label)> inputs = session.Inputs.Select(device => (device.Id, device.Name)).ToList();
        if (inputs.Count == 0)
        {
            inputs.Add(("", "No microphone found"));
        }

        List<(string Value, string Label)> outputs = session.Outputs
            .OrderByDescending(device => device.IsVirtualCable)
            .Select(device => (device.Id, device.Name))
            .ToList();
        if (outputs.Count == 0)
        {
            outputs.Add(("", "No output found"));
        }

        List<(string Value, string Label)> headphones = session.Outputs
            .Where(device => !device.IsVirtualCable)
            .Select(device => (device.Id, device.Name))
            .ToList();
        if (headphones.Count == 0)
        {
            headphones.Add(("", "No output found"));
        }

        return Ui.Modal("settings-modal", session.CloseLayers,
            Ui.ModalTitle(Icons.Settings, "Audio & output", "Devices are used directly by Windows. Nothing is uploaded.", session.CloseLayers),
            Div(new() { ClassName = "modal-body settings-body" },
                Div(new() { ClassName = "settings-grid" },
                    Setting("Microphone", "The physical input Sonus listens to.", Ui.Choice(session, "Input device", session.InputDeviceId, inputs, session.SelectInput, Icons.Microphone)),
                    Setting("Headphones", "Independent monitoring for you.", Ui.Choice(session, "Monitor device", session.MonitorDeviceId, headphones, session.SelectMonitor, Icons.Headphones)),
                    Setting("Virtual microphone", "A playback endpoint that other apps can select as a microphone.", Ui.Choice(session, "Virtual routing", session.VirtualDeviceId, outputs, session.SelectVirtual, Icons.Nodes)),
                    Setting("Input trim", "Clean gain before the noise stage.", Ui.Slider(session, "Trim", session.Settings.InputGainDb, -12f, 18f, 0.5f, Ui.Db(session.Settings.InputGainDb), value => session.SetInputGain(Ui.Parse(value, -12f, 18f, session.Settings.InputGainDb))))),
                Div(new() { ClassName = session.HasVirtualCable ? "cable-note is-ready" : "cable-note" },
                    Div(new() { ClassName = "cable-note-icon" }, Ui.Icon(session.HasVirtualCable ? Icons.Check : Icons.CircleInfo)),
                    Div(new() { ClassName = "cable-note-copy" },
                        Span(new() { ClassName = "cable-note-title" }, Text(session.HasVirtualCable ? "Virtual microphone ready" : "Set up your virtual microphone")),
                        Span(new() { ClassName = "cable-note-text" }, Text(session.HasVirtualCable
                            ? $"Sonus sends processed audio to {session.VirtualDevice?.Name}. In Discord, OBS or Teams, choose CABLE Output as your microphone. Nothing else is needed."
                            : "Windows requires a signed audio driver for a virtual microphone. Sonus uses the free VB-CABLE driver, downloads the official package and opens its installer. After the installer finishes, restart Windows once.")),
                        session.HasVirtualCable
                            ? null
                            : Div(new() { ClassName = "cable-actions" },
                                Button(new() { ClassName = "btn is-primary", OnClick = () => VirtualCableSetup.Install(session) },
                                    Ui.Icon(Icons.Download, "btn-icon"),
                                    Text("Download & install")),
                                Button(new() { ClassName = "btn is-quiet", OnClick = () => VirtualCableSetup.OpenPage(session) },
                                    Text("Open VB-CABLE page")),
                                Span(new() { ClassName = "cable-attribution" }, Text("Free donationware by VB-Audio")))))),
            Div(new() { ClassName = "modal-actions is-split" },
                Div(new() { ClassName = "modal-actions-left" },
                    Button(new() { ClassName = "btn is-quiet", OnClick = session.RefreshDevices },
                        Ui.Icon(Icons.Reset, "btn-icon"),
                        Text("Refresh devices")),
                    Span(new() { ClassName = "settings-version" }, Text("Sonus.NET · local-first voice studio"))),
                Div(new() { ClassName = "modal-actions-right" },
                    Button(new() { ClassName = "btn is-primary", OnClick = session.CloseLayers }, Text("Done")))));
    }

    private static Node Setting(string title, string copy, Node control) =>
        Div(new() { ClassName = "setting" },
            Div(new() { ClassName = "setting-copy" },
                Span(new() { ClassName = "setting-title" }, Text(title)),
                Span(new() { ClassName = "setting-text" }, Text(copy))),
            control);
}

/// <summary>Short in-app guide, including the virtual-cable step.</summary>
internal sealed class HelpDialog : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        return Ui.Modal("help-modal", session.CloseLayers,
            Ui.ModalTitle(Icons.Wave, "How Sonus works", "Four stages between your microphone and the rest of Windows.", session.CloseLayers),
            Div(new() { ClassName = "modal-body help-body" },
                Div(new() { ClassName = "help-flow" },
                    HelpStep("01", "Listen", "Choose a microphone and headphones. Turn on monitoring to hear the processed voice with no echo."),
                    HelpStep("02", "Shape", "Use the four stages. Drag the tone points; the graph is the control, not a picture of one."),
                    HelpStep("03", "Route", "Install VB-CABLE, enable Virtual microphone, then choose CABLE Output in Discord or OBS."),
                    HelpStep("04", "Compare", "Record a take. Sonus writes both the raw microphone and the processed result.")) ,
                Div(new() { ClassName = "help-note" },
                    Ui.Icon(Icons.Shield, "help-note-icon"),
                    Span(new() { ClassName = "help-note-copy" }, Text("Audio is captured, shaped and written on this computer. Sonus does not use an account, cloud processing or telemetry."))),
                Div(new() { ClassName = "shortcut-grid" },
                    Shortcut("Space", "Start or stop live audio"),
                    Shortcut("R", "Record a take"),
                    Shortcut("1 – 4", "Choose a processing stage"),
                    Shortcut("Esc", "Close a sheet"))),
            Div(new() { ClassName = "modal-actions" },
                Button(new() { ClassName = "btn is-primary", OnClick = session.CloseLayers }, Text("Start shaping"))));
    }

    private static Node HelpStep(string number, string title, string copy) =>
        Div(new() { ClassName = "help-step" },
            Span(new() { ClassName = "help-step-number" }, Text(number)),
            Span(new() { ClassName = "help-step-title" }, Text(title)),
            Span(new() { ClassName = "help-step-copy" }, Text(copy)));

    private static Node Shortcut(string key, string description) =>
        Div(new() { ClassName = "shortcut-row" },
            Span(new() { ClassName = "keycap" }, Text(key)),
            Span(new() { ClassName = "shortcut-copy" }, Text(description)));
}
