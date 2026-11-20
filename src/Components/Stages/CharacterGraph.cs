using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>The character stage: a waveshaper curve with a reverb halo.</summary>
internal sealed class CharacterGraph : Component
{
    private bool _drag;

    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "graph-canvas character-graph",
            Attributes = Ui.Size(920, 324),
            OnDraw = context => Draw(context, session, canvasRef.Value),
            OnPointerDown = (x, y) =>
            {
                _drag = true;
                Move(session, canvasRef.Value, x, y);
            },
            OnPointerMove = (x, y) => Move(session, canvasRef.Value, x, y),
            OnPointerUp = (x, y) => _drag = false,
        });
        if (node is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return node;
    }

    private void Move(StudioSession session, ElementNode? canvas, float x, float y)
    {
        if (!_drag)
        {
            return;
        }

        Rect plot = Graph.Plot(canvas);
        float warmth = Math.Clamp((x - plot.X) / plot.Width, 0f, 1f) * 100f;
        session.UpdateCharacter(settings => settings with { Warmth = MathF.Round(warmth) });
    }

    private void Draw(CanvasContext context, StudioSession session, ElementNode? canvas)
    {
        Rect plot = Graph.Plot(canvas, 42f, 28f, 24f, 30f);
        Graph.Frame(context, plot, 4, 4);
        CharacterSettings character = session.Settings.Character;
        float warmth = Math.Clamp(character.Warmth / 100f, 0f, 1f);

        float zeroX = plot.X + (plot.Width * 0.5f);
        float zeroY = plot.Y + (plot.Height * 0.5f);
        context.FillRect(plot.X, zeroY, plot.Width, 1f, 0x28FFFFFF);
        context.FillRect(zeroX, plot.Y, 1f, plot.Height, 0x28FFFFFF);

        context.DrawText(new Rect(plot.X - 30f, zeroY - 20f, 46f, 12f), "+1", Graph.Small);
        context.DrawText(new Rect(plot.X - 30f, plot.Bottom - 8f, 46f, 12f), "−1", Graph.Small);
        context.DrawText(new Rect(zeroX - 8f, plot.Bottom + 7f, 30f, 12f), "0", Graph.Small);
        context.DrawText(new Rect(plot.Right - 12f, plot.Bottom + 7f, 30f, 12f), "+1", Graph.Small);

        // Dry identity is a quiet reference; the warm curve is the instrument.
        Graph.Curve(context, plot, x => Graph.Y(plot, 1f - ((x - plot.X) / plot.Width)), 0x20FFFFFF, 1f);

        float drive = 1f + (warmth * 8f);
        Graph.Curve(context, plot, x =>
        {
            float input = ((x - plot.X) / plot.Width * 2f) - 1f;
            float shaped = MathF.Tanh(input * drive) / MathF.Tanh(drive);
            return Graph.Y(plot, (1f - shaped) * 0.5f);
        }, character.Enabled ? Graph.Warm : Graph.Faint, 2f);

        float handleX = Graph.X(plot, warmth);
        float handleY = Graph.Y(plot, (1f - (MathF.Tanh(warmth * drive) / MathF.Tanh(drive))) * 0.5f);
        Graph.Node(context, handleX, handleY, character.Enabled ? Graph.Warm : Graph.Faint, 5.5f, _drag);

        context.DrawText(new Rect(plot.X + 8f, plot.Y + 8f, 250f, 12f), "Drag to add harmonic weight", Graph.Small);
        context.DrawText(
            new Rect(plot.Right - 238f, plot.Y + 8f, 230f, 12f),
            $"PITCH {Ui.Semitones(character.PitchSemitones)}  FORMANT {Ui.Semitones(character.FormantSemitones)}",
            Graph.Value);
        context.DrawText(
            new Rect(plot.X + 8f, plot.Y + 26f, 300f, 12f),
            $"Resonance {character.Resonance * 50f + 50f:0}%  ·  Brightness {character.Brightness * 50f + 50f:0}%  ·  Texture {character.Warmth:0}%",
            Graph.Small);

        if (!character.Enabled)
        {
            context.DrawText(new Rect(plot.X + 8f, plot.Y + 26f, 120f, 12f), "Stage bypassed", Graph.Label);
        }
    }
}
