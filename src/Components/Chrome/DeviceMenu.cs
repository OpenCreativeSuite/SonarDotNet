using Donkeh.UILab;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Components;

/// <summary>The dark, anchored list used by every device picker.</summary>
internal sealed class DeviceMenu : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        ChoiceMenuState? menu = session.OpenChoice;
        if (menu is null)
        {
            return F();
        }

        return Div(new()
        {
            ClassName = "choice-layer",
            OnClick = session.CloseChoice,
        },
        Div(new()
        {
            ClassName = "choice-menu",
            OnClick = () => { },
            Attributes = new()
            {
                ["style"] = $"left:{menu.X:0.#}px;top:{menu.Y:0.#}px;width:{menu.Width:0.#}px",
            },
        },
        Div(new() { ClassName = "choice-menu-head" },
            Div(new() { ClassName = "choice-menu-title-block" },
                Span(new() { ClassName = "choice-menu-kicker" }, Text("SELECT")),
                Span(new() { ClassName = "choice-menu-title" }, Text(menu.Title))),
            Span(new() { ClassName = "choice-menu-count" }, Text($"{menu.Options.Count}")),
            Ui.IconButton(Icons.Close, "Close list", session.CloseChoice, className: "icon-btn choice-menu-close")),
        Div(new() { ClassName = "choice-menu-list" },
            menu.Options.Count == 0
                ? Div(new() { ClassName = "choice-empty" }, Text("No devices found"))
                : F(menu.Options.Select(option => (Node)OptionRow(session, menu, option)).ToArray()))));
    }

    private static Node OptionRow(StudioSession session, ChoiceMenuState menu, ChoiceOption option)
    {
        bool selected = option.Value == menu.Selected;
        return Button(new()
        {
            ClassName = selected ? "choice-option is-selected" : "choice-option",
            Title = option.Label,
            OnClick = () => session.SelectChoice(option.Value),
        },
        Span(new() { ClassName = "choice-option-check" },
            selected ? Ui.Icon(Icons.Check) : null),
        Div(new() { ClassName = "choice-option-copy" },
            Span(new() { ClassName = "choice-option-label" }, Text(option.Label)),
            option.Detail.Length > 0 ? Span(new() { ClassName = "choice-option-detail" }, Text(option.Detail)) : null));
    }
}
