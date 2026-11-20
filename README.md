# Sonus.NET

**A local-first voice studio for Windows.** Sonus listens to your microphone, shapes the sound through four visual stages, monitors it in headphones and exposes the result as a virtual microphone that Discord, OBS, Teams, games and recording tools can select.

There is no account, cloud processing, subscription or telemetry. Audio stays on the machine.

![Sonus.NET main window](docs/screenshots/main.png)

## What it does

- **Live microphone capture** with device selection, input trim, level metering and clipping hold.
- **Headphone monitoring** with an independent volume control and a **Test** button, kept separate from the virtual output.
- **A real virtual microphone** through the signed VB-CABLE driver. Sonus detects it automatically, routes the processed signal to `CABLE Input`, and other applications choose `CABLE Output` as their microphone.
- **Four visual processing stages** — Noise, Tone, Dynamics and Character — rather than a wall of plugins.
- **An interactive tone curve.** Drag low, body, presence and air points directly on the frequency-response graph.
- **A visual compressor curve** with draggable knee and ratio handles, a limiter and live gain-reduction feedback.
- **Character** with pitch shifting, harmonic warmth and a small, short room.
- **Raw and processed recording at the same time.** Every take writes two WAV files so they can be compared directly.
- **Local presets** in plain JSON, plus session restore for devices and processing values.
- **Graceful device failure.** If a microphone disappears, capture stops cleanly and the interface reports it.

## Get started

1. Run Sonus.NET.
2. Choose a microphone and headphones in the device bar.
3. Open **Settings → Virtual microphone → Download & install** once, if VB-CABLE is not already present. Complete the official VB-CABLE installer and restart Windows.
4. Enable **Monitoring** to hear yourself. Headphones are strongly recommended.
5. Enable the **Virtual microphone** switch. In Discord/OBS/Teams, select **CABLE Output (VB-Audio Virtual Cable)** as the microphone.
6. Shape the sound in the four stages. Record a take to compare raw and processed versions.

The virtual microphone output and the headphone monitor consume the same processed signal. They are independent consumers, not two processing chains.

### Why a driver?

Windows does not provide a user-mode API for creating a recording endpoint. Applications cannot invent a microphone that other applications can enumerate. Sonus therefore uses the free, signed **VB-CABLE** virtual audio device and keeps the driver separate from the DSP engine. The app detects the cable, selects its playback side automatically, and the setup sheet offers the official package when it is missing.

VB-CABLE is donationware by [VB-Audio](https://vb-audio.com/Cable/). Sonus does not bundle or modify the driver; the setup sheet downloads the official package and runs the official installer.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build SonarDotNet.csproj
dotnet run
```

The desktop host accepts a headless frame capture:

```sh
dotnet run -- --screenshot frame.bmp
```

Run the automated tests with:

```sh
dotnet test tests/SonarDotNet.Tests/SonarDotNet.Tests.csproj
```

The suite covers the gate, EQ response, compressor curve, limiter, character stage, resampling, device-format conversion, ring buffers, WAV recording and every major interface component through Donkeh's headless harness. An optional end-to-end hardware test can be run with `SONUS_AUDIO_TEST=1` when VB-CABLE and a loopback-capable input are installed.

## Project layout

```text
Program.cs                       application root, overlays and shortcuts
src/Core/
  VoiceSettings.cs               immutable processing configuration
  StudioSession.cs               devices, presets, recording and view state
  Dsp/                           biquads, gate, compressor, limiter,
                                 character stage, tone and dynamics curves
  Audio/                         WASAPI host, format conversion, resampling,
                                 output sinks, telemetry, WAV recording
src/Components/
  App/App.css                    the complete visual identity
  Chrome/                        top bar, device bar, preset list, menus
  Stages/                        stage rail, editors and interactive graphs
  Live/                          scope, meters and recording transport
  Dialogs/                       settings, help, save-preset, VB-CABLE setup
tests/SonarDotNet.Tests/         DSP, recording and component tests
```

The DSP engine has no dependency on the interface and can process supplied buffers without audio hardware. The processing chain runs at 48 kHz mono; device formats and sample rates are converted at the boundary. Monitoring and virtual output use independent WASAPI render consumers.

## Privacy

Sonus does not create an account, contact a server for audio processing, or collect usage data. The only network action is the optional VB-CABLE package download from VB-Audio when the user explicitly asks for it. Recordings are written to `Documents\Sonus Recordings`; presets and preferences live under `%LOCALAPPDATA%\Sonus`.

## Status

Implemented: microphone capture, device changes, monitoring, virtual output through VB-CABLE, the four-stage DSP chain, interactive graphs, level and clip feedback, simultaneous raw/processed WAV recording, playback, local presets and in-app VB-CABLE setup.

Planned next: pitch and formant controls in Character, additional noise reduction, a spectrum view, and richer comparison between takes.
