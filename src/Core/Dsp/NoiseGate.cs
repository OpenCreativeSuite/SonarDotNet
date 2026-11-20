namespace SonarDotNet.Core.Dsp;

/// <summary>A smooth downward gate with an optional gentle expander.</summary>
public sealed class NoiseGate
{
    private NoiseSettings _settings = new();
    private float _envelope;
    private float _gain = 1f;

    /// <summary>Updates the gate parameters.</summary>
    public void Configure(NoiseSettings settings, int sampleRate)
    {
        _settings = settings;
    }

    /// <summary>Processes one sample.</summary>
    public float Process(float sample, int sampleRate)
    {
        if (!_settings.Enabled)
        {
            _gain = 1f;
            return sample;
        }

        float magnitude = MathF.Abs(sample);
        float envelopeCoefficient = magnitude > _envelope
            ? Coefficient(4f, sampleRate)
            : Coefficient(120f + (_settings.Smoothness * 220f), sampleRate);
        _envelope += (magnitude - _envelope) * envelopeCoefficient;

        float threshold = DbToLinear(_settings.ThresholdDb);
        float target = _envelope < threshold ? DbToLinear(-Math.Clamp(_settings.ReductionDb, 0f, 72f)) : 1f;

        if (_settings.Suppress && _envelope < threshold && threshold > 0.0001f)
        {
            float ratio = Math.Clamp(_envelope / threshold, 0f, 1f);
            target *= 0.72f + (0.28f * ratio);
        }

        float open = Coefficient(3.5f + (_settings.Smoothness * 12f), sampleRate);
        float close = Coefficient(70f + (_settings.Smoothness * 220f), sampleRate);
        _gain += (target - _gain) * (target > _gain ? open : close);
        return sample * _gain;
    }

    /// <summary>Returns the current gate gain, 0..1.</summary>
    public float Gain => _gain;

    /// <summary>Converts decibels to a linear multiplier.</summary>
    public static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    /// <summary>Converts a linear value to decibels.</summary>
    public static float LinearToDb(float value) => 20f * MathF.Log10(MathF.Max(1e-7f, value));

    private static float Coefficient(float milliseconds, int sampleRate) =>
        1f - MathF.Exp(-1f / MathF.Max(1f, sampleRate * milliseconds * 0.001f));
}
