using NAudio.CoreAudioApi;
using NAudio.Wave;
using SonarDotNet.Core.Dsp;

namespace SonarDotNet.Core.Audio;

/// <summary>
/// The Windows real-time audio host: WASAPI capture, two independent render
/// consumers, and background WAV writing.
/// </summary>
public sealed class WindowsAudioService : IAudioService
{
    private readonly object _sync = new();
    private readonly List<AudioDevice> _inputs = [];
    private readonly List<AudioDevice> _outputs = [];
    private readonly AudioTelemetry _telemetry = new();
    private readonly VoiceProcessor _processor = new();

    private MMDeviceEnumerator? _enumerator;
    private WasapiCapture? _capture;
    private AudioOutputSink? _monitor;
    private AudioOutputSink? _virtual;
    private StreamingResampler? _captureResampler;
    private float[] _captureMono = new float[8192];
    private float[] _engineRaw = new float[8192];
    private float[] _engineProcessed = new float[8192];
    private VoiceSettings _settings = new();
    private VoiceSettings? _audioSettings;
    private RecordingSession? _recording;
    private AudioEngineState _state = AudioEngineState.Stopped;
    private string _status = "Ready";
    private bool _monitorEnabled;
    private bool _virtualEnabled;
    private float _monitorVolume = 0.8f;
    private bool _disposed;

