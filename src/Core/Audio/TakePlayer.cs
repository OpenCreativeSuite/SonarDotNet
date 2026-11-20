using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SonarDotNet.Core.Audio;

/// <summary>Plays a recorded WAV take through the selected headphone endpoint.</summary>
/// <remarks>
/// Playback deliberately uses the same WASAPI output path as live monitoring,
/// not the Windows default device. That keeps A/B comparison on the headphones
/// the user chose even when the default output is a virtual cable or speakers.
/// </remarks>
public sealed class TakePlayer : IDisposable
{
    private readonly object _sync = new();
    private AudioFileReader? _reader;
    private AudioOutputSink? _sink;
    private Thread? _thread;
    private string? _path;
    private string _devicePath = "";
    private float _volume = 0.8f;
    private volatile bool _playing;
    private bool _disposed;

    /// <summary>The loaded file path, if any.</summary>
    public string? Path => _path;

    /// <summary>Whether playback is running.</summary>
    public bool IsPlaying
    {
        get
        {
            lock (_sync)
            {
                return _playing;
            }
        }
    }

    /// <summary>Playback position in seconds.</summary>
    public double Position
    {
        get
        {
            lock (_sync)
            {
                return _reader?.CurrentTime.TotalSeconds ?? 0.0;
            }
        }
    }

    /// <summary>Total length in seconds.</summary>
    public double Duration
    {
        get
        {
            lock (_sync)
            {
                return _reader?.TotalTime.TotalSeconds ?? 0.0;
            }
        }
    }

    /// <summary>Starts a file, or resumes it when it is already loaded.</summary>
    public void Play(string path, string? deviceId = null, float volume = 0.8f)
    {
        lock (_sync)
        {
            string deviceKey = deviceId ?? "";
            bool needsReload = _path != path || _reader is null;
            bool needsDevice = _devicePath != deviceKey || _sink is null;
            if (needsReload)
            {
                DisposeLocked();
                try
                {
                    _reader = new AudioFileReader(path);
                    _path = path;
                }
                catch
                {
                    DisposeLocked();
                    return;
                }
            }

            _volume = Math.Clamp(volume, 0f, 1f);
            if (needsDevice || needsReload)
            {
                DisposeSinkLocked();
                try
                {
                    MMDevice device = FindDevice(deviceId)
                        ?? throw new InvalidOperationException("The selected headphone output is unavailable.");
                    _sink = new AudioOutputSink(device, 48000, 250, 40) { Volume = _volume };
                    _devicePath = deviceKey;
                }
                catch
                {
                    _playing = false;
                    DisposeSinkLocked();
                    return;
                }
            }
            else if (_sink is not null)
            {
                _sink.Volume = _volume;
            }

            _playing = true;
            if (_thread is null || !_thread.IsAlive)
            {
                _thread = new Thread(PlaybackLoop)
                {
                    IsBackground = true,
                    Name = "Sonus take playback",
                    Priority = ThreadPriority.AboveNormal,
                };
                _thread.Start();
            }
        }
    }

    /// <summary>Pauses playback at the current position.</summary>
    public void Pause()
    {
        lock (_sync)
        {
            _playing = false;
            DisposeSinkLocked();
        }
    }

    /// <summary>Stops and rewinds the current file.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            _playing = false;
            if (_reader is not null)
            {
                _reader.Position = 0;
            }

            DisposeSinkLocked();
        }
    }

    /// <summary>Seeks to a fraction of the file.</summary>
    public void Seek(float fraction)
    {
        lock (_sync)
        {
            if (_reader is null)
            {
                return;
            }

            _playing = false;
            _reader.CurrentTime = TimeSpan.FromSeconds(_reader.TotalTime.TotalSeconds * Math.Clamp(fraction, 0f, 1f));
            DisposeSinkLocked();
        }
    }

    /// <summary>Loads a file without playing it.</summary>
    public void Load(string path)
    {
        lock (_sync)
        {
            if (_path == path)
            {
                return;
            }

            DisposeLocked();
            try
            {
                _reader = new AudioFileReader(path);
                _path = path;
            }
            catch
            {
                DisposeLocked();
            }
        }
    }

    /// <summary>Releases the current file and output stream.</summary>
    public void Unload()
    {
        lock (_sync)
        {
            DisposeLocked();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _playing = false;
            DisposeLocked();
        }
    }

    private void PlaybackLoop()
    {
        float[] buffer = new float[960];
        while (true)
        {
            AudioOutputSink? sink;
            AudioFileReader? reader;
            lock (_sync)
            {
                if (_disposed || !_playing || _sink is null || _reader is null)
                {
                    return;
                }

                sink = _sink;
                reader = _reader;
            }

            try
            {
                if (sink.BufferedDuration < TimeSpan.FromMilliseconds(120))
                {
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        // Let the device finish the buffered tail before closing it.
                        while (_playing && sink.BufferedDuration > TimeSpan.FromMilliseconds(15))
                        {
                            Thread.Sleep(10);
                        }

                        lock (_sync)
                        {
                            _playing = false;
                            _reader.Position = 0;
                            DisposeSinkLocked();
                        }

                        return;
                    }

                    sink.Push(buffer.AsSpan(0, read));
                }
                else
                {
                    Thread.Sleep(8);
                }
            }
            catch
            {
                lock (_sync)
                {
                    _playing = false;
                    DisposeSinkLocked();
                }

                return;
            }
        }
    }

    private static MMDevice? FindDevice(string? id)
    {
        using MMDeviceEnumerator enumerator = new();
        if (!string.IsNullOrEmpty(id))
        {
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (device.ID == id)
                {
                    return device;
                }
            }
        }

        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    private void DisposeSinkLocked()
    {
        _sink?.Dispose();
        _sink = null;
    }

    private void DisposeLocked()
    {
        _playing = false;
        DisposeSinkLocked();
        _reader?.Dispose();
        _reader = null;
        _path = null;
        _devicePath = "";
        _thread = null;
    }
}
