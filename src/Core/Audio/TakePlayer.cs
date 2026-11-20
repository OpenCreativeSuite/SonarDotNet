using NAudio.Wave;

namespace SonarDotNet.Core.Audio;

/// <summary>Plays a recorded WAV take for comparison.</summary>
public sealed class TakePlayer : IDisposable
{
    private readonly object _sync = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private string? _path;
    private bool _playing;

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
    public void Play(string path)
    {
        lock (_sync)
        {
            if (_path != path)
            {
                DisposeLocked();
                try
                {
                    _reader = new AudioFileReader(path);
                    _output = new WaveOutEvent { DesiredLatency = 100 };
                    _output.Init(_reader);
                    _output.PlaybackStopped += (_, _) =>
                    {
                        lock (_sync)
                        {
                            _playing = false;
                        }
                    };
                    _path = path;
                }
                catch
                {
                    DisposeLocked();
                    return;
                }
            }

            _output?.Play();
            _playing = true;
        }
    }

    /// <summary>Pauses the current file.</summary>
    public void Pause()
    {
        lock (_sync)
        {
            _output?.Pause();
            _playing = false;
        }
    }

    /// <summary>Stops and rewinds the current file.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            if (_reader is not null)
            {
                _reader.Position = 0;
            }

            _output?.Stop();
            _playing = false;
        }
    }

    /// <summary>Seeks to a fraction of the file.</summary>
    public void Seek(float fraction)
    {
        lock (_sync)
        {
            if (_reader is not null)
            {
                _reader.CurrentTime = TimeSpan.FromSeconds(_reader.TotalTime.TotalSeconds * Math.Clamp(fraction, 0f, 1f));
            }
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
                _output = new WaveOutEvent { DesiredLatency = 100 };
                _output.Init(_reader);
                _path = path;
            }
            catch
            {
                DisposeLocked();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            DisposeLocked();
        }
    }

    private void DisposeLocked()
    {
        _output?.Dispose();
        _output = null;
        _reader?.Dispose();
        _reader = null;
        _path = null;
        _playing = false;
    }
}