    /// <summary>Creates the host without opening an endpoint.</summary>
    public WindowsAudioService()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            RefreshDevices();
        }
        catch (Exception ex)
        {
            _state = AudioEngineState.Error;
            _status = "Audio was unavailable: " + ex.Message;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioDevice> Inputs => _inputs;

    /// <inheritdoc/>
    public IReadOnlyList<AudioDevice> Outputs => _outputs;

    /// <inheritdoc/>
    public AudioTelemetry Telemetry => _telemetry;

    /// <inheritdoc/>
    public AudioEngineState State => _state;

    /// <inheritdoc/>
    public string StatusMessage => Volatile.Read(ref _status);

    /// <inheritdoc/>
    public bool IsRunning => _state == AudioEngineState.Running && _capture is not null;

    /// <inheritdoc/>
    public RecordingSession? ActiveRecording => _recording;

    /// <inheritdoc/>
    public void RefreshDevices()
    {
        _inputs.Clear();
        _outputs.Clear();
        if (_enumerator is null)
        {
            return;
        }

        string defaultInput = "";
        string defaultOutput = "";
        try
        {
            defaultInput = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID;
            defaultOutput = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch
        {
            // A machine with no default endpoint still gets its active devices listed.
        }

        try
        {
            foreach (MMDevice device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                _inputs.Add(new AudioDevice(
                    device.ID,
                    device.FriendlyName,
                    AudioDeviceKind.Input,
                    device.ID == defaultInput,
                    LooksLikeVirtualCable(device.FriendlyName)));
            }
        }
        catch
        {
            // Keep whatever was discoverable.
        }

        try
        {
            foreach (MMDevice device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                bool virtualCable = LooksLikeVirtualCable(device.FriendlyName);
                _outputs.Add(new AudioDevice(
                    device.ID,
                    device.FriendlyName,
                    AudioDeviceKind.Output,
                    device.ID == defaultOutput && !virtualCable,
                    virtualCable));
            }
        }
        catch
        {
            // Keep whatever was discoverable.
        }
    }

    /// <summary>Returns true when a render endpoint looks like a virtual cable.</summary>
    public static bool LooksLikeVirtualCable(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("cable input", StringComparison.Ordinal)
            || lower.Contains("cable output", StringComparison.Ordinal)
            || lower.Contains("vb-audio", StringComparison.Ordinal)
            || lower.Contains("voicemeeter input", StringComparison.Ordinal)
            || lower.Contains("voicemeeter output", StringComparison.Ordinal)
            || lower.Contains("virtual audio cable", StringComparison.Ordinal)
            || lower.Contains("virtual cable", StringComparison.Ordinal)
            || lower.Contains("vac input", StringComparison.Ordinal)
            || lower.Contains("vac output", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public bool Start(AudioDevice? input, AudioDevice? monitor, AudioDevice? virtualOutput)
    {
        lock (_sync)
        {
            StopLocked();
            if (input is null)
            {
                _state = AudioEngineState.Stopped;
                _status = "Choose a microphone to begin";
                return false;
            }

            if (_enumerator is null)
            {
                _state = AudioEngineState.Error;
                _status = "The Windows audio host is unavailable";
                return false;
            }

            try
            {
                MMDevice captureDevice = Find(input.Id, DataFlow.Capture)
                    ?? throw new InvalidOperationException("That microphone is no longer available.");

                _capture = new WasapiCapture(captureDevice, true, 20);
                WaveFormat captureFormat = _capture.WaveFormat;
                _captureResampler = new StreamingResampler(captureFormat.SampleRate, VoiceProcessor.SampleRate);
                int capacity = Math.Max(4096, _captureResampler.MaximumOutputCount(8192));
                _captureMono = new float[Math.Max(4096, captureFormat.SampleRate / 4)];
                _engineRaw = new float[capacity];
                _engineProcessed = new float[capacity];

                MMDevice? monitorDevice = monitor is null ? null : Find(monitor.Id, DataFlow.Render);
                MMDevice? virtualDevice = virtualOutput is null ? null : Find(virtualOutput.Id, DataFlow.Render);
                string outputWarning = "";

                if (monitorDevice is not null)
                {
                    try
                    {
                        _monitor = new AudioOutputSink(monitorDevice, VoiceProcessor.SampleRate, 60, 20)
                        {
                            Enabled = _monitorEnabled,
                            Volume = _monitorVolume,
                        };
                    }
                    catch (Exception ex)
                    {
                        outputWarning = "Monitoring unavailable: " + ex.Message;
                    }
                }

                if (virtualDevice is not null)
                {
                    try
                    {
                        _virtual = new AudioOutputSink(virtualDevice, VoiceProcessor.SampleRate, 120, 40)
                        {
                            Enabled = _virtualEnabled,
                            Volume = 1f,
                        };
                    }
                    catch (Exception ex)
                    {
                        outputWarning = outputWarning.Length > 0
                            ? outputWarning + " Virtual output unavailable: " + ex.Message
                            : "Virtual output unavailable: " + ex.Message;
                    }
                }

                _capture.DataAvailable += OnCapture;
                _capture.RecordingStopped += OnCaptureStopped;
                _processor.Reset();
                _audioSettings = null;
                _processor.Configure(_settings);
                _audioSettings = _settings;
                _capture.StartRecording();
                _state = AudioEngineState.Running;
                _status = outputWarning.Length > 0
                    ? outputWarning
                    : $"Listening to {input.ShortName}";
                return true;
            }
            catch (Exception ex)
            {
                StopLocked();
                _state = AudioEngineState.Error;
                _status = ex.Message;
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_sync)
        {
            StopLocked();
        }
    }

    /// <inheritdoc/>
    public void UpdateSettings(VoiceSettings settings) => Volatile.Write(ref _settings, settings);

    /// <inheritdoc/>
    public void SetMonitorEnabled(bool enabled)
    {
        _monitorEnabled = enabled;
        if (_monitor is not null)
        {
            _monitor.Enabled = enabled;
        }
    }

    /// <inheritdoc/>
    public void SetVirtualEnabled(bool enabled)
    {
        _virtualEnabled = enabled;
        if (_virtual is not null)
        {
            _virtual.Enabled = enabled;
        }
    }

    /// <inheritdoc/>
    public void SetMonitorVolume(float volume)
    {
        _monitorVolume = Math.Clamp(volume, 0f, 1f);
        if (_monitor is not null)
        {
            _monitor.Volume = _monitorVolume;
        }
    }

    /// <inheritdoc/>
    public void TestMonitor()
    {
        AudioOutputSink? monitor = _monitor;
        if (monitor is null)
        {
            _status = "Start audio and choose headphones before testing";
            return;
        }

        int frames = (int)(VoiceProcessor.SampleRate * 0.42f);
        float[] chime = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)VoiceProcessor.SampleRate;
            float envelope = MathF.Min(1f, t / 0.015f) * MathF.Min(1f, (0.42f - t) / 0.08f);
            chime[i] = envelope * 0.18f * MathF.Sin(2f * MathF.PI * (t < 0.18f ? 660f : 880f) * t);
        }

        monitor.Push(chime);
        _status = "Monitor test played";
    }

    /// <inheritdoc/>
    public RecordingSession? StartRecording()
    {        if (!IsRunning)
        {
            _status = "Start the microphone before recording";
            return null;
        }

        if (_recording is not null)
        {
            return _recording;
        }

        try
        {
            _recording = new RecordingSession();
            _status = "Recording raw and processed audio";
            return _recording;
        }
        catch (Exception ex)
        {
            _status = "Could not start recording: " + ex.Message;
            return null;
        }
    }

    /// <inheritdoc/>
    public RecordingResult? StopRecording()
    {
        RecordingSession? session = _recording;
        if (session is null)
        {
            return null;
        }

        _recording = null;
        try
        {
            RecordingResult result = session.Stop();
            _status = "Recording saved";
            return result;
        }
        catch (Exception ex)
        {
            _status = "Could not finish recording: " + ex.Message;
            return null;
        }
    }

    private void OnCapture(object? sender, WaveInEventArgs args)
    {
        try
        {
            WaveFormat format = _capture?.WaveFormat ?? throw new InvalidOperationException("Capture stopped");
            int frames = AudioSampleConverter.ToMono(args.Buffer.AsSpan(0, args.BytesRecorded), format, _captureMono);
            if (frames <= 0)
            {
                return;
            }

            StreamingResampler resampler = _captureResampler ?? throw new InvalidOperationException("Capture stopped");
            int engineFrames = resampler.IsIdentity
                ? Copy(_captureMono.AsSpan(0, frames), _engineRaw)
                : resampler.Process(_captureMono.AsSpan(0, frames), _engineRaw);

            if (engineFrames <= 0)
            {
                return;
            }

            Span<float> raw = _engineRaw.AsSpan(0, engineFrames);
            Span<float> processed = _engineProcessed.AsSpan(0, engineFrames);
            raw.CopyTo(processed);

            VoiceSettings settings = Volatile.Read(ref _settings);
            if (!ReferenceEquals(_audioSettings, settings))
            {
                _processor.Configure(settings);
                _audioSettings = settings;
            }

            _processor.Process(processed);

            if (_monitor is not null)
            {
                _monitor.Push(processed);
            }

            if (_virtual is not null)
            {
                _virtual.Push(processed);
            }

            float latency = 0f;
            if (_monitor is not null)
            {
                latency = _monitor.LatencyMs;
            }

            _recording?.Enqueue(raw, processed);
            _telemetry.Report(raw, processed, _processor.GainReductionDb, format.SampleRate, engineFrames, latency);
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _status, "Audio stream error: " + ex.Message);
        }
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null)
        {
            _state = AudioEngineState.Error;
            Volatile.Write(ref _status, "Microphone disconnected: " + args.Exception.Message);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (_sync)
                {
                    StopLocked(keepStatus: true, keepError: true);
                }
            });
        }
    }

    private void StopLocked(bool keepStatus = false, bool keepError = false)
    {
        if (_capture is not null)
        {
            try
            {
                _capture.DataAvailable -= OnCapture;
                _capture.RecordingStopped -= OnCaptureStopped;
                if (_capture.CaptureState == CaptureState.Capturing)
                {
                    _capture.StopRecording();
                }

                _capture.Dispose();
            }
            catch
            {
                // The endpoint has probably already vanished.
            }

            _capture = null;
        }

        _monitor?.Dispose();
        _monitor = null;
        _virtual?.Dispose();
        _virtual = null;
        _captureResampler = null;
        _captureMono = new float[8192];
        _engineRaw = new float[8192];
        _engineProcessed = new float[8192];
        _audioSettings = null;
        _telemetry.Clear();
        if (!keepError)
        {
            _state = AudioEngineState.Stopped;
        }

        if (!keepStatus)
        {
            _status = "Audio stopped";
        }
    }

    private MMDevice? Find(string id, DataFlow flow)
    {
        if (_enumerator is null)
        {
            return null;
        }

        try
        {
            foreach (MMDevice device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                if (device.ID == id)
                {
                    return device;
                }
            }
        }
        catch
        {
            // Fall through to null.
        }

        return null;
    }

    private static int Copy(ReadOnlySpan<float> source, float[] destination)
    {
        int count = Math.Min(source.Length, destination.Length);
        source[..count].CopyTo(destination);
        return count;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _recording?.Dispose();
        _enumerator?.Dispose();
    }
}
