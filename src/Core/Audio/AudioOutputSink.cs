using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SonarDotNet.Core.Audio;

/// <summary>An independent consumer of the processed voice for one playback device.</summary>
/// <remarks>
/// Monitoring and the virtual microphone each own one of these. Both consume the
/// same 48 kHz mono processed block; only the device format conversion differs.
/// </remarks>
public sealed class AudioOutputSink : IDisposable
{
    private readonly WasapiOut _output;
    private readonly BufferedWaveProvider _buffer;
    private readonly StreamingResampler _resampler;
    private readonly float[] _resampled = new float[65536];
    private readonly float[] _interleaved = new float[131072];
    private readonly byte[] _bytes = new byte[262144];
    private readonly int _channels;
    private readonly int _bits;
    private readonly bool _isFloat;
    private readonly int _bufferMilliseconds;
    private readonly int _deviceLatencyMilliseconds;
    private float _volume = 1f;
    private bool _enabled = true;
    private bool _disposed;

    /// <summary>Creates and starts a sink for a render endpoint.</summary>
    public AudioOutputSink(MMDevice device, int sourceRate, int bufferMilliseconds = 100, int deviceLatencyMilliseconds = 30)
    {
        Device = device;
        _bufferMilliseconds = Math.Clamp(bufferMilliseconds, 20, 500);
        _deviceLatencyMilliseconds = Math.Clamp(deviceLatencyMilliseconds, 10, 200);
        WaveFormat target = device.AudioClient.MixFormat;
        _channels = Math.Max(1, target.Channels);
        _bits = Math.Max(8, target.BitsPerSample);
        _isFloat = AudioSampleConverter.IsFloat(target);
        _resampler = new StreamingResampler(sourceRate, target.SampleRate);

        _buffer = new BufferedWaveProvider(target)
        {
            BufferDuration = TimeSpan.FromMilliseconds(_bufferMilliseconds),
            ReadFully = true,
            DiscardOnBufferOverflow = true,
        };

        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, _deviceLatencyMilliseconds);
        _output.Init(_buffer);
        _output.Play();
    }

    /// <summary>The endpoint this sink feeds.</summary>
    public MMDevice Device { get; }

    /// <summary>Whether audio is currently delivered to this endpoint.</summary>
    public bool Enabled
    {
        get => Volatile.Read(ref _enabled);
        set => Volatile.Write(ref _enabled, value);
    }

    /// <summary>Output level, 0..1.</summary>
    public float Volume
    {
        get => Volatile.Read(ref _volume);
        set => Volatile.Write(ref _volume, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>Approximate latency through this endpoint.</summary>
    public float LatencyMs => _bufferMilliseconds + _deviceLatencyMilliseconds;

    /// <summary>Pushes one processed mono block to the device.</summary>
    public void Push(ReadOnlySpan<float> mono)
    {
        if (!Enabled || _disposed || mono.Length == 0)
        {
            return;
        }

        int resampled = _resampler.Process(mono, _resampled);
        if (resampled <= 0)
        {
            return;
        }

        int sampleBytes = Math.Max(1, _bits / 8);
        int maxFrames = Math.Min(
            _interleaved.Length / _channels,
            _bytes.Length / Math.Max(1, _channels * sampleBytes));
        int frames = Math.Min(resampled, maxFrames);
        if (frames <= 0)
        {
            return;
        }

        float volume = Volume;
        int cursor = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            float sample = Math.Clamp(_resampled[frame] * volume, -1f, 1f);
            for (int channel = 0; channel < _channels; channel++)
            {
                _interleaved[cursor++] = sample;
            }
        }

        int byteCount = frames * _channels * sampleBytes;
        WriteBytes(frames, sampleBytes);
        try
        {
            _buffer.AddSamples(_bytes, 0, byteCount);
        }
        catch
        {
            // A disappearing endpoint is reported by the engine's state, not here.
        }
    }

    private void WriteBytes(int frames, int sampleBytes)
    {
        int offset = 0;
        for (int i = 0; i < frames * _channels; i++)
        {
            float value = _interleaved[i];
            switch (sampleBytes)
            {
                case 1:
                    _bytes[offset++] = (byte)Math.Clamp((int)((value * 127f) + 128f), 0, 255);
                    break;
                case 2:
                {
                    short pcm = (short)Math.Clamp((int)(value * 32767f), short.MinValue, short.MaxValue);
                    _bytes[offset++] = (byte)(pcm & 0xFF);
                    _bytes[offset++] = (byte)((pcm >> 8) & 0xFF);
                    break;
                }
                case 3:
                {
                    int pcm = Math.Clamp((int)(value * 8388607f), -8388608, 8388607);
                    _bytes[offset++] = (byte)(pcm & 0xFF);
                    _bytes[offset++] = (byte)((pcm >> 8) & 0xFF);
                    _bytes[offset++] = (byte)((pcm >> 16) & 0xFF);
                    break;
                }
                default:
                    if (_isFloat)
                    {
                        int bits = BitConverter.SingleToInt32Bits(value);
                        _bytes[offset++] = (byte)(bits & 0xFF);
                        _bytes[offset++] = (byte)((bits >> 8) & 0xFF);
                        _bytes[offset++] = (byte)((bits >> 16) & 0xFF);
                        _bytes[offset++] = (byte)((bits >> 24) & 0xFF);
                    }
                    else
                    {
                        int pcm = Math.Clamp((int)(value * 2147483647f), int.MinValue, int.MaxValue);
                        _bytes[offset++] = (byte)(pcm & 0xFF);
                        _bytes[offset++] = (byte)((pcm >> 8) & 0xFF);
                        _bytes[offset++] = (byte)((pcm >> 16) & 0xFF);
                        _bytes[offset++] = (byte)((pcm >> 24) & 0xFF);
                    }

                    break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _output.Stop();
        }
        catch
        {
            // The endpoint may already be gone.
        }

        _output.Dispose();
        _buffer.ClearBuffer();
    }
}
