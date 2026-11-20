using System.Globalization;
using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>Small markup helpers shared by the chrome, stages and live panel.</summary>
internal static class Ui
{
    /// <summary>Formats a number for an attribute without trailing noise.</summary>
    public static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Formats a signed decibel value.</summary>
    public static string Db(float value) => (value > 0.05f ? "+" : "") + value.ToString("0.#", CultureInfo.InvariantCulture) + " dB";

    /// <summary>Formats a frequency compactly.</summary>
    public static string Hz(float value) =>
        value >= 1000f
            ? (value / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + " kHz"
            : value.ToString("0", CultureInfo.InvariantCulture) + " Hz";

    /// <summary>Formats a percentage.</summary>
    public static string Percent(float value) => MathF.Round(value).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Formats a pitch change in semitones.</summary>
    public static string Semitones(float value)
    {
        int rounded = (int)MathF.Round(value);
        return rounded == 0 ? "0 st" : (rounded > 0 ? "+" : "") + rounded.ToString(CultureInfo.InvariantCulture) + " st";
    }

    /// <summary>Formats seconds as minutes and seconds.</summary>
    public static string Clock(double seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
        return time.TotalHours >= 1.0
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    /// <summary>Parses a float invariant-culture value inside a range.</summary>
    public static float Parse(string text, float min, float max, float fallback)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return Math.Clamp(value, min, max);
        }

        return fallback;
    }

    /// <summary>A Font Awesome glyph.</summary>
    public static Node Icon(string name, string extra = "") =>
        I(new() { ClassName = $"fa {name} {extra}".TrimEnd() });

    /// <summary>A square icon button.</summary>
    public static Node IconButton(
        string icon,
        string title,
        Action onClick,
        bool enabled = true,
        bool active = false,
        string className = "icon-btn")
    {
        List<string> classes = [className];
        if (active)
        {
            classes.Add("is-on");
        }

        return Button(new()
        {
            ClassName = string.Join(' ', classes),
            Title = title,
            Disabled = !enabled,
            OnClick = onClick,
        }, Icon(icon));
    }

    /// <summary>A small toggle with a track and a thumb.</summary>
    public static Node Switch(bool on, string title, Action onClick, bool enabled = true)
    {
        List<string> classes = ["switch"];
        if (on)
        {
            classes.Add("is-on");
        }

        if (!enabled)
        {
            classes.Add("is-disabled");
        }

        return Button(new()
        {
            ClassName = string.Join(' ', classes),
            Title = title,
            Disabled = !enabled,
            OnClick = onClick,
        },
        Span(new() { ClassName = "switch-track" },
            Span(new() { ClassName = "switch-thumb" })));
    }

    /// <summary>A labelled range control with a live readout.</summary>
    public static Node Slider(
        StudioSession session,
        string label,
        float value,
        float min,
        float max,
        float step,
        string display,
        Action<string> onInput,
        string hint = "")
    {
        float fill = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) * 100f : 0f;
        return Div(new() { ClassName = "slider", Title = hint },
            Div(new() { ClassName = "slider-head" },
                Span(new() { ClassName = "slider-label" }, Text(label)),
                Span(new() { ClassName = "slider-value" }, Text(display))),
            Input(new()
            {
                ClassName = "slider-input",
                Type = "range",
                Value = Number(value),
                OnInput = onInput,
                OnChange = onInput,
                OnFocus = () => session.IsTyping = true,
                OnBlur = () => session.IsTyping = false,
                Attributes = new()
                {
                    ["min"] = Number(min),
                    ["max"] = Number(max),
                    ["step"] = Number(step),
                    ["style"] = $"--fill:{fill:0.#}%",
                },
            }));
    }

    /// <summary>A labelled custom dropdown control.</summary>
    public static Node Choice(
        StudioSession session,
        string label,
        string value,
        IEnumerable<(string Value, string Label)> options,
        Action<string> onChange,
        string icon = "")
    {
        List<ChoiceOption> list = options.Select(option => new ChoiceOption(option.Value, option.Label)).ToList();
        string selected = list.FirstOrDefault(option => option.Value == value)?.Label ?? "Not selected";
        Ref<ElementNode> reference = new();
        List<Node> children = [];
        if (icon.Length > 0)
        {
            children.Add(Icon(icon, "choice-icon"));
        }

        children.Add(Span(new() { ClassName = "choice-label" }, Text(label)));
        children.Add(Span(new() { ClassName = "choice-value" }, Text(selected)));
        children.Add(Icon(Icons.ChevronDown, "choice-chevron"));

        ElementNode node = (ElementNode)Button(new()
        {
            ClassName = "choice",
            Title = $"{label}: {selected}",
            OnClick = () =>
            {
                ElementNode? anchor = reference.Value;
                session.OpenChoiceMenu(
                    label,
                    icon,
                    value,
                    list,
                    onChange,
                    anchor?.PositionX ?? 0f,
                    (anchor?.PositionY ?? 0f) + (anchor?.Height ?? 48f) + 5f,
                    MathF.Max(300f, anchor?.Width ?? 242f));
            },
        }, children.ToArray());
        node.BindRef(reference);
        return node;
    }

    /// <summary>A compact stage-level heading.</summary>
    public static Node SectionHead(string eyebrow, string title, string note = "") =>
        Div(new() { ClassName = "section-head" },
            Div(new() { ClassName = "section-head-copy" },
                Span(new() { ClassName = "section-eyebrow" }, Text(eyebrow)),
                Span(new() { ClassName = "section-title" }, Text(title))),
            note.Length > 0 ? Span(new() { ClassName = "section-note" }, Text(note)) : null);

    /// <summary>A small status dot and label.</summary>
    public static Node StatusDot(bool on, string label, string extra = "")
    {
        List<string> classes = ["status-dot"];
        if (on)
        {
            classes.Add("is-on");
        }

        return Span(new() { ClassName = string.Join(' ', classes) + (extra.Length > 0 ? " " + extra : "") },
            Span(new() { ClassName = "dot" }),
            Span(new() { ClassName = "dot-label" }, Text(label)));
    }

    /// <summary>A modal layer that closes when its backdrop is clicked.</summary>
    public static Node Modal(string className, Action close, params Node[] children)
    {
        return Div(new()
        {
            ClassName = "modal-backdrop",
            OnClick = close,
        },
        Div(new()
        {
            ClassName = "modal " + className,
            OnClick = () => { },
        }, children));
    }

    /// <summary>A modal title row with a close button.</summary>
    public static Node ModalTitle(string icon, string title, string subtitle, Action close) =>
        Div(new() { ClassName = "modal-head" },
            Div(new() { ClassName = "modal-mark" }, Icon(icon)),
            Div(new() { ClassName = "modal-copy" },
                Span(new() { ClassName = "modal-title" }, Text(title)),
                Span(new() { ClassName = "modal-subtitle" }, Text(subtitle))),
            IconButton(Icons.Close, "Close", close, className: "icon-btn modal-close"));

    /// <summary>A labelled readout row.</summary>
    public static Node Readout(string label, string value) =>
        Div(new() { ClassName = "readout" },
            Span(new() { ClassName = "readout-label" }, Text(label)),
            Span(new() { ClassName = "readout-value" }, Text(value)));

    /// <summary>Builds canvas width/height attributes.</summary>
    public static Dictionary<string, string> Size(int width, int height) => new()
    {
        ["width"] = width.ToString(CultureInfo.InvariantCulture),
        ["height"] = height.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>An inline hint line.</summary>
    public static Node Hint(string text) => Div(new() { ClassName = "hint" }, Text(text));
}
