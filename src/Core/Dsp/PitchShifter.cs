namespace SonarDotNet.Core.Dsp;

/// <summary>
/// A compact, allocation-free delay-line pitch shifter.
/// </summary>
/// <remarks>
/// Two read heads run through a shared delay line at a rate that differs from
/// the write head. A pair of complementary sine-squared windows hides the point
/// where each read head wraps. This is a musical pitch shifter, not a
/// formant-preserving one: it changes the whole voice, which is exactly what the
/// Character stage wants for subtle or dramatic pitch changes.
/// </remarks>
public sealed class PitchShifter
{
    private const int WindowLength = 2048;
    private const int BufferLength = WindowLength * 2;
    private const int Mask = BufferLength - 1;

    private readonly float[] _buffer = new float[BufferLength];
    private int _write;
    private float _phase = 0.25f;
    private float _ratio = 1f;
    private float _semitones;

    /// <summary>Changes the pitch in semitones.</summary>
    public void Configure(float semitones)
    {
        _semitones = Math.Clamp(semitones, -24f, 24f);
        _ratio = MathF.Pow(2f, _semitones / 12f);
    }

    /// <summary>Clears the delay line.</summary>
    public void Reset()
    {
        Array.Clear(_buffer);
        _write = 0;
        _phase = 0.25f;
    }

    /// <summary>Processes one sample.</summary>
    public float Process(float input)
    {
        _buffer[_write & Mask] = input;

        if (MathF.Abs(_semitones) < 0.01f)
        {
            _write++;
            return input;
        }

        float phaseA = _phase;
        float phaseB = _phase + 0.5f;
        if (phaseB >= 1f)
        {
            phaseB -= 1f;
        }

        float windowA = MathF.Sin(MathF.PI * phaseA);
        float windowB = MathF.Sin(MathF.PI * phaseB);
        float weightA = windowA * windowA;
        float weightB = windowB * windowB;
        float first = Read(phaseA * WindowLength);
        float second = Read(phaseB * WindowLength);
        float output = (first * weightA) + (second * weightB);

        _write++;
        _phase += (1f - _ratio) / WindowLength;
        if (_phase >= 1f)
        {
            _phase -= 1f;
        }
        else if (_phase < 0f)
        {
            _phase += 1f;
        }

        return output;
    }

    private float Read(float delay)
    {
        int whole = (int)delay;
        float fraction = delay - whole;
        float first = _buffer[(_write - whole) & Mask];
        float second = _buffer[(_write - whole - 1) & Mask];
        return first + ((second - first) * fraction);
    }
}
