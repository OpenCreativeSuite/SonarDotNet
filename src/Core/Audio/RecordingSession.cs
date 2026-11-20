using NAudio.Wave;

namespace SonarDotNet.Core.Audio;

/// <summary>The files produced by one recording take.</summary>
/// <param name="RawPath">The unprocessed microphone WAV.</param>
/// <param name="ProcessedPath">The processed microphone WAV.</param>
/// <param name="Duration">Length in seconds.</param>
public sealed record RecordingResult(string RawPath, string ProcessedPath, TimeSpan Duration);

/// <summary>
/// Writes raw and processed microphone audio to two WAV files without doing file
/// I/O on the audio callback thread.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    /// <summary>The recording sample rate.</summary>
    public const int SampleRate = 48000;

    private readonly FloatRingBuffer _raw = new(SampleRate * 60);
    private readonly FloatRingBuffer _processed = new(SampleRate * 60);
    private readonly WaveFileWriter _rawWriter;
    private readonly WaveFileWriter _processedWriter;
    private readonly Thread _thread;
    private readonly byte[] _rawBytes = new byte[4096];
    private readonly byte[] _processedBytes = new byte[4096];
    private readonly float[] _rawBlock = new float[2048];
    private readonly float[] _processedBlock = new float[2048];
    private volatile bool _running = true;
    private volatile bool _started;
    private long _frames;
    private bool _disposed;

    /// <summary>Starts a take in the user's recordings folder.</summary>
    public RecordingSession(string? folder = null)
    {
        string root = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Sonus Recordings");
        Directory.CreateDirectory(root);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        RawPath = Path.Combine(root, $"Sonus_{stamp}_raw.wav");
        ProcessedPath = Path.Combine(root, $"Sonus_{stamp}_processed.wav");

        WaveFormat format = new(SampleRate, 16, 1);
        _rawWriter = new WaveFileWriter(RawPath, format);
        _processedWriter = new WaveFileWriter(ProcessedPath, format);
        _thread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "Sonus recording writer",
        };
        _thread.Start();
    }

    /// <summary>The raw WAV path.</summary>
    public string RawPath { get; }

    /// <summary>The processed WAV path.</summary>
    public string ProcessedPath { get; }

    /// <summary>Elapsed recording time.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Interlocked.Read(ref _frames) / (double)SampleRate);

    /// <summary>Whether the writer is still open.</summary>
    public bool IsRunning => _running;

    /// <summary>Adds a block to both recordings. Called from the audio thread.</summary>
    public void Enqueue(ReadOnlySpan<float> raw, ReadOnlySpan<float> processed)
    {
        if (!_running)
        {
            return;
        }

        _raw.Write(raw);
        _processed.Write(processed);
        Interlocked.Add(ref _frames, raw.Length);
    }

    /// <summary>Finishes both files and returns their location.</summary>
    public RecordingResult Stop()
    {
        if (!_disposed)
        {
            _running = false;
            _thread.Join(2000);
            Drain();
            _rawWriter.Flush();
            _processedWriter.Flush();
            _rawWriter.Dispose();
            _processedWriter.Dispose();
            _disposed = true;
        }

        return new RecordingResult(RawPath, ProcessedPath, Duration);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
        }
    }

    private void WriteLoop()
    {
        while (_running || _raw.Count > 0 || _processed.Count > 0)
        {
            if (!_started)
            {
                _started = true;
            }

            Drain();
            if (_raw.Count == 0 && _processed.Count == 0)
            {
                Thread.Sleep(8);
            }
        }
    }

    private void Drain()
    {
        while (_raw.Count > 0 || _processed.Count > 0)
        {
            int rawRead = _raw.Read(_rawBlock);
            int processedRead = _processed.Read(_processedBlock);
            _rawBytes[..(rawRead * 2)].AsSpan().Clear();
            _processedBytes[..(processedRead * 2)].AsSpan().Clear();
            for (int i = 0; i < rawRead; i++)
            {
                short value = (short)Math.Clamp((int)(_rawBlock[i] * 32767f), short.MinValue, short.MaxValue);
                _rawBytes[i * 2] = (byte)(value & 0xFF);
                _rawBytes[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            for (int i = 0; i < processedRead; i++)
            {
                short value = (short)Math.Clamp((int)(_processedBlock[i] * 32767f), short.MinValue, short.MaxValue);
                _processedBytes[i * 2] = (byte)(value & 0xFF);
                _processedBytes[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            if (rawRead > 0)
            {
                _rawWriter.Write(_rawBytes, 0, rawRead * 2);
            }

            if (processedRead > 0)
            {
                _processedWriter.Write(_processedBytes, 0, processedRead * 2);
            }
        }
    }
}
