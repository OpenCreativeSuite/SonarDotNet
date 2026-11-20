namespace SonarDotNet.Core.Dsp;

/// <summary>Shared calculations for the voice tone curve and its graph.</summary>
public static class ToneCurve
{
    /// <summary>The lowest frequency drawn on the tone graph.</summary>
    public const float MinFrequency = 30f;

    /// <summary>The highest frequency drawn on the tone graph.</summary>
    public const float MaxFrequency = 20000f;

    /// <summary>The vertical range of the tone graph, in decibels.</summary>
    public const float RangeDb = 15f;

    /// <summary>Evaluates the combined response, in decibels.</summary>
    public static double ResponseDb(ToneSettings tone, float frequency, int sampleRate = 48000)
    {
        if (!tone.Enabled)
        {
            return 0.0;
        }

        double db = 0.0;
        Biquad filter = default;

        if (tone.LowCutHz > 20f)
        {
            filter.HighPass(tone.LowCutHz, 0.72f, sampleRate);
            db += filter.MagnitudeDb(frequency, sampleRate);
        }

        if (tone.HighCutHz < 20000f)
        {
            filter.LowPass(tone.HighCutHz, 0.72f, sampleRate);
            db += filter.MagnitudeDb(frequency, sampleRate);
        }

        foreach (ToneBand band in tone.Bands)
        {
            if (!band.Enabled || MathF.Abs(band.GainDb) < 0.01f)
            {
                continue;
            }

            switch (band.Kind)
            {
                case ToneBandKind.LowShelf:
                    filter.LowShelf(band.Frequency, band.GainDb, band.Q, sampleRate);
                    break;
                case ToneBandKind.HighShelf:
                    filter.HighShelf(band.Frequency, band.GainDb, band.Q, sampleRate);
                    break;
                default:
                    filter.Peaking(band.Frequency, band.GainDb, band.Q, sampleRate);
                    break;
            }

            db += filter.MagnitudeDb(frequency, sampleRate);
        }

        return db;
    }

    /// <summary>Maps a frequency to a 0..1 position on a logarithmic axis.</summary>
    public static float FrequencyToUnit(float frequency) =>
        (MathF.Log(Math.Clamp(frequency, MinFrequency, MaxFrequency)) - MathF.Log(MinFrequency)) /
        (MathF.Log(MaxFrequency) - MathF.Log(MinFrequency));

    /// <summary>Maps a 0..1 position back to a frequency.</summary>
    public static float UnitToFrequency(float unit) =>
        MathF.Exp(MathF.Log(MinFrequency) + (Math.Clamp(unit, 0f, 1f) * (MathF.Log(MaxFrequency) - MathF.Log(MinFrequency))));

    /// <summary>Maps a gain in decibels to a 0..1 position, top to bottom.</summary>
    public static float GainToUnit(float gainDb) =>
        0.5f - (Math.Clamp(gainDb, -RangeDb, RangeDb) / (RangeDb * 2f));

    /// <summary>Maps a 0..1 position back to a gain in decibels.</summary>
    public static float UnitToGain(float unit) =>
        Math.Clamp((0.5f - unit) * RangeDb * 2f, -RangeDb, RangeDb);

    /// <summary>Formats a frequency for a graph label.</summary>
    public static string FormatFrequency(float frequency) =>
        frequency >= 1000f ? $"{frequency / 1000f:0.#}k" : $"{frequency:0}";
}
