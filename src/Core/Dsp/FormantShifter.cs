namespace SonarDotNet.Core.Dsp;

/// <summary>
/// A spectral-envelope shifter for formants and vocal size.
/// </summary>
/// <remarks>
/// <para>
/// A short-time Fourier analysis estimates the smooth spectral envelope of the
/// voice. The envelope is then warped along the frequency axis, while the
/// original phase is kept. That changes the resonant peaks (the formants)
/// without moving the fundamental pitch, which is exactly the distinction
/// between pitch and vocal size.
/// </para>
/// <para>
/// A 1024-sample window with 75% overlap gives roughly 21 ms of latency. All
/// FFT tables, windows and overlap buffers are allocated once; the audio path
/// only reads and writes them.
/// </para>
/// </remarks>
public sealed class FormantShifter
{
    private const int FftSize = 1024;
    private const int HopSize = 256;
    private const int BinCount = (FftSize / 2) + 1;
    private const int HistoryMask = FftSize - 1;
    private const int FifoLength = 4096;
    private const float OverlapScale = 1f / 1.5f;
    private const int EnvelopeRadius = 5;

    private readonly float[] _window = new float[FftSize];
    private readonly float[] _history = new float[FftSize];
    private readonly float[] _overlap = new float[FftSize];
    private readonly float[] _real = new float[FftSize];
    private readonly float[] _imag = new float[FftSize];
    private readonly float[] _envelope = new float[BinCount];
    private readonly float[] _smoothed = new float[BinCount];
    private readonly float[] _fifo = new float[FifoLength];
    private readonly float[] _cos = new float[FftSize / 2];
    private readonly float[] _sin = new float[FftSize / 2];
    private readonly int[] _bitReverse = new int[FftSize];

    private int _historyWrite;
    private int _historyCount;
    private int _hopCount;
    private int _fifoRead;
    private int _fifoWrite;
    private int _fifoCount;
    private float _ratio = 1f;
    private float _semitones;

    /// <summary>Creates the shifter and prepares its FFT tables.</summary>
    public FormantShifter()
    {
        for (int i = 0; i < FftSize; i++)
        {
            _window[i] = 0.5f - (0.5f * MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));
        }

        for (int i = 0; i < _cos.Length; i++)
        {
            double angle = 2.0 * Math.PI * i / FftSize;
            _cos[i] = (float)Math.Cos(angle);
            _sin[i] = (float)Math.Sin(angle);
        }

