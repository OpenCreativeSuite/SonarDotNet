namespace SonarDotNet.Core.Audio;

/// <summary>Live measurements shared between the audio thread and the interface.</summary>
/// <remarks>
/// Getters and setters are lock-free. A slightly torn waveform frame is invisible
/// in a scope; stable scalar values use <see cref="Volatile"/>.
/// </remarks>
public sealed class AudioTelemetry
{
    /// <summary>The number of waveform points retained for the scope.</summary>
    public const int WaveformLength = 256;

    private readonly float[] _waveform = new float[WaveformLength];
    private float _inputPeak;
    private float _inputRms;
    private float _outputPeak;
    private float _outputRms;
    private float _gainReductionDb;
    private int _inputClipHold;
    private int _outputClipHold;
    private int _waveIndex;
    private int _dropouts;
    private int _sampleRate = 48000;
    private int _bufferFrames = 256;
    private float _latencyMs;

    /// <summary>The current input peak, 0..1.</summary>
    public float InputPeak => Volatile.Read(ref _inputPeak);

    /// <summary>The current input RMS, 0..1.</summary>
    public float InputRms => Volatile.Read(ref _inputRms);

    /// <summary>The current processed peak, 0..1.</summary>
    public float OutputPeak => Volatile.Read(ref _outputPeak);

    /// <summary>The current processed RMS, 0..1.</summary>
    public float OutputRms => Volatile.Read(ref _outputRms);

    /// <summary>Compressor gain reduction in positive decibels.</summary>
    public float GainReductionDb => Volatile.Read(ref _gainReductionDb);

    /// <summary>Whether the input clipped recently.</summary>
    public bool InputClipped => Volatile.Read(ref _inputClipHold) > 0;

    /// <summary>Whether the processed output clipped recently.</summary>
    public bool OutputClipped => Volatile.Read(ref _outputClipHold) > 0;

    /// <summary>Dropped recording or output buffers since the engine started.</summary>
    public int Dropouts => Volatile.Read(ref _dropouts);

    /// <summary>The capture sample rate reported by the device.</summary>
    public int SampleRate => Volatile.Read(ref _sampleRate);

    /// <summary>The engine block size in frames.</summary>
    public int BufferFrames => Volatile.Read(ref _bufferFrames);

    /// <summary>Approximate monitoring latency in milliseconds.</summary>
    public float LatencyMs => Volatile.Read(ref _latencyMs);

    /// <summary>Reads a copy of the current waveform for drawing.</summary>
    public float[] Snapshot()
    {
        float[] copy = new float[WaveformLength];
        int start = Volatile.Read(ref _waveIndex);
        for (int i = 0; i < WaveformLength; i++)
        {
            copy[i] = _waveform[(start + i) % WaveformLength];
        }

        return copy;
    }

    /// <summary>Updates the live values after a processed block.</summary>
    public void Report(
        ReadOnlySpan<float> input,
        ReadOnlySpan<float> output,
        float gainReductionDb,
        int sampleRate,
        int bufferFrames,
        float latencyMs)
    {
        Volatile.Write(ref _inputPeak, Peak(input));
        Volatile.Write(ref _inputRms, Rms(input));
        Volatile.Write(ref _outputPeak, Peak(output));
        Volatile.Write(ref _outputRms, Rms(output));
        Volatile.Write(ref _gainReductionDb, gainReductionDb);
        Volatile.Write(ref _sampleRate, sampleRate);
        Volatile.Write(ref _bufferFrames, bufferFrames);
        Volatile.Write(ref _latencyMs, latencyMs);

        float inputPeak = Peak(input);
        float outputPeak = Peak(output);
        if (inputPeak >= 0.999f)
        {
            Volatile.Write(ref _inputClipHold, 90);
        }
        else if (Volatile.Read(ref _inputClipHold) > 0)
        {
            Volatile.Write(ref _inputClipHold, Volatile.Read(ref _inputClipHold) - 1);
        }

        if (outputPeak >= 0.999f)
        {
            Volatile.Write(ref _outputClipHold, 90);
        }
        else if (Volatile.Read(ref _outputClipHold) > 0)
        {
            Volatile.Write(ref _outputClipHold, Volatile.Read(ref _outputClipHold) - 1);
        }

        int step = Math.Max(1, output.Length / 32);
        int index = Volatile.Read(ref _waveIndex);
        for (int i = 0; i < output.Length; i += step)
        {
            _waveform[index % WaveformLength] = Math.Clamp(output[i], -1f, 1f);
            index++;
        }

        Volatile.Write(ref _waveIndex, index);
    }

    /// <summary>Records a dropped buffer.</summary>
    public void ReportDropout() => Interlocked.Increment(ref _dropouts);

    /// <summary>Clears all measurements after the engine stops.</summary>
    public void Clear()
    {
        Volatile.Write(ref _inputPeak, 0f);
        Volatile.Write(ref _inputRms, 0f);
        Volatile.Write(ref _outputPeak, 0f);
        Volatile.Write(ref _outputRms, 0f);
        Volatile.Write(ref _gainReductionDb, 0f);
        Volatile.Write(ref _inputClipHold, 0);
        Volatile.Write(ref _outputClipHold, 0);
        Array.Clear(_waveform);
    }

    private static float Peak(ReadOnlySpan<float> samples)
    {
        float peak = 0f;
        foreach (float sample in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(sample));
        }

        return peak;
    }

    private static float Rms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0f;
        }

        double sum = 0.0;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
