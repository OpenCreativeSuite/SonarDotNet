using Donkeh.Testing;
using Donkeh.UILab;
using SonarDotNet.Components;
using SonarDotNet.Core;
using SonarDotNet.Core.Audio;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet.Tests;

/// <summary>Shared fixtures for DSP and component tests.</summary>
internal static class Fx
{
    public static readonly AudioDevice Microphone =
        new("mic-1", "Studio Microphone", AudioDeviceKind.Input, IsDefault: true);

    public static readonly AudioDevice Microphone2 =
        new("mic-2", "USB Microphone", AudioDeviceKind.Input);

    public static readonly AudioDevice Headphones =
        new("out-1", "Studio Headphones", AudioDeviceKind.Output, IsDefault: true);

    public static readonly AudioDevice Cable =
        new("cable-1", "CABLE Input (VB-Audio Virtual Cable)", AudioDeviceKind.Output, IsVirtualCable: true);

    /// <summary>A session with known devices and no persistence.</summary>
    public static StudioSession Session(FakeAudioService? audio = null)
    {
        FakeAudioService service = audio ?? new FakeAudioService();
        SonusSettings preferences = new()
        {
            InputDeviceId = Microphone.Id,
            MonitorDeviceId = Headphones.Id,
            VirtualDeviceId = Cable.Id,
            MonitoringEnabled = false,
            VirtualEnabled = false,
            MonitorVolume = 0.8f,
            ActiveStage = StudioStage.Tone,
            PresetName = "Natural",
            LiveOnLaunch = false,
        };
        return new StudioSession(service, preferences, PresetLibrary.BuiltIn, persistPreferences: false, loadRecentTakes: false);
    }

    /// <summary>Wraps a component in the application provider.</summary>
    public static Node WithSession(StudioSession session, Node child) =>
        Provider(SessionContext.Value, session, child);

    /// <summary>Mounts a component against a session.</summary>
    public static ComponentTestHarness Render(StudioSession session, Node child) =>
        ComponentTest.Render(WithSession(session, child));

    /// <summary>Re-renders a mounted component after a session mutation.</summary>
    public static void Reload<T>(ComponentTestHarness page, StudioSession session)
        where T : Component, new() =>
        page.Update(WithSession(session, C<T>()));

    /// <summary>A sine wave at the engine sample rate.</summary>
    public static float[] Tone(float frequency, int frames, float amplitude = 0.25f, int rate = 48000)
    {
        float[] samples = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * frequency * i / rate);
        }

        return samples;
    }
}

/// <summary>An audio host that never touches hardware.</summary>
internal sealed class FakeAudioService : IAudioService
{
    public FakeAudioService()
    {
        Inputs = [Fx.Microphone, Fx.Microphone2];
        Outputs = [Fx.Headphones, Fx.Cable];
    }

    public IReadOnlyList<AudioDevice> Inputs { get; }

    public IReadOnlyList<AudioDevice> Outputs { get; }

    public AudioTelemetry Telemetry { get; } = new();

    public AudioEngineState State { get; private set; } = AudioEngineState.Stopped;

    public string StatusMessage { get; private set; } = "Ready";

    public bool IsRunning => State == AudioEngineState.Running;

    public VoiceSettings? Settings { get; private set; }

    public bool MonitorEnabled { get; private set; }

    public bool VirtualEnabled { get; private set; }

    public float MonitorVolume { get; private set; }

    public int StartCount { get; private set; }

    public int RecordingCount { get; private set; }

    public AudioDevice? StartedInput { get; private set; }

    public AudioDevice? StartedMonitor { get; private set; }

    public AudioDevice? StartedVirtual { get; private set; }

    public RecordingSession? ActiveRecording => null;

    public void RefreshDevices()
    {
    }

    public bool Start(AudioDevice? input, AudioDevice? monitor, AudioDevice? virtualOutput)
    {
        StartedInput = input;
        StartedMonitor = monitor;
        StartedVirtual = virtualOutput;
        StartCount++;
        State = input is null ? AudioEngineState.Error : AudioEngineState.Running;
        StatusMessage = input is null ? "Choose a microphone" : "Listening";
        return input is not null;
    }

    public void Stop()
    {
        State = AudioEngineState.Stopped;
        StatusMessage = "Audio stopped";
    }

    public void UpdateSettings(VoiceSettings settings) => Settings = settings;

    public void SetMonitorEnabled(bool enabled) => MonitorEnabled = enabled;

    public void SetVirtualEnabled(bool enabled) => VirtualEnabled = enabled;

    public void SetMonitorVolume(float volume) => MonitorVolume = volume;

    public RecordingSession? StartRecording()
    {
        RecordingCount++;
        return null;
    }

    public void TestMonitor() => TestCount++;

    public int TestCount { get; private set; }

    public RecordingResult? StopRecording() => null;

    public void Dispose()
    {
    }
}