        int bits = (int)MathF.Log2(FftSize);
        for (int i = 0; i < FftSize; i++)
        {
            int value = i;
            int reversed = 0;
            for (int bit = 0; bit < bits; bit++)
            {
                reversed = (reversed << 1) | (value & 1);
                value >>= 1;
            }

            _bitReverse[i] = reversed;
        }
    }

    /// <summary>Changes the formant shift in semitones.</summary>
    public void Configure(float semitones)
    {
        float clamped = Math.Clamp(semitones, -18f, 18f);
        if (MathF.Abs(clamped) < 0.01f)
        {
            _semitones = 0f;
            _ratio = 1f;
            Reset();
            return;
        }

        _semitones = clamped;
        _ratio = MathF.Pow(2f, _semitones / 12f);
    }

    /// <summary>Clears the STFT and overlap-add state.</summary>
    public void Reset()
    {
        Array.Clear(_history);
        Array.Clear(_overlap);
        Array.Clear(_fifo);
        Array.Clear(_real);
        Array.Clear(_imag);
        _historyWrite = 0;
        _historyCount = 0;
        _hopCount = 0;
        _fifoRead = 0;
        _fifoWrite = 0;
        _fifoCount = 0;
    }

    /// <summary>Processes one sample.</summary>
    public float Process(float input)
    {
        if (MathF.Abs(_semitones) < 0.01f)
        {
            return input;
        }

        _history[_historyWrite] = input;
        _historyWrite = (_historyWrite + 1) & HistoryMask;
        if (_historyCount < FftSize)
        {
            _historyCount++;
        }

        _hopCount++;
        if (_hopCount >= HopSize && _historyCount == FftSize)
        {
            _hopCount = 0;
            ProcessFrame();
        }

        if (_fifoCount == 0)
        {
            return 0f;
        }

        float output = _fifo[_fifoRead];
        _fifoRead = (_fifoRead + 1) % FifoLength;
        _fifoCount--;
        return output;
    }

    private void ProcessFrame()
    {
        int start = _historyWrite;
        for (int i = 0; i < FftSize; i++)
        {
            _real[i] = _history[(start + i) & HistoryMask] * _window[i];
            _imag[i] = 0f;
        }

        Fft(_real, _imag, inverse: false);
        BuildEnvelope();

        for (int bin = 0; bin < BinCount; bin++)
        {
            float source = bin / _ratio;
            source = Math.Clamp(source, 0f, BinCount - 1);
            float shifted = SampleEnvelope(source);
            float gain = MathF.Exp(shifted - _envelope[bin]);
            gain = Math.Clamp(gain, 0.18f, 5.5f);
            _real[bin] *= gain;
            _imag[bin] *= gain;

            int mirror = FftSize - bin;
            if (bin > 0 && mirror < FftSize && mirror != bin)
            {
                _real[mirror] *= gain;
                _imag[mirror] *= gain;
            }
        }

        Fft(_real, _imag, inverse: true);

        for (int i = 0; i < FftSize; i++)
        {
            _overlap[i] += _real[i] * _window[i] * OverlapScale;
        }

        for (int i = 0; i < HopSize; i++)
        {
            if (_fifoCount >= FifoLength)
            {
                break;
            }

            _fifo[_fifoWrite] = _overlap[i];
            _fifoWrite = (_fifoWrite + 1) % FifoLength;
            _fifoCount++;
        }

        Array.Copy(_overlap, HopSize, _overlap, 0, FftSize - HopSize);
        Array.Clear(_overlap, FftSize - HopSize, HopSize);
    }

    private void BuildEnvelope()
    {
        for (int bin = 0; bin < BinCount; bin++)
        {
            float magnitude = MathF.Sqrt((_real[bin] * _real[bin]) + (_imag[bin] * _imag[bin]));
            _envelope[bin] = MathF.Log(magnitude + 1e-7f);
        }

        // A short moving average through log magnitude gives a smooth vocal
        // envelope while keeping the harmonic peaks out of the estimate.
        for (int bin = 0; bin < BinCount; bin++)
        {
            int from = Math.Max(0, bin - EnvelopeRadius);
            int to = Math.Min(BinCount - 1, bin + EnvelopeRadius);
            float sum = 0f;
            for (int i = from; i <= to; i++)
            {
                sum += _envelope[i];
            }

            _smoothed[bin] = sum / (to - from + 1);
        }

        for (int bin = 0; bin < BinCount; bin++)
        {
            _envelope[bin] = _smoothed[bin];
        }
    }
    private float SampleEnvelope(float position)
    {
        int index = (int)position;
        if (index >= BinCount - 1)
        {
            return _envelope[BinCount - 1];
        }

        float fraction = position - index;
        return _envelope[index] + ((_envelope[index + 1] - _envelope[index]) * fraction);
    }

    private void Fft(float[] real, float[] imag, bool inverse)
    {
        for (int i = 0; i < FftSize; i++)
        {
            int j = _bitReverse[i];
            if (j <= i)
            {
                continue;
            }

            (real[i], real[j]) = (real[j], real[i]);
            (imag[i], imag[j]) = (imag[j], imag[i]);
        }

        int sign = inverse ? 1 : -1;
        for (int length = 2; length <= FftSize; length <<= 1)
        {
            int half = length >> 1;
            int step = FftSize / length;
            for (int start = 0; start < FftSize; start += length)
            {
                for (int j = 0; j < half; j++)
                {
                    int twiddle = j * step;
                    float cosine = _cos[twiddle];
                    float sine = sign * _sin[twiddle];
                    int a = start + j;
                    int b = a + half;
                    float oddReal = (real[b] * cosine) - (imag[b] * sine);
                    float oddImag = (real[b] * sine) + (imag[b] * cosine);
                    real[b] = real[a] - oddReal;
                    imag[b] = imag[a] - oddImag;
                    real[a] += oddReal;
                    imag[a] += oddImag;
                }
            }
        }

        if (inverse)
        {
            float scale = 1f / FftSize;
            for (int i = 0; i < FftSize; i++)
            {
                real[i] *= scale;
                imag[i] *= scale;
            }
        }
    }
}
