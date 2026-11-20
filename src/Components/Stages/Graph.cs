using Donkeh.LayoutLab;
using Donkeh.UILab;
using Donkeh.Windowing;
using SonarDotNet.Core;
using SonarDotNet.Core.Dsp;
using static Donkeh.UILab.HtmlHelper;

namespace SonarDotNet.Components;

/// <summary>Palette and drawing primitives shared by every graph.</summary>
internal static class Graph
{
    public const uint Background = 0xFF0E1217;
    public const uint Grid = 0x12FFFFFF;
    public const uint GridStrong = 0x22FFFFFF;
    public const uint Axis = 0x38FFFFFF;
    public const uint Accent = 0xFF59E3B2;
    public const uint AccentSoft = 0x2959E3B2;
    public const uint AccentDim = 0x8059E3B2;
    public const uint Warm = 0xFFFFB86B;
    public const uint WarmSoft = 0x22FFB86B;
    public const uint Output = 0xFF7AA2FF;
    public const uint Muted = 0xFF7C8B9C;
    public const uint Faint = 0xFF4D5A68;
    public const uint Danger = 0xFFFF6B5E;

    public static readonly TextStyle Label = new(
        "Segoe UI",
        9f,
        500,
        false,
        0xFF7A8898,
        0f,
        12f,
        TextAlign.Left,
        TextDecoration.None,
        WhiteSpace.NoWrap,
        Direction.LeftToRight);

    public static readonly TextStyle Small = new(
        "Segoe UI",
        8f,
        500,
        false,
        0xFF5C6978,
        0f,
        10f,
        TextAlign.Left,
        TextDecoration.None,
        WhiteSpace.NoWrap,
        Direction.LeftToRight);

    public static readonly TextStyle Value = new(
        "Segoe UI",
        9.5f,
        600,
        false,
        0xFFE7ECF2,
        0f,
        12f,
        TextAlign.Left,
        TextDecoration.None,
        WhiteSpace.NoWrap,
        Direction.LeftToRight);

    /// <summary>A plot rectangle with room for axis labels.</summary>
    public static Rect Plot(ElementNode? canvas, float left = 48f, float right = 22f, float top = 20f, float bottom = 30f)
    {
        float width = MathF.Max(20f, canvas?.Width ?? 640f);
        float height = MathF.Max(20f, canvas?.Height ?? 320f);
        return new Rect(left, top, MathF.Max(10f, width - left - right), MathF.Max(10f, height - top - bottom));
    }

    /// <summary>Draws the graph backdrop and a fine grid.</summary>
    public static void Frame(CanvasContext context, Rect plot, int verticalLines = 8, int horizontalLines = 6)
    {
        context.Clear(Background);
        for (int i = 0; i <= verticalLines; i++)
        {
            float x = plot.X + (plot.Width * i / verticalLines);
            context.FillRect(x, plot.Y, 1f, plot.Height, i == 0 || i == verticalLines ? Axis : Grid);
        }

        for (int i = 0; i <= horizontalLines; i++)
        {
            float y = plot.Y + (plot.Height * i / horizontalLines);
            context.FillRect(plot.X, y, plot.Width, 1f, i == 0 || i == horizontalLines ? Axis : Grid);
        }
    }

    /// <summary>Draws a point with a dark rim and a bright centre.</summary>
    public static void Node(CanvasContext context, float x, float y, uint colour, float radius = 4.5f, bool selected = false)
    {
        if (selected)
        {
            context.FillEllipse(new Rect(x - 13f, y - 13f, 26f, 26f), 0x2459E3B2);
        }

        context.FillEllipse(new Rect(x - radius - 2.5f, y - radius - 2.5f, (radius + 2.5f) * 2f, (radius + 2.5f) * 2f), 0xFF0A0D11);
        context.FillEllipse(new Rect(x - radius, y - radius, radius * 2f, radius * 2f), colour);
        context.FillEllipse(new Rect(x - 1.5f, y - 1.5f, 3f, 3f), 0xFFFFFFFF);
    }

    /// <summary>Draws a dashed horizontal line made of short rectangles.</summary>
    public static void DashedHorizontal(CanvasContext context, float x, float y, float width, uint colour)
    {
        for (float cursor = 0f; cursor < width; cursor += 7f)
        {
            context.FillRect(x + cursor, y, MathF.Min(3f, width - cursor), 1f, colour);
        }
    }

    /// <summary>Draws a dashed vertical line.</summary>
    public static void DashedVertical(CanvasContext context, float x, float y, float height, uint colour)
    {
        for (float cursor = 0f; cursor < height; cursor += 7f)
        {
            context.FillRect(x, y + cursor, 1f, MathF.Min(3f, height - cursor), colour);
        }
    }

    /// <summary>Draws a 1-pixel polyline by plotting short vertical spans.</summary>
    public static void Curve(CanvasContext context, Rect plot, Func<float, float> yAt, uint colour, float width = 1.6f)
    {
        float previous = yAt(plot.X);
        for (float x = plot.X; x <= plot.Right; x += width)
        {
            float y = yAt(x);
            float top = MathF.Min(previous, y);
            float span = MathF.Max(1f, MathF.Abs(previous - y) + 0.6f);
            context.FillRect(x, top, width, span, colour);
            previous = y;
        }
    }

    /// <summary>Clamps a value to a plot rectangle.</summary>
    public static float X(Rect plot, float unit) => plot.X + (plot.Width * Math.Clamp(unit, 0f, 1f));

    /// <summary>Clamps a value to a plot rectangle.</summary>
    public static float Y(Rect plot, float unit) => plot.Y + (plot.Height * Math.Clamp(unit, 0f, 1f));

    /// <summary>Maps a decibel level onto a vertical scale, top to bottom.</summary>
    public static float DbY(Rect plot, float db, float minDb, float maxDb) =>
        Y(plot, (maxDb - Math.Clamp(db, minDb, maxDb)) / (maxDb - minDb));

    /// <summary>Maps a vertical position back to decibels.</summary>
    public static float YDb(Rect plot, float y, float minDb, float maxDb) =>
        maxDb - (Math.Clamp((y - plot.Y) / plot.Height, 0f, 1f) * (maxDb - minDb));

    /// <summary>Maps an input level to a horizontal graph position.</summary>
    public static float DbX(Rect plot, float db, float minDb = -60f, float maxDb = 0f) =>
        X(plot, (Math.Clamp(db, minDb, maxDb) - minDb) / (maxDb - minDb));

    /// <summary>Maps a horizontal graph position back to decibels.</summary>
    public static float XDb(Rect plot, float x, float minDb = -60f, float maxDb = 0f) =>
        minDb + (Math.Clamp((x - plot.X) / plot.Width, 0f, 1f) * (maxDb - minDb));
}
