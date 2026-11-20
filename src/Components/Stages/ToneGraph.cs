using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using SonarDotNet.Core.Dsp;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>The interactive frequency-response editor.</summary>
internal sealed class ToneGraph : Component
{
    private int _drag = -1;

    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "graph-canvas tone-graph",
            Attributes = Ui.Size(920, 410),
            OnDraw = context => Draw(context, session, canvasRef.Value),
            OnPointerDown = (x, y) => Begin(session, canvasRef.Value, x, y),
            OnPointerMove = (x, y) => Move(session, canvasRef.Value, x, y),
            OnPointerUp = (x, y) => _drag = -1,
        });
        if (node is ElementNode element)
        {
            element.BindRef(canvasRef);
        }

        return node;
    }

    private void Begin(StudioSession session, ElementNode? canvas, float x, float y)
    {
        Rect plot = Graph.Plot(canvas);
        int closest = -1;
        float distance = 32f;
        for (int i = 0; i < session.Settings.Tone.Bands.Length; i++)
        {
            ToneBand band = session.Settings.Tone.Bands[i];
            float px = Graph.X(plot, ToneCurve.FrequencyToUnit(band.Frequency));
            float py = Graph.Y(plot, ToneCurve.GainToUnit(band.GainDb));
            float current = MathF.Sqrt(((x - px) * (x - px)) + ((y - py) * (y - py)));
            if (current < distance)
            {
                distance = current;
                closest = i;
            }
        }

        if (closest >= 0)
        {
            _drag = closest;
            Move(session, canvas, x, y);
        }
    }

    private void Move(StudioSession session, ElementNode? canvas, float x, float y)
    {
        if (_drag < 0)
        {
            return;
        }

        Rect plot = Graph.Plot(canvas);
        float unitX = Math.Clamp((x - plot.X) / plot.Width, 0f, 1f);
        float unitY = Math.Clamp((y - plot.Y) / plot.Height, 0f, 1f);
        session.MoveToneBand(_drag, ToneCurve.UnitToFrequency(unitX), ToneCurve.UnitToGain(unitY));
    }

    private static void Draw(CanvasContext context, StudioSession session, ElementNode? canvas)
    {
        Rect plot = Graph.Plot(canvas);
        Graph.Frame(context, plot, 9, 6);
        ToneSettings tone = session.Settings.Tone;

        float zero = Graph.Y(plot, 0.5f);
        context.FillRect(plot.X, zero, plot.Width, 1f, 0x36FFFFFF);

        // Frequency guides.
        float[] guides = [50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f];
        foreach (float frequency in guides)
        {
            float x = Graph.X(plot, ToneCurve.FrequencyToUnit(frequency));
            Graph.DashedVertical(context, x, plot.Y, plot.Height, 0x12FFFFFF);
            string label = ToneCurve.FormatFrequency(frequency);
            float width = label.Length * 5.1f;
            context.DrawText(new Rect(x - (width * 0.5f), plot.Bottom + 7f, width + 4f, 12f), label, Graph.Small);
        }

        for (int db = -12; db <= 12; db += 6)
        {
            float y = Graph.Y(plot, ToneCurve.GainToUnit(db));
            string label = db > 0 ? $"+{db}" : db.ToString();
            context.DrawText(new Rect(plot.X - 27f, y - 6f, 24f, 12f), label, Graph.Small);
        }

        context.DrawText(new Rect(plot.X - 36f, plot.Y - 17f, 60f, 12f), "LEVEL", Graph.Small);

        // Cut regions read as part of the curve, not as separate controls.
        float lowCutX = Graph.X(plot, ToneCurve.FrequencyToUnit(tone.LowCutHz));
        float highCutX = Graph.X(plot, ToneCurve.FrequencyToUnit(tone.HighCutHz));
        context.FillRect(plot.X, plot.Y, MathF.Max(1f, lowCutX - plot.X), plot.Height, 0x12FF6B5E);
        context.FillRect(highCutX, plot.Y, MathF.Max(1f, plot.Right - highCutX), plot.Height, 0x12FF6B5E);
        Graph.DashedVertical(context, lowCutX, plot.Y, plot.Height, 0x55FF6B5E);
        Graph.DashedVertical(context, highCutX, plot.Y, plot.Height, 0x55FF6B5E);

        // Filled response.
        float previousY = zero;
        for (float x = plot.X; x <= plot.Right; x += 1f)
        {
            float frequency = ToneCurve.UnitToFrequency((x - plot.X) / plot.Width);
            float db = (float)ToneCurve.ResponseDb(tone, frequency);
            float y = Graph.Y(plot, ToneCurve.GainToUnit(db));
            if (y < zero)
            {
                context.FillRect(x, y, 1.3f, zero - y, 0x1859E3B2);
            }
            else
            {
                context.FillRect(x, zero, 1.3f, y - zero, 0x0E59E3B2);
            }

            float top = MathF.Min(previousY, y);
            float span = MathF.Max(1.2f, MathF.Abs(previousY - y) + 0.8f);
            context.FillRect(x, top, 1.6f, span, Graph.Accent);
            previousY = y;
        }

        // Draggable points.
        for (int i = 0; i < tone.Bands.Length; i++)
        {
            ToneBand band = tone.Bands[i];
            float x = Graph.X(plot, ToneCurve.FrequencyToUnit(band.Frequency));
            float y = Graph.Y(plot, ToneCurve.GainToUnit(band.GainDb));
            bool active = i == 0 || i == tone.Bands.Length - 1;
            uint colour = !band.Enabled ? Graph.Faint : (active ? Graph.Warm : Graph.Accent);
            Graph.Node(context, x, y, colour, band.Enabled ? 5f : 3.5f);
            float labelWidth = band.Label.Length * 5.7f + 10f;
            context.DrawText(
                new Rect(x - (labelWidth * 0.5f), y - 27f, labelWidth, 13f),
                band.Label,
                band.Enabled ? Graph.Value : Graph.Label);
        }

        // Low/high cut handles are deliberately small; the graph remains the primary control.
        context.FillRect(lowCutX - 3f, plot.Bottom - 3f, 6f, 6f, Graph.Danger);
        context.FillRect(highCutX - 3f, plot.Bottom - 3f, 6f, 6f, Graph.Danger);
        context.DrawText(new Rect(plot.X + 7f, plot.Y + 8f, 220f, 12f), "Drag a point to shape the curve", Graph.Small);
        context.DrawText(new Rect(plot.Right - 86f, plot.Y + 8f, 80f, 12f), $"{Ui.Hz(tone.LowCutHz)} – {Ui.Hz(tone.HighCutHz)}", Graph.Small);
    }
}
