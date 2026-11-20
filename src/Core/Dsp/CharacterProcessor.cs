namespace SonarDotNet.Core.Dsp;

/// <summary>Warmths, saturation and a small, preallocated room.</summary>
public sealed class CharacterProcessor
{
    private readonly CombFilter[] _combs;
    private readonly AllPassFilter[] _allPasses;
    private readonly PitchShifter _pitch = new();
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
    }

    /// <summary>Clears the delay lines.</summary>
    public void Reset() => _pitch.Reset();

    /// <summary>Processes one sample.</summary>
    public float Process(float sample)
    {
        if (!_settings.Enabled)
        {
            return sample;
        }

        sample = _pitch.Process(sample);

        float warmth = Math.Clamp(_settings.Warmth / 100f, 0f, 1f);
        float space = Math.Clamp(_settings.Space / 100f, 0f, 1f);

        float shaped = sample;
        float saturated = sample;
        if (warmth > 0.001f)
        {
            float drive = 1f + (warmth * 8f);
            float saturatedWet = MathF.Tanh(sample * drive) / MathF.Tanh(drive);
            saturated = (sample * (1f - (warmth * 0.55f))) + (saturatedWet * warmth * 0.55f);
        }

        if (space <= 0.001f)
        {
            return saturated;
        }

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
        return (saturated * (1f - mix)) + (wet * mix);
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
