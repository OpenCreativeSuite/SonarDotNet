namespace SonarDotNet.Core.Audio;

/// <summary>A small streaming linear resampler used at device boundaries.</summary>
/// <remarks>
/// Linear interpolation is deliberately modest: device formats are normally
/// already 48 kHz, and the fallback only has to be clean and predictable. The
/// class does not allocate after construction.
/// </remarks>
public sealed class StreamingResampler
{
    private readonly double _step;
    private double _position;
    private float _last = 0f;
    private bool _hasLast;

    /// <summary>Creates a resampler.</summary>
    public StreamingResampler(int sourceRate, int targetRate)
    {
        SourceRate = Math.Max(1, sourceRate);
        TargetRate = Math.Max(1, targetRate);
        _step = (double)SourceRate / TargetRate;
    }

    /// <summary>The input sample rate.</summary>
    public int SourceRate { get; }

    /// <summary>The output sample rate.</summary>
    public int TargetRate { get; }

    /// <summary>Whether the resampler is a pass-through.</summary>
    public bool IsIdentity => SourceRate == TargetRate;

    /// <summary>Clears interpolation state.</summary>
    public void Reset()
    {
        _position = 0.0;
        _last = 0f;
        _hasLast = false;
    }

    /// <summary>
    /// Resamples one block. The output span must be large enough for
    /// <see cref="MaximumOutputCount"/> samples.
    /// </summary>
    public int Process(ReadOnlySpan<float> input, Span<float> output)
    {
        if (input.Length == 0)
        {
            return 0;
        }

        if (IsIdentity)
        {
            input.CopyTo(output);
            return input.Length;
        }

        int written = 0;
        while (_position < input.Length && written < output.Length)
        {
            int index = (int)_position;
            float fraction = (float)(_position - index);
            float first;
            if (index < 0)
            {
                first = _hasLast ? _last : input[0];
            }
            else
            {
                first = input[Math.Min(index, input.Length - 1)];
            }

            float second = index + 1 < input.Length
                ? input[index + 1]
                : first;
            output[written++] = first + ((second - first) * fraction);
            _position += _step;
        }

        _position -= input.Length;
        if (_position < 0.0)
        {
            _position = 0.0;
        }

        _last = input[^1];
        _hasLast = true;
        return written;
    }

    /// <summary>The largest output a block of the supplied length can produce.</summary>
    public int MaximumOutputCount(int inputLength) =>
        (int)Math.Ceiling(inputLength * (double)TargetRate / SourceRate) + 4;
}
