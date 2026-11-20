using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>The processed waveform and the input/output level meters.</summary>
internal sealed class LiveScope : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "scope-canvas",
            Attributes = Ui.Size(280, 128),
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
        float width = MathF.Max(40f, canvas?.Width ?? 280f);
        float height = MathF.Max(40f, canvas?.Height ?? 128f);
        context.Clear(0xFF0B0F14);

        for (int i = 1; i < 4; i++)
        {
            float y = height * i / 4f;
            context.FillRect(0f, y, width, 1f, 0x09FFFFFF);
        }

        for (int i = 1; i < 6; i++)
        {
            float x = width * i / 6f;
            context.FillRect(x, 0f, 1f, height, 0x07FFFFFF);
        }

        float mid = height * 0.5f;
        context.FillRect(0f, mid, width, 1f, 0x28FFFFFF);

        float[] waveform = session.Telemetry.Snapshot();
        int count = waveform.Length;
        if (count == 0)
        {
            return;
        }

        float step = width / count;
        for (int i = 0; i < count; i++)
        {
            float x = i * step;
            float amplitude = Math.Clamp(waveform[i], -1f, 1f) * (height * 0.42f);
            float y = mid - amplitude;
            float bar = MathF.Max(1.2f, MathF.Abs(amplitude) * 1.7f);
            context.FillRect(x, MathF.Min(y, mid), MathF.Max(1f, step * 0.8f), bar, 0x1859E3B2);
            context.FillRect(x, y, MathF.Max(1f, step * 0.65f), 1.4f, 0xFF59E3B2);
            context.FillRect(x, mid - (amplitude * 0.7f), MathF.Max(1f, step * 0.65f), 1f, 0x807AA2FF);
        }

        if (!session.IsLive)
        {
            context.FillRect(0f, 0f, width, height, 0xB00B0F14);
            context.DrawText(new Rect(14f, mid - 7f, width - 28f, 14f), "Waiting for the microphone", Graph.Label);
        }
    }
}

/// <summary>Input and processed output levels with clip holds.</summary>
internal sealed class LevelMeters : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        StudioSession session = UseContext(SessionContext.Value);
        Ref<ElementNode> canvasRef = UseRef<ElementNode>();
        Node node = Canvas(new CanvasProps
        {
            ClassName = "meter-canvas",
            Attributes = Ui.Size(280, 104),
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
        float width = MathF.Max(80f, canvas?.Width ?? 280f);
        float height = MathF.Max(60f, canvas?.Height ?? 104f);
        context.Clear(0xFF0B0F14);

        float barLeft = 52f;
        float barWidth = MathF.Max(20f, width - barLeft - 74f);
        Row(context, 17f, "INPUT", session.Telemetry.InputPeak, session.Telemetry.InputClipped, barLeft, barWidth, Graph.Output);
        Row(context, 51f, "OUTPUT", session.Telemetry.OutputPeak, session.Telemetry.OutputClipped, barLeft, barWidth, Graph.Accent);

        float reduction = Math.Clamp(session.Telemetry.GainReductionDb / 18f, 0f, 1f);
        context.DrawText(new Rect(13f, 83f, 40f, 12f), "GAIN", Graph.Small);
        context.FillRect(barLeft, 86f, barWidth, 5f, 0x1AFFFFFF);
        context.FillRect(barLeft, 86f, barWidth * reduction, 5f, Graph.Warm);
        context.DrawText(new Rect(barLeft + barWidth + 8f, 82f, 52f, 12f), $"{session.Telemetry.GainReductionDb:0.0} dB", Graph.Small);

        // Decibel ticks.
        for (int db = -48; db <= 0; db += 12)
        {
            float unit = (db + 60f) / 60f;
            float x = barLeft + (barWidth * unit);
            context.FillRect(x, 6f, 1f, 72f, 0x07FFFFFF);
        }

        if (!session.IsLive)
        {
            context.DrawText(new Rect(barLeft, 32f, 130f, 12f), "No signal", Graph.Small);
        }

        if (height < 80f)
        {
            context.Clear(0xFF0B0F14);
        }
    }

    private static void Row(
        CanvasContext context,
        float y,
        string label,
        float peak,
        bool clipped,
        float left,
        float width,
        uint colour)
    {
        context.DrawText(new Rect(13f, y - 6f, 44f, 12f), label, Graph.Small);
        context.FillRect(left, y, width, 9f, 0x1AFFFFFF);
        float level = Math.Clamp((20f * MathF.Log10(MathF.Max(1e-5f, peak)) + 60f) / 60f, 0f, 1f);
        float fill = width * level;
        for (float x = 0f; x < fill; x += 4f)
        {
            uint segment = level > 0.88f ? Graph.Danger : level > 0.72f ? Graph.Warm : colour;
            context.FillRect(left + x, y, MathF.Min(3f, fill - x), 9f, segment);
        }

        context.FillEllipse(new Rect(left + width + 9f, y + 0.5f, 8f, 8f), clipped ? Graph.Danger : 0xFF222A33);
        context.DrawText(new Rect(left + width + 23f, y - 4f, 55f, 12f), peak > 0.0001f ? $"{20f * MathF.Log10(peak):0.0}" : "−∞", Graph.Small);
    }
}
