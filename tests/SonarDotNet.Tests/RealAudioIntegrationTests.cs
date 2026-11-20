using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SonarDotNet.Core;
using SonarDotNet.Core.Audio;

namespace SonarDotNet.Tests;

/// <summary>
/// Optional end-to-end check against the real Windows endpoints. It is skipped
/// unless SONUS_AUDIO_TEST=1 and VB-CABLE is installed, so normal test runs stay
/// hardware-independent.
/// </summary>
public sealed class RealAudioIntegrationTests
{
    [Fact]
    public void WindowsAudioServiceRoutesProcessedAudioToTheVirtualCable()
    {
        if (Environment.GetEnvironmentVariable("SONUS_AUDIO_TEST") != "1")
        {
            return;
        }

        using MMDeviceEnumerator enumerator = new();
        MMDevice? input = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("Microphone (K66)", StringComparison.Ordinal));
        MMDevice? monitor = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("Speakers (K66)", StringComparison.Ordinal));
        MMDevice? cable = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("CABLE Input", StringComparison.Ordinal));
        MMDevice? cableOutput = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("CABLE Output", StringComparison.Ordinal));
        MMDevice? toneDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("Speakers (K66)", StringComparison.Ordinal));
        if (input is null || monitor is null || cable is null || cableOutput is null || toneDevice is null)
        {
            return;
        }

        using WindowsAudioService service = new();
        service.SetMonitorEnabled(false);
        service.SetVirtualEnabled(true);
        service.UpdateSettings(new VoiceSettings
        {
            InputGainDb = 18f,
            Noise = new NoiseSettings { Enabled = false },
            Tone = new ToneSettings { Enabled = false },
            Dynamics = new DynamicsSettings { Enabled = false, Limiter = false },
            Character = new CharacterSettings { Enabled = false },
        });

        AudioDevice inputInfo = new(input.ID, input.FriendlyName, AudioDeviceKind.Input);
        AudioDevice monitorInfo = new(monitor.ID, monitor.FriendlyName, AudioDeviceKind.Output);
        AudioDevice cableInfo = new(cable.ID, cable.FriendlyName, AudioDeviceKind.Output, IsVirtualCable: true);
        Assert.True(service.Start(inputInfo, monitorInfo, cableInfo));

        float peak = CapturePeak(cableOutput, toneDevice, service);
        float inputPeak = service.Telemetry.InputPeak;
        string status = service.StatusMessage;
        service.Stop();

        Assert.True(inputPeak > 0.0005f, $"Input peak was {inputPeak:0.0000}; status={status}");
        Assert.True(peak > 0.0005f, $"Cable output peak was {peak:0.0000}; status={status}");
    }

    [Fact]
    public void WindowsAudioServiceRoutesProcessedAudioToTheMonitorOutput()
    {
        if (Environment.GetEnvironmentVariable("SONUS_AUDIO_TEST") != "1")
        {
            return;
        }

        using MMDeviceEnumerator enumerator = new();
        MMDevice? input = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("Microphone (K66)", StringComparison.Ordinal));
        MMDevice? monitor = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("CABLE Input", StringComparison.Ordinal));
        MMDevice? monitorOutput = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("CABLE Output", StringComparison.Ordinal));
        MMDevice? toneDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName.Contains("Speakers (K66)", StringComparison.Ordinal));
        if (input is null || monitor is null || monitorOutput is null || toneDevice is null)
        {
            return;
        }

        using WindowsAudioService service = new();
        service.SetMonitorEnabled(true);
        service.SetVirtualEnabled(false);
        service.SetMonitorVolume(1f);
        service.UpdateSettings(new VoiceSettings
        {
            InputGainDb = 18f,
            Noise = new NoiseSettings { Enabled = false },
            Tone = new ToneSettings { Enabled = false },
            Dynamics = new DynamicsSettings { Enabled = false, Limiter = false },
            Character = new CharacterSettings { Enabled = false },
        });

        AudioDevice inputInfo = new(input.ID, input.FriendlyName, AudioDeviceKind.Input);
        AudioDevice monitorInfo = new(monitor.ID, monitor.FriendlyName, AudioDeviceKind.Output);
        Assert.True(service.Start(inputInfo, monitorInfo, null));

        float peak = CapturePeak(monitorOutput, toneDevice, service);
        float inputPeak = service.Telemetry.InputPeak;
        string status = service.StatusMessage;
        service.Stop();

        Assert.True(inputPeak > 0.0005f, $"Input peak was {inputPeak:0.0000}; status={status}");
        Assert.True(peak > 0.0005f, $"Monitor output peak was {peak:0.0000}; status={status}");
    }

    private static float CapturePeak(MMDevice captureDevice, MMDevice toneDevice, WindowsAudioService service)
    {
        float peak = 0f;
        using WasapiCapture capture = new(captureDevice, true, 20);
        capture.DataAvailable += (_, args) =>
        {
            WaveFormat format = capture.WaveFormat;
            int frames = args.BytesRecorded / format.BlockAlign;
            for (int frame = 0; frame < frames; frame++)
            {
                peak = MathF.Max(peak, MathF.Abs(BitConverter.ToSingle(args.Buffer, frame * format.BlockAlign)));
            }
        };
        capture.StartRecording();
        Thread.Sleep(250);

        SignalGenerator tone = new(48000, 2)
        {
            Frequency = 440f,
            Gain = 0.4f,
            Type = SignalGeneratorType.Sin,
        };
        using (WasapiOut player = new(toneDevice, AudioClientShareMode.Shared, true, 50))
        {
            player.Init(tone);
            player.Play();
            Thread.Sleep(1200);
            player.Stop();
        }

        Thread.Sleep(300);
        capture.StopRecording();
        return peak;
    }
}
