namespace SonarDotNet.Core.Dsp;

/// <summary>
/// The complete mono voice chain: input trim, noise, tone, dynamics and character.
/// </summary>
/// <remarks>
/// <para>
/// The processor is created once and reconfigured in place. No buffer, filter or
/// coefficient table is allocated while audio is flowing, which keeps the capture
/// callback predictable.
/// </para>
/// <para>
/// A settings snapshot is immutable, so the audio thread can read it without a
/// lock while the editor publishes a replacement.
/// </para>
/// </remarks>
public sealed class VoiceProcessor
{
    /// <summary>The internal processing sample rate.</summary>
    public const int SampleRate = 48000;

    private readonly NoiseGate _gate = new();
    private readonly Compressor _compressor = new();
    private readonly Limiter _limiter = new();
    private readonly CharacterProcessor _character;
    private Biquad _lowCut = new();
    private Biquad _highCut = new();
    private readonly Biquad[] _bands = new Biquad[4];

    private VoiceSettings _settings = new();
    private VoiceSettings? _configured;
    private float _gainReductionDb;

    /// <summary>Creates a processor at the engine sample rate.</summary>
    public VoiceProcessor() => _character = new CharacterProcessor(SampleRate);

    /// <summary>The latest configuration applied to the chain.</summary>
    public VoiceSettings Settings => _settings;

    /// <summary>Current compressor gain reduction, in positive decibels.</summary>
    public float GainReductionDb => _gainReductionDb;

    /// <summary>Applies a settings snapshot if it differs from the current one.</summary>
    public void Configure(VoiceSettings settings)
    {
        if (ReferenceEquals(_configured, settings))
        {
            return;
        }

        _settings = settings;
        _configured = settings;
        _gate.Configure(settings.Noise, SampleRate);
        _compressor.Configure(settings.Dynamics, SampleRate);
        _character.Configure(settings.Character);

        ToneSettings tone = settings.Tone;
        _lowCut.HighPass(tone.LowCutHz, 0.72f, SampleRate);
        _highCut.LowPass(tone.HighCutHz, 0.72f, SampleRate);
        for (int i = 0; i < _bands.Length; i++)
        {
            ToneBand band = i < tone.Bands.Length ? tone.Bands[i] : new ToneBand { GainDb = 0f };
            switch (band.Kind)
            {
                case ToneBandKind.LowShelf:
                    _bands[i].LowShelf(band.Frequency, band.GainDb, band.Q, SampleRate);
                    break;
                case ToneBandKind.HighShelf:
                    _bands[i].HighShelf(band.Frequency, band.GainDb, band.Q, SampleRate);
                    break;
                default:
                    _bands[i].Peaking(band.Frequency, band.GainDb, band.Q, SampleRate);
                    break;
            }
        }
    }

    /// <summary>Resets every stateful stage, which is useful after a device change.</summary>
    public void Reset()
    {
        _lowCut.Reset();
        _highCut.Reset();
        foreach (ref Biquad band in _bands.AsSpan())
        {
            band.Reset();
        }

        _configured = null;
        _gainReductionDb = 0f;
        _character.Reset();
    }

    /// <summary>Processes a mono block in place.</summary>
    public void Process(Span<float> samples)
    {
        float inputGain = MathF.Pow(10f, _settings.InputGainDb / 20f);
        bool toneEnabled = _settings.Tone.Enabled;
        bool dynamicsEnabled = _settings.Dynamics.Enabled;

        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i] * inputGain;

            sample = _gate.Process(sample, SampleRate);

            if (toneEnabled)
            {
                sample = _lowCut.Process(sample);
                for (int band = 0; band < _bands.Length; band++)
                {
                    sample = _bands[band].Process(sample);
                }

                sample = _highCut.Process(sample);
            }

            if (dynamicsEnabled)
            {
                sample = _compressor.Process(sample);
            }

            float limited = _limiter.Process(sample, SampleRate, _settings.Dynamics.Limiter);
            sample = _character.Process(limited);
            samples[i] = Math.Clamp(sample, -1f, 1f);
        }

        _gainReductionDb = _compressor.ReductionDb;
    }

    /// <summary>Convenience overload for offline tests and previews.</summary>
    public float[] Process(float[] samples)
    {
        float[] output = [.. samples];
        Process(output.AsSpan());
        return output;
    }
}
