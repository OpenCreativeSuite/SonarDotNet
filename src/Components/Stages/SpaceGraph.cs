using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>A visual readout of the room decay and echo tap.</summary>
internal sealed class SpaceGraph : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "graph-canvas space-graph",
            Attributes = Ui.Size(920, 330),
            OnDraw = context => Draw(context, session, canvasRef.Value),
        });
        if (node is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return node;
    }

    private static void Draw(CanvasContext context, StudioSession session, ElementNode? canvas)
    {
        Rect plot = Graph.Plot(canvas, 48f, 24f, 24f, 32f);
        Graph.Frame(context, plot, 8, 4);
        CharacterSettings character = session.Settings.Character;

        context.DrawText(new Rect(plot.X, plot.Y - 16f, 220f, 12f), "ROOM DECAY & ECHO", Graph.Small);
        context.DrawText(new Rect(plot.X - 26f, plot.Bottom + 8f, 80f, 12f), "TIME", Graph.Small);

        float mid = plot.Y + (plot.Height * 0.48f);
        context.FillRect(plot.X, mid, plot.Width, 1f, 0x32FFFFFF);

        float room = Math.Clamp(character.Space / 100f, 0f, 1f);
        context.DrawText(new Rect(plot.X + 8f, plot.Y + 8f, 220f, 12f),
            character.SpaceEnabled ? "The voice enters a space" : "Space stage bypassed",
            Graph.Label);

        // Decay curve. A larger room produces a longer, softer tail.
        float length = 0.34f + (room * 0.58f);
        for (float x = 0f; x <= plot.Width * length; x += 1.5f)
        {
            float unit = x / (plot.Width * length);
            float amplitude = MathF.Exp(-3.6f * unit) * (1f - (unit * 0.25f));
            float y = mid - (amplitude * plot.Height * 0.34f);
            context.FillRect(plot.X + x, y, 1.6f, 2f, character.SpaceEnabled ? Graph.Accent : Graph.Faint);
            context.FillRect(plot.X + x, mid, 1.6f, amplitude * plot.Height * 0.13f, 0x1659E3B2);
        }

        // Echo tap.
        float delayUnit = Math.Clamp((character.DelayMs - 80f) / 370f, 0f, 1f);
        float echoX = plot.X + (plot.Width * (0.18f + delayUnit * 0.72f));
        float echoMix = Math.Clamp(character.DelayMix, 0f, 1f);
        if (echoMix > 0.001f)
        {
            Graph.DashedVertical(context, echoX, plot.Y, plot.Height, 0x55FFB86B);
            float height = plot.Height * (0.12f + echoMix * 0.36f);
            context.FillRect(echoX - 2f, mid - height, 4f, height * 2f, 0x80FFB86B);
            Graph.Node(context, echoX, mid - height, Graph.Warm, 4f);
            context.DrawText(new Rect(echoX - 30f, mid + 12f, 90f, 12f), $"{character.DelayMs:0} ms", Graph.Value);
        }

        // Reflection ticks make the size of the room legible.
        int reflections = 3 + (int)(room * 7f);
        for (int i = 0; i < reflections; i++)
        {
            float unit = 0.08f + (i * 0.075f);
            if (unit > 0.95f)
            {
                break;
            }

            float x = plot.X + (plot.Width * unit);
            float echoHeight = (1f - unit) * (8f + room * 26f);
            context.FillRect(x, mid - echoHeight, 1f, echoHeight * 2f, 0x2CFFFFFF);
        }

        context.DrawText(new Rect(plot.Right - 150f, plot.Bottom + 8f, 146f, 12f),
            $"{character.SpaceMix * 100f:0}% room · {character.DelayMix * 100f:0}% echo",
            Graph.Value);
    }
}
