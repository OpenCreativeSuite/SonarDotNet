namespace SonarDotNet.Core.Audio;

/// <summary>The audio host used by the studio session.</summary>
/// <remarks>
/// The interface exists so the processing engine and the interface can be tested
/// without physical audio hardware.
/// </remarks>
public interface IAudioService : IDisposable
{
    /// <summary>Available capture endpoints.</summary>
    IReadOnlyList<AudioDevice> Inputs { get; }

    /// <summary>Available render endpoints.</summary>
    IReadOnlyList<AudioDevice> Outputs { get; }

    /// <summary>Live measurements.</summary>
    AudioTelemetry Telemetry { get; }

    /// <summary>The current host state.</summary>
    AudioEngineState State { get; }

    /// <summary>A human-readable status line.</summary>
    string StatusMessage { get; }

    /// <summary>Whether capture is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Re-enumerates the system endpoints.</summary>
    void RefreshDevices();

    /// <summary>Starts capture and the requested outputs.</summary>
    bool Start(AudioDevice? input, AudioDevice? monitor, AudioDevice? virtualOutput);

    /// <summary>Stops capture and all outputs.</summary>
    void Stop();

    /// <summary>Publishes a new processing configuration.</summary>
    void UpdateSettings(VoiceSettings settings);

    /// <summary>Enables or disables headphone output without stopping capture.</summary>
    void SetMonitorEnabled(bool enabled);

    /// <summary>Enables or disables virtual-microphone output without stopping capture.</summary>
    void SetVirtualEnabled(bool enabled);

    /// <summary>Sets the headphone level, 0..1.</summary>
    void SetMonitorVolume(float volume);

    /// <summary>Starts a two-file recording take.</summary>
    RecordingSession? StartRecording();

    /// <summary>Plays a short confirmation chime through the monitor output.</summary>
    void TestMonitor();

    /// <summary>Stops the current take.</summary>
    RecordingResult? StopRecording();

    /// <summary>The active recording, if any.</summary>
    RecordingSession? ActiveRecording { get; }
}
