namespace SonarDotNet.Core.Dsp;

/// <summary>Warmths, saturation and a small, preallocated room.</summary>
public sealed class CharacterProcessor
{
    private readonly CombFilter[] _combs;
    private readonly AllPassFilter[] _allPasses;
    private readonly PitchShifter _pitch = new();
    private readonly FormantShifter _formant = new();
    private readonly float[] _delayBuffer = new float[32768];
    private Biquad _resonance = new();
    private Biquad _brightness = new();
    private int _delayWrite;
    private int _delaySamples = 11520;
    private float _delayFeedback = 0.28f;
    private CharacterSettings _settings = new();

    /// <summary>Creates the character stage at the engine sample rate.</summary>
    public CharacterProcessor(int sampleRate)
    {
        _combs =
        [
            new CombFilter(Milliseconds(29.7f, sampleRate)),
            new CombFilter(Milliseconds(33.1f, sampleRate)),
            new CombFilter(Milliseconds(35.9f, sampleRate)),
            new CombFilter(Milliseconds(38.3f, sampleRate)),
        ];
        _allPasses =
        [
            new AllPassFilter(Milliseconds(5.0f, sampleRate)),
            new AllPassFilter(Milliseconds(1.7f, sampleRate)),
        ];
    }

    /// <summary>Updates the stage parameters.</summary>
    public void Configure(CharacterSettings settings)
    {
        _settings = settings;
        _pitch.Configure(settings.PitchSemitones);
        _formant.Configure(settings.FormantSemitones);
        _resonance.Peaking(920f, Math.Clamp(settings.Resonance, -1f, 1f) * 9f, 1.15f, 48000);
        _brightness.HighShelf(3400f, Math.Clamp(settings.Brightness, -1f, 1f) * 8f, 0.7f, 48000);
        _delaySamples = Math.Clamp((int)(settings.DelayMs * 48f), 2400, _delayBuffer.Length - 2);
        _delayFeedback = Math.Clamp(settings.DelayFeedback, 0f, 0.85f);
    }

    /// <summary>Clears the delay lines.</summary>
    public void Reset()
    {
        _pitch.Reset();
        _formant.Reset();
        _resonance.Reset();
        _brightness.Reset();
        Array.Clear(_delayBuffer);
        _delayWrite = 0;
    }

    /// <summary>Processes one sample.</summary>
    public float Process(float sample)
    {
        float saturated = sample;
        if (_settings.Enabled)
        {
            sample = _pitch.Process(sample);
            sample = _formant.Process(sample);
            sample = _resonance.Process(sample);
            sample = _brightness.Process(sample);

            float warmth = Math.Clamp(_settings.Warmth / 100f, 0f, 1f);
            saturated = sample;
            if (warmth > 0.001f)
            {
                float drive = 1f + (warmth * 8f);
                float saturatedWet = MathF.Tanh(sample * drive) / MathF.Tanh(drive);
                saturated = (sample * (1f - (warmth * 0.55f))) + (saturatedWet * warmth * 0.55f);
            }
        }

        if (!_settings.SpaceEnabled)
        {
            return saturated;
        }

        float space = Math.Clamp(_settings.Space / 100f, 0f, 1f);
        float result = saturated;
        if (space > 0.001f)
        {
            float feedback = 0.58f + (space * 0.34f);
            float wet = 0f;
            foreach (CombFilter comb in _combs)
            {
                wet += comb.Process(saturated, feedback);
            }

            wet *= 0.25f;
            foreach (AllPassFilter allPass in _allPasses)
            {
                wet = allPass.Process(wet);
            }

            float mix = Math.Clamp(_settings.SpaceMix, 0f, 1f) * space * 0.62f;
            result = (saturated * (1f - mix)) + (wet * mix);
        }

        float delayMix = Math.Clamp(_settings.DelayMix, 0f, 1f);
        int read = (_delayWrite - _delaySamples) & (_delayBuffer.Length - 1);
        float delayed = _delayBuffer[read];
        _delayBuffer[_delayWrite] = result + (delayed * _delayFeedback);
        _delayWrite = (_delayWrite + 1) & (_delayBuffer.Length - 1);
        return delayMix > 0.001f
            ? (result * (1f - delayMix * 0.6f)) + (delayed * delayMix * 0.6f)
            : result;
    }

    private static int Milliseconds(float milliseconds, int sampleRate) =>
        Math.Max(8, (int)(milliseconds * sampleRate / 1000f));

    private sealed class CombFilter
    {
        private readonly float[] _buffer;
        private int _index;

        public CombFilter(int length) => _buffer = new float[length];

        public float Process(float input, float feedback)
        {
            float delayed = _buffer[_index];
            _buffer[_index] = input + (delayed * feedback);
            if (++_index >= _buffer.Length)
            {
                _index = 0;
            }

            return delayed;
        }
    }

    private sealed class AllPassFilter
    {
        private readonly float[] _buffer;
        private int _index;
        private const float Feedback = 0.5f;

        public AllPassFilter(int length) => _buffer = new float[length];

        public float Process(float input)
        {
            float delayed = _buffer[_index];
            float output = delayed - input;
            _buffer[_index] = input + (delayed * Feedback);
            if (++_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }
    }
}
