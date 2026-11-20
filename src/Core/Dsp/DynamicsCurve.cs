namespace SonarDotNet.Core.Dsp;

/// <summary>Shared calculations for the compressor curve and its graph.</summary>
public static class DynamicsCurve
{
    /// <summary>The soft-knee width in decibels.</summary>
    public const float KneeDb = 6f;

    /// <summary>The lowest input level drawn on the dynamics graph.</summary>
    public const float MinDb = -60f;

    /// <summary>Evaluates the compressor's static transfer curve.</summary>
    public static float Evaluate(float inputDb, float thresholdDb, float ratio, float makeupDb)
    {
        float above = inputDb - thresholdDb;
        float compressed;
        if (above <= -(KneeDb * 0.5f))
        {
            compressed = 0f;
        }
        else if (above >= KneeDb * 0.5f)
        {
            compressed = above * (1f - (1f / MathF.Max(1f, ratio)));
        }
        else
        {
            float kneePosition = above + (KneeDb * 0.5f);
            float kneeCurve = (kneePosition * kneePosition) / (2f * KneeDb);
            compressed = kneeCurve * (1f - (1f / MathF.Max(1f, ratio)));
        }

        return inputDb - compressed + makeupDb;
    }

    /// <summary>Returns the amount of gain reduction at an input level.</summary>
    public static float Reduction(float inputDb, float thresholdDb, float ratio)
    {
        float above = inputDb - thresholdDb;
        if (above <= 0f)
        {
            return 0f;
        }

        return above * (1f - (1f / MathF.Max(1f, ratio)));
    }
}
