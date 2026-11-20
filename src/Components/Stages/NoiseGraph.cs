using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using SonarDotNet.Core.Dsp;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>The noise-gate transfer curve with a live input marker.</summary>
internal sealed class NoiseGraph : Component
{
    private int _drag;

    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "graph-canvas noise-graph",
            Attributes = Ui.Size(920, 330),
            OnDraw = context => Draw(context, session, canvasRef.Value),
            OnPointerDown = (x, y) => Begin(session, canvasRef.Value, x, y),
            OnPointerMove = (x, y) => Move(session, canvasRef.Value, x, y),
            OnPointerUp = (x, y) => _drag = 0,
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
        float thresholdX = Graph.DbX(plot, session.Settings.Noise.ThresholdDb);
        if (MathF.Abs(x - thresholdX) < 46f)
        {
            _drag = 1;
            Move(session, canvas, x, y);
        }
    }

    private void Move(StudioSession session, ElementNode? canvas, float x, float y)
    {
        if (_drag == 0)
        {
            return;
        }

        Rect plot = Graph.Plot(canvas);
        float threshold = Graph.XDb(plot, x);
        float output = Graph.YDb(plot, y, -60f, 0f);
        float reduction = Math.Clamp(threshold - output, 0f, 60f);
        session.UpdateNoise(noise => noise with
        {
            ThresholdDb = Math.Clamp(threshold, -70f, -12f),
            ReductionDb = reduction,
        });
    }

    private void Draw(CanvasContext context, StudioSession session, ElementNode? canvas)
    {
        Rect plot = Graph.Plot(canvas);
        Graph.Frame(context, plot, 7, 6);
        NoiseSettings noise = session.Settings.Noise;

        for (int db = -60; db <= 0; db += 10)
        {
            float x = Graph.DbX(plot, db);
            float y = Graph.DbY(plot, db, -60f, 0f);
            Graph.DashedVertical(context, x, plot.Y, plot.Height, 0x09FFFFFF);
            context.DrawText(new Rect(x - 12f, plot.Bottom + 7f, 30f, 12f), db == 0 ? "0" : db.ToString(), Graph.Small);
            context.DrawText(new Rect(plot.X - 28f, y - 6f, 24f, 12f), db == 0 ? "0" : db.ToString(), Graph.Small);
        }

        context.DrawText(new Rect(plot.X, plot.Bottom + 20f, 140f, 12f), "INPUT LEVEL  dBFS", Graph.Small);
        context.DrawText(new Rect(plot.X - 36f, plot.Y - 16f, 80f, 12f), "OUTPUT", Graph.Small);

        // Gate transfer curve.
        Graph.Curve(context, plot, x =>
        {
            float input = Graph.XDb(plot, x);
            float target = input < noise.ThresholdDb ? input - noise.ReductionDb : input;
            return Graph.DbY(plot, target, -60f, 0f);
        }, noise.Enabled ? Graph.Accent : Graph.Faint, 1.8f);

        // The open half is the useful part of the curve, so give it a soft wash.
        for (float x = plot.X; x <= plot.Right; x += 2f)
        {
            float input = Graph.XDb(plot, x);
            if (input < noise.ThresholdDb)
            {
                continue;
            }

            float y = Graph.DbY(plot, input, -60f, 0f);
            context.FillRect(x, y, 2f, plot.Bottom - y, 0x1059E3B2);
        }

        float thresholdX = Graph.DbX(plot, noise.ThresholdDb);
        Graph.DashedVertical(context, thresholdX, plot.Y, plot.Height, 0x70FFB86B);
        context.DrawText(new Rect(thresholdX + 7f, plot.Y + 7f, 140f, 12f), $"THRESHOLD  {noise.ThresholdDb:0} dB", Graph.Value);

        float nodeY = Graph.DbY(plot, noise.ThresholdDb - noise.ReductionDb, -60f, 0f);
        Graph.Node(context, thresholdX, nodeY, noise.Enabled ? Graph.Warm : Graph.Faint, 5.5f, _drag == 1);
        context.DrawText(new Rect(plot.Right - 112f, plot.Bottom - 18f, 108f, 12f), $"{noise.ReductionDb:0} dB reduction", Graph.Small);

        // Live marker sits on the curve so the graph is useful while speaking.
        float inputDb = NoiseGate.LinearToDb(session.Telemetry.InputPeak);
        if (inputDb > -70f)
        {
            float liveX = Graph.DbX(plot, inputDb);
            float liveY = Graph.DbY(plot, inputDb < noise.ThresholdDb ? inputDb - noise.ReductionDb : inputDb, -60f, 0f);
            context.FillRect(liveX - 0.5f, plot.Y, 1f, plot.Height, 0x40FF6B5E);
            Graph.Node(context, liveX, liveY, Graph.Danger, 3f);
        }

        if (!noise.Enabled)
        {
            context.DrawText(new Rect(plot.X + 10f, plot.Y + 8f, 120f, 12f), "Stage bypassed", Graph.Label);
        }
    }
}
