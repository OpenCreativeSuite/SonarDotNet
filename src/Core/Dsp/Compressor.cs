namespace SonarDotNet.Core.Dsp;

/// <summary>A feed-forward peak compressor with a soft knee.</summary>
public sealed class Compressor
{
    private DynamicsSettings _settings = new();
    private float _envelope;
    private float _reductionDb;
    private float _attack;
    private float _release;

    /// <summary>Updates the compressor parameters.</summary>
    public void Configure(DynamicsSettings settings, int sampleRate)
    {
        _settings = settings;
        _attack = Coefficient(settings.AttackMs, sampleRate);
        _release = Coefficient(settings.ReleaseMs, sampleRate);
        _reductionDb = 0f;
    }

    /// <summary>Processes one sample and returns it with gain reduction applied.</summary>
    public float Process(float sample)
    {
        if (!_settings.Enabled)
        {
            _reductionDb = 0f;
            return sample;
        }

        float magnitude = MathF.Abs(sample);
        float detector = magnitude > _envelope
            ? magnitude
            : _envelope + ((magnitude - _envelope) * _release);
        _envelope = MathF.Max(magnitude, detector);

        float levelDb = 20f * MathF.Log10(MathF.Max(1e-7f, _envelope));
        float target = DynamicsCurve.Reduction(levelDb, _settings.ThresholdDb, _settings.Ratio);
        float coefficient = target > _reductionDb ? _attack : _release;
        _reductionDb += (target - _reductionDb) * coefficient;

        float makeup = DbToLinear(_settings.MakeupDb);
        return sample * DbToLinear(-_reductionDb) * makeup;
    }

    /// <summary>Current gain reduction in positive decibels.</summary>
    public float ReductionDb => _reductionDb;

    private static float Coefficient(float milliseconds, int sampleRate) =>
        1f - MathF.Exp(-1f / MathF.Max(1f, sampleRate * MathF.Max(0.1f, milliseconds) * 0.001f));

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
}
