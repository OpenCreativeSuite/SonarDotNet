using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using SonarDotNet.Core.Dsp;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>The compressor transfer curve with draggable threshold and ratio handles.</summary>
internal sealed class DynamicsGraph : Component
{
    private int _drag;

    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "graph-canvas dynamics-graph",
            Attributes = Ui.Size(920, 342),
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
        DynamicsSettings dynamics = session.Settings.Dynamics;
        float kneeX = Graph.DbX(plot, dynamics.ThresholdDb);
        float kneeY = Graph.DbY(plot, DynamicsCurve.Evaluate(dynamics.ThresholdDb, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
        float topX = Graph.DbX(plot, 0f);
        float topY = Graph.DbY(plot, DynamicsCurve.Evaluate(0f, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);

        float kneeDistance = MathF.Sqrt(((x - kneeX) * (x - kneeX)) + ((y - kneeY) * (y - kneeY)));
        float topDistance = MathF.Sqrt(((x - topX) * (x - topX)) + ((y - topY) * (y - topY)));
        _drag = kneeDistance <= topDistance && kneeDistance < 55f ? 1 : (topDistance < 55f ? 2 : 0);
        if (_drag > 0)
        {
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
        DynamicsSettings dynamics = session.Settings.Dynamics;
        if (_drag == 1)
        {
            float threshold = Math.Clamp(Graph.XDb(plot, x), -50f, -2f);
            float output = Math.Clamp(Graph.YDb(plot, y, -60f, 6f), -50f, 6f);
            float makeup = Math.Clamp(output - threshold, 0f, 18f);
            session.UpdateDynamics(current => current with { ThresholdDb = threshold, MakeupDb = makeup });
            return;
        }

        float topOutput = Math.Clamp(Graph.YDb(plot, y, -60f, 6f), -40f, 6f);
        float available = MathF.Max(0.5f, -dynamics.ThresholdDb);
        float ratio = Math.Clamp(available / MathF.Max(0.5f, topOutput - dynamics.ThresholdDb - dynamics.MakeupDb), 1f, 12f);
        session.UpdateDynamics(current => current with { Ratio = ratio });
    }

    private void Draw(CanvasContext context, StudioSession session, ElementNode? canvas)
    {
        Rect plot = Graph.Plot(canvas);
        Graph.Frame(context, plot, 6, 6);
        DynamicsSettings dynamics = session.Settings.Dynamics;

        for (int db = -60; db <= 0; db += 10)
        {
            float position = Graph.DbX(plot, db);
            Graph.DashedVertical(context, position, plot.Y, plot.Height, 0x08FFFFFF);
            context.DrawText(new Rect(position - 12f, plot.Bottom + 7f, 28f, 12f), db == 0 ? "0" : db.ToString(), Graph.Small);
            float y = Graph.DbY(plot, db, -60f, 6f);
            context.DrawText(new Rect(plot.X - 28f, y - 6f, 24f, 12f), db == 0 ? "0" : db.ToString(), Graph.Small);
        }

        context.DrawText(new Rect(plot.X, plot.Bottom + 20f, 150f, 12f), "INPUT LEVEL  dBFS", Graph.Small);
        context.DrawText(new Rect(plot.X - 36f, plot.Y - 16f, 80f, 12f), "OUTPUT", Graph.Small);

        // Unity reference.
        Graph.Curve(context, plot, x => Graph.DbY(plot, Graph.XDb(plot, x), -60f, 6f), 0x22FFFFFF, 1f);

        // Compression curve.
        Graph.Curve(context, plot, x =>
        {
            float input = Graph.XDb(plot, x);
            return Graph.DbY(plot, DynamicsCurve.Evaluate(input, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
        }, dynamics.Enabled ? Graph.Accent : Graph.Faint, 2f);

        for (float x = plot.X; x <= plot.Right; x += 2f)
        {
            float input = Graph.XDb(plot, x);
            float y = Graph.DbY(plot, DynamicsCurve.Evaluate(input, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
            context.FillRect(x, y, 2f, plot.Bottom - y, 0x0C59E3B2);
        }

        float thresholdX = Graph.DbX(plot, dynamics.ThresholdDb);
        Graph.DashedVertical(context, thresholdX, plot.Y, plot.Height, 0x65FFB86B);
        context.DrawText(new Rect(thresholdX + 7f, plot.Y + 7f, 150f, 12f), $"KNEE  {dynamics.ThresholdDb:0} dB", Graph.Value);

        float kneeY = Graph.DbY(plot, DynamicsCurve.Evaluate(dynamics.ThresholdDb, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
        Graph.Node(context, thresholdX, kneeY, dynamics.Enabled ? Graph.Warm : Graph.Faint, 5f, _drag == 1);

        float topX = Graph.DbX(plot, 0f);
        float topY = Graph.DbY(plot, DynamicsCurve.Evaluate(0f, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
        Graph.Node(context, topX, topY, dynamics.Enabled ? Graph.Accent : Graph.Faint, 5f, _drag == 2);
        context.DrawText(new Rect(topX - 128f, topY - 22f, 120f, 12f), $"RATIO  {dynamics.Ratio:0.0}:1", Graph.Value);

        float inputDb = NoiseGate.LinearToDb(session.Telemetry.InputPeak);
        if (inputDb > -60f)
        {
            float liveX = Graph.DbX(plot, inputDb);
            float liveY = Graph.DbY(plot, DynamicsCurve.Evaluate(inputDb, dynamics.ThresholdDb, dynamics.Ratio, dynamics.MakeupDb), -60f, 6f);
            context.FillRect(liveX - 0.5f, plot.Y, 1f, plot.Height, 0x38FF6B5E);
            Graph.Node(context, liveX, liveY, Graph.Danger, 3f);
        }

        float reduction = session.Telemetry.GainReductionDb;
        context.DrawText(new Rect(plot.Right - 152f, plot.Y + 8f, 146f, 12f), $"GAIN REDUCTION  {reduction:0.0} dB", reduction > 0.2f ? Graph.Value : Graph.Small);
        if (!dynamics.Enabled)
        {
            context.DrawText(new Rect(plot.X + 10f, plot.Y + 8f, 120f, 12f), "Stage bypassed", Graph.Label);
        }
    }
}
