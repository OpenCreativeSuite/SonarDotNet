namespace SonarDotNet.Core.Dsp;

/// <summary>A gentle output limiter using a fast peak follower and soft saturation.</summary>
public sealed class Limiter
{
    private const float CeilingDb = -1f;
    private float _gain = 1f;

    /// <summary>Processes one sample.</summary>
    public float Process(float sample, int sampleRate, bool enabled)
    {
        if (!enabled)
        {
            _gain = 1f;
            return sample;
        }

        float ceiling = MathF.Pow(10f, CeilingDb / 20f);
        float magnitude = MathF.Abs(sample);
        float target = magnitude > ceiling ? ceiling / magnitude : 1f;
        float coefficient = target < _gain
            ? 0.55f
            : 1f - MathF.Exp(-1f / MathF.Max(1f, sampleRate * 0.060f));
        _gain += (target - _gain) * coefficient;
        float limited = sample * _gain;

        // The gain follower catches sustained peaks; the soft knee removes the
        // last fraction of a decibel from fast transients without hard clipping.
        if (limited > ceiling)
        {
            limited = ceiling + ((1f - MathF.Exp(-(limited - ceiling) * 8f)) * 0.08f);
        }
        else if (limited < -ceiling)
        {
            limited = -ceiling - ((1f - MathF.Exp(-(-limited - ceiling) * 8f)) * 0.08f);
        }

        return Math.Clamp(limited, -1f, 1f);
    }

    /// <summary>Current limiter gain, 0..1.</summary>
    public float Gain => _gain;
}
