using SonarDotNet.Core.Audio;
using SonarDotNet.Core.Dsp;

namespace SonarDotNet.Core;

/// <summary>
/// The application session: device choice, processing values, presets, recording
/// takes and the small amount of interface state shared by every panel.
/// </summary>
/// <remarks>
/// The session lives in a ref in the root component. Mutating methods call
/// <see cref="Refresh"/> after changing state; the root increments a counter and
/// rebuilds the tree. The audio callback never calls into the session.
/// </remarks>
public sealed class StudioSession : IDisposable
{
    private readonly IAudioService _audio;
    private readonly TakePlayer _player = new();
    private readonly List<VoicePreset> _presets;
    private readonly SonusSettings _preferences;
    private readonly bool _persistPreferences;

    /// <summary>Creates a session over the supplied audio host.</summary>
    public StudioSession(
        IAudioService? audio = null,
        SonusSettings? preferences = null,
        IEnumerable<VoicePreset>? presets = null,
        bool persistPreferences = true)
    {
        _audio = audio ?? new WindowsAudioService();
        _preferences = preferences ?? SonusSettings.Load();
        _persistPreferences = persistPreferences;
        _presets = presets is null ? PresetLibrary.Load() : [.. presets];
        Settings = _preferences.Voice ?? FindPreset("Natural")?.Settings ?? new VoiceSettings();
        PresetName = _preferences.PresetName;
        if (FindPreset(PresetName) is null)
        {
            PresetName = "Natural";
        }

        ActiveStage = _preferences.ActiveStage;
        MonitoringEnabled = _preferences.MonitoringEnabled;
        VirtualEnabled = _preferences.VirtualEnabled;
        MonitorVolume = Math.Clamp(_preferences.MonitorVolume, 0f, 1f);

        _audio.RefreshDevices();
        ResolveDevices();
        LoadRecentTakes();

        _audio.UpdateSettings(Settings);
        _audio.SetMonitorVolume(MonitorVolume);
        _audio.SetMonitorEnabled(MonitoringEnabled);
        _audio.SetVirtualEnabled(VirtualEnabled);

        if (_preferences.LiveOnLaunch && InputDevice is not null)
        {
            StartAudio(silent: true);
        }
        else
        {
            Status = "Ready";
        }
    }

    /// <summary>Raised whenever the interface should rebuild.</summary>
    public Action? Refresh { get; set; }

    /// <summary>The runtime audio host.</summary>
    public IAudioService Audio => _audio;

    /// <summary>Current processing values.</summary>
    public VoiceSettings Settings { get; private set; }

    /// <summary>Available microphones.</summary>
    public IReadOnlyList<AudioDevice> Inputs => _audio.Inputs;

    /// <summary>Available playback endpoints.</summary>
    public IReadOnlyList<AudioDevice> Outputs => _audio.Outputs;

    /// <summary>The selected microphone.</summary>
    public AudioDevice? InputDevice => FindDevice(Inputs, InputDeviceId);

    /// <summary>The selected headphone endpoint.</summary>
    public AudioDevice? MonitorDevice => FindDevice(Outputs, MonitorDeviceId);

    /// <summary>The selected virtual-cable endpoint.</summary>
    public AudioDevice? VirtualDevice => FindDevice(Outputs, VirtualDeviceId);

    /// <summary>The capture-side name other applications should select.</summary>
    public string VirtualMicrophoneName
    {
        get
        {
            AudioDevice? device = VirtualDevice;
            if (device is null)
            {
                return "Not installed";
            }

            string name = device.ShortName;
            return name.Contains("Input", StringComparison.OrdinalIgnoreCase)
                ? name.Replace("Input", "Output", StringComparison.OrdinalIgnoreCase)
                : name;
        }
    }

    /// <summary>The selected microphone id.</summary>
    public string InputDeviceId { get; private set; } = "";

    /// <summary>The selected headphone id.</summary>
    public string MonitorDeviceId { get; private set; } = "";

    /// <summary>The selected virtual-cable id.</summary>
    public string VirtualDeviceId { get; private set; } = "";

    /// <summary>Whether capture is running.</summary>
    public bool IsLive => _audio.IsRunning;

    /// <summary>Whether the virtual microphone output is enabled.</summary>
    public bool VirtualEnabled { get; private set; }

    /// <summary>Whether headphone monitoring is enabled.</summary>
    public bool MonitoringEnabled { get; private set; }

    /// <summary>Headphone level, 0..1.</summary>
    public float MonitorVolume { get; private set; }

    /// <summary>The stage shown in the editor.</summary>
    public StudioStage ActiveStage { get; private set; }

    /// <summary>The current preset name.</summary>
    public string PresetName { get; private set; }

    /// <summary>The preset library, including user presets.</summary>
    public IReadOnlyList<VoicePreset> Presets => _presets;

    /// <summary>The latest status message.</summary>
    public string Status { get; private set; } = "Ready";

    /// <summary>Whether the latest status is an error.</summary>
    public bool StatusIsError => _audio.State == AudioEngineState.Error;

    /// <summary>Live measurements.</summary>
    public AudioTelemetry Telemetry => _audio.Telemetry;

    /// <summary>The current recording take, if any.</summary>
    public RecordingSession? Recording { get; private set; }

    /// <summary>Recent takes, newest first.</summary>
    public List<RecordingResult> Takes { get; } = [];

    /// <summary>The take selected for playback.</summary>
    public RecordingResult? SelectedTake { get; private set; }

    /// <summary>Whether the selected take's processed file is being auditioned.</summary>
    public bool PlayingProcessed { get; private set; } = true;

    /// <summary>The last completed take.</summary>
    public RecordingResult? LastTake => Takes.Count > 0 ? Takes[0] : null;

    /// <summary>Whether the preset popover is open.</summary>
    public bool ShowPresets { get; private set; }

    /// <summary>Whether the audio settings dialog is open.</summary>
    public bool ShowSettings { get; private set; }

    /// <summary>Whether the help dialog is open.</summary>
    public bool ShowHelp { get; private set; }

    /// <summary>Whether the "save preset" form is open.</summary>
    public bool ShowSavePreset { get; private set; }

    /// <summary>The custom dropdown currently open, if any.</summary>
    public ChoiceMenuState? OpenChoice { get; private set; }

    /// <summary>A draft name for a new preset.</summary>
    public string PresetDraftName { get; private set; } = "My Voice";

    /// <summary>Whether text is being typed, which suppresses shortcuts.</summary>
    public bool IsTyping { get; set; }

    /// <summary>Whether the virtual cable was found.</summary>
    public bool HasVirtualCable => VirtualDevice is not null;

    /// <summary>Whether playback is currently running.</summary>
    public bool IsPlaying => _player.IsPlaying;

    /// <summary>Playback position in seconds.</summary>
    public double PlaybackPosition => _player.Position;

    /// <summary>Playback length in seconds.</summary>
    public double PlaybackDuration => _player.Duration;

    /// <summary>Elapsed recording time.</summary>
    public TimeSpan RecordingElapsed => Recording?.Duration ?? TimeSpan.Zero;

    /// <summary>Selects a processing stage.</summary>
    public void SelectStage(StudioStage stage)
    {
        ActiveStage = stage;
        _preferences.ActiveStage = stage;
        Persist();
        Changed();
    }

    /// <summary>Chooses a microphone and restarts the host if it is live.</summary>
    public void SelectInput(string id)
    {
        if (InputDeviceId == id)
        {
            return;
        }

        InputDeviceId = id;
        _preferences.InputDeviceId = id;
        Persist();
        if (IsLive)
        {
            StartAudio();
        }
        else
        {
            Status = InputDevice is null ? "No microphone selected" : $"Ready · {InputDevice.ShortName}";
            Changed();
        }
    }

    /// <summary>Chooses a headphone endpoint and restarts the host if it is live.</summary>
    public void SelectMonitor(string id)
    {
        if (MonitorDeviceId == id)
        {
            return;
        }

        MonitorDeviceId = id;
        _preferences.MonitorDeviceId = id;
        Persist();
        if (IsLive)
        {
            StartAudio();
        }
        else
        {
            Changed();
        }
    }

    /// <summary>Chooses the virtual-cable endpoint and restarts the host if it is live.</summary>
    public void SelectVirtual(string id)
    {
        if (VirtualDeviceId == id)
        {
            return;
        }

        VirtualDeviceId = id;
        _preferences.VirtualDeviceId = id;
        Persist();
        if (IsLive)
        {
            StartAudio();
        }
        else
        {
            Changed();
        }
    }

    /// <summary>Starts capture and both outputs.</summary>
    public void StartAudio(bool silent = false)
    {
        AudioDevice? input = InputDevice;
        if (input is null)
        {
            Status = "Choose a microphone to begin";
            Changed();
            return;
        }

        _audio.UpdateSettings(Settings);
        _audio.SetMonitorVolume(MonitorVolume);
        _audio.SetMonitorEnabled(MonitoringEnabled);
        _audio.SetVirtualEnabled(VirtualEnabled);
        bool started = _audio.Start(input, MonitorDevice, VirtualDevice);
        Status = started
            ? (_audio.StatusMessage.StartsWith("Listening", StringComparison.Ordinal)
                ? $"Live · {input.ShortName}"
                : _audio.StatusMessage)
            : _audio.StatusMessage;
        if (!started && InputDevice is not null)
        {
            Status = _audio.StatusMessage;
        }

        if (silent && started && Status.StartsWith("Live", StringComparison.Ordinal))
        {
            Status = $"Live · {input.ShortName}";
        }

        Changed();
    }

    /// <summary>Starts or stops the live host.</summary>
    public void ToggleLive()
    {
        if (IsLive)
        {
            StopRecording();
            _audio.Stop();
            Status = "Audio stopped";
            Changed();
            return;
        }

        StartAudio();
    }

    /// <summary>Enables or disables headphone monitoring.</summary>
    public void ToggleMonitoring()
    {
        if (MonitorDevice is null)
        {
            Status = "Choose a headphone output first";
            Changed();
            return;
        }

        MonitoringEnabled = !MonitoringEnabled;
        _preferences.MonitoringEnabled = MonitoringEnabled;
        _audio.SetMonitorEnabled(MonitoringEnabled);
        if (MonitoringEnabled && !IsLive)
        {
            StartAudio();
            return;
        }

        Status = MonitoringEnabled ? "Monitoring on" : "Monitoring muted";
        Persist();
        Changed();
    }

    /// <summary>Enables or disables the virtual microphone.</summary>
    public void ToggleVirtual()
    {
        if (VirtualDevice is null)
        {
            ShowSettings = true;
            Status = "Install or select a virtual audio cable";
            Changed();
            return;
        }

        VirtualEnabled = !VirtualEnabled;
        _preferences.VirtualEnabled = VirtualEnabled;
        _audio.SetVirtualEnabled(VirtualEnabled);
        if (VirtualEnabled && !IsLive)
        {
            StartAudio();
            return;
        }

        Status = VirtualEnabled ? "Virtual microphone on" : "Virtual microphone off";
        Persist();
        Changed();
    }

    /// <summary>Enables the virtual microphone as soon as a cable is available.</summary>
    public void EnableVirtualOutput()
    {
        VirtualEnabled = true;
        _preferences.VirtualEnabled = true;
        _audio.SetVirtualEnabled(true);
        Persist();
        Changed();
    }

    /// <summary>Sets the headphone level.</summary>
    public void SetMonitorVolume(float value)
    {
        MonitorVolume = Math.Clamp(value, 0f, 1f);
        _preferences.MonitorVolume = MonitorVolume;
        _audio.SetMonitorVolume(MonitorVolume);
        Persist();
        Changed();
    }

    /// <summary>Plays a short chime through the selected headphones.</summary>
    public void TestMonitor()
    {
        if (MonitorDevice is null)
        {
            Status = "Choose headphones before testing";
            Changed();
            return;
        }

        if (!MonitoringEnabled && IsLive)
        {
            MonitoringEnabled = true;
            _preferences.MonitoringEnabled = true;
            _audio.SetMonitorEnabled(true);
        }

        if (!IsLive)
        {
            StartAudio();
        }

        _audio.TestMonitor();
        Status = $"Monitor test · {MonitorDevice.ShortName}";
        Persist();
        Changed();
    }

    /// <summary>Applies a preset.</summary>
    public void ApplyPreset(VoicePreset preset)
    {
        Settings = (preset.Settings with { Revision = Settings.Revision + 1 });
        PresetName = preset.Name;
        _preferences.PresetName = preset.Name;
        _preferences.Voice = Settings;
        _audio.UpdateSettings(Settings);
        Status = $"Preset · {preset.Name}";
        Persist();
        Changed();
    }

    /// <summary>Saves the current values as a named user preset.</summary>
    public void SavePreset(string name)
    {
        string clean = string.IsNullOrWhiteSpace(name) ? "My Voice" : name.Trim();
        VoicePreset preset = PresetLibrary.Save(clean, "Saved in Sonus", Settings);
        _presets.RemoveAll(item => !item.BuiltIn && string.Equals(item.Name, clean, StringComparison.OrdinalIgnoreCase));
        _presets.Add(preset);
        PresetName = preset.Name;
        _preferences.PresetName = preset.Name;
        ShowSavePreset = false;
        Status = $"Saved preset · {preset.Name}";
        Persist();
        Changed();
    }

    /// <summary>Deletes a user preset.</summary>
    public void DeletePreset(VoicePreset preset)
    {
        if (preset.BuiltIn)
        {
            return;
        }

        PresetLibrary.Delete(preset.Name);
        _presets.Remove(preset);
        if (PresetName == preset.Name)
        {
            PresetName = "Natural";
        }

        Status = $"Deleted preset · {preset.Name}";
        Persist();
        Changed();
    }

    /// <summary>Replaces the tone settings.</summary>
    public void UpdateTone(Func<ToneSettings, ToneSettings> edit) =>
        Apply(Settings with { Tone = edit(Settings.Tone) });

    /// <summary>Replaces the noise settings.</summary>
    public void UpdateNoise(Func<NoiseSettings, NoiseSettings> edit) =>
        Apply(Settings with { Noise = edit(Settings.Noise) });

    /// <summary>Replaces the dynamics settings.</summary>
    public void UpdateDynamics(Func<DynamicsSettings, DynamicsSettings> edit) =>
        Apply(Settings with { Dynamics = edit(Settings.Dynamics) });

    /// <summary>Replaces the character settings.</summary>
    public void UpdateCharacter(Func<CharacterSettings, CharacterSettings> edit) =>
        Apply(Settings with { Character = edit(Settings.Character) });

    /// <summary>Replaces the input trim.</summary>
    public void SetInputGain(float db) =>
        Apply(Settings with { InputGainDb = Math.Clamp(db, -12f, 18f) });

    /// <summary>Publishes a transient message to the status bar.</summary>
    public void SetStatus(string message)
    {
        Status = message;
        Changed();
    }

    /// <summary>Moves one tone-band point.</summary>
    public void MoveToneBand(int index, float frequency, float gainDb)
    {
        if (index < 0 || index >= Settings.Tone.Bands.Length)
        {
            return;
        }

        ToneBand band = Settings.Tone.Bands[index] with
        {
            Frequency = Math.Clamp(frequency, 40f, 18000f),
            GainDb = Math.Clamp(gainDb, -ToneCurve.RangeDb, ToneCurve.RangeDb),
        };
        UpdateTone(tone => tone.WithBand(index, band));
    }

    /// <summary>Toggles one tone band.</summary>
    public void ToggleToneBand(int index)
    {
        if (index < 0 || index >= Settings.Tone.Bands.Length)
        {
            return;
        }

        ToneBand band = Settings.Tone.Bands[index];
        UpdateTone(tone => tone.WithBand(index, band with { Enabled = !band.Enabled }));
    }

    /// <summary>Restores the selected stage to its neutral values.</summary>
    public void ResetStage()
    {
        Settings = ActiveStage switch
        {
            StudioStage.Noise => Settings with { Noise = new NoiseSettings() },
            StudioStage.Tone => Settings with { Tone = new ToneSettings() },
            StudioStage.Dynamics => Settings with { Dynamics = new DynamicsSettings() },
            StudioStage.Character => Settings with { Character = new CharacterSettings() },
            _ => Settings,
        };
        Apply(Settings);
        Status = "Stage reset";
        Changed();
    }

    /// <summary>Starts a recording take, starting the host first if necessary.</summary>
    public void StartRecording()
    {
        if (!IsLive)
        {
            StartAudio();
        }

        RecordingSession? session = _audio.StartRecording();
        if (session is null)
        {
            Status = _audio.StatusMessage;
            Changed();
            return;
        }

        Recording = session;
        SelectedTake = null;
        Status = "Recording · raw and processed WAV";
        Changed();
    }

    /// <summary>Stops the current take and adds it to the recent list.</summary>
    public void StopRecording()
    {
        if (Recording is null)
        {
            return;
        }

        RecordingResult? result = _audio.StopRecording();
        Recording = null;
        if (result is not null)
        {
            Takes.Insert(0, result);
            SelectedTake = result;
            PlayingProcessed = true;
            TrimTakes();
            Status = $"Take saved · {result.Duration:mm\\:ss}";
        }

        Changed();
    }

    /// <summary>Starts or stops a recorded take for comparison.</summary>
    public void ToggleTake(RecordingResult take, bool processed)
    {
        string path = processed ? take.ProcessedPath : take.RawPath;
        if (SelectedTake == take && PlayingProcessed == processed && IsPlaying)
        {
            _player.Pause();
            Changed();
            return;
        }

        SelectedTake = take;
        PlayingProcessed = processed;
        _player.Play(path);
        Status = processed ? "Playing processed take" : "Playing raw take";
        Changed();
    }

    /// <summary>Stops playback and rewinds.</summary>
    public void StopPlayback()
    {
        _player.Stop();
        Changed();
    }

    /// <summary>Pauses or resumes the selected take.</summary>
    public void TogglePlayback()
    {
        if (SelectedTake is null)
        {
            return;
        }

        if (IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            _player.Play(PlayingProcessed ? SelectedTake.ProcessedPath : SelectedTake.RawPath);
        }

        Changed();
    }

    /// <summary>Seeks within the selected take.</summary>
    public void SeekPlayback(float fraction)
    {
        _player.Seek(fraction);
        Changed();
    }

    /// <summary>Copies the currently processed values into a new preset draft.</summary>
    public void OpenSavePreset()
    {
        PresetDraftName = PresetName.StartsWith("My", StringComparison.Ordinal) ? "My Voice" : $"{PresetName} Copy";
        ShowSavePreset = true;
        Changed();
    }

    /// <summary>Sets the draft preset name.</summary>
    public void SetPresetDraft(string name)
    {
        PresetDraftName = name;
        Changed();
    }

    /// <summary>Toggles the preset popover.</summary>
    public void TogglePresets()
    {
        ShowPresets = !ShowPresets;
        ShowSavePreset = false;
        Changed();
    }

    /// <summary>Toggles the settings dialog.</summary>
    public void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        Changed();
    }

    /// <summary>Toggles the help dialog.</summary>
    public void ToggleHelp()
    {
        ShowHelp = !ShowHelp;
        Changed();
    }

    /// <summary>Opens a custom choice menu anchored below a control.</summary>
    public void OpenChoiceMenu(string title, string icon, string selected, IReadOnlyList<ChoiceOption> options, Action<string> choose, float x, float y, float width)
    {
        OpenChoice = new ChoiceMenuState(title, icon, selected, options, choose, x, y, width);
        ShowPresets = false;
        ShowSavePreset = false;
        Changed();
    }

    /// <summary>Applies the selected value from the current dropdown.</summary>
    public void SelectChoice(string value)
    {
        ChoiceMenuState? menu = OpenChoice;
        OpenChoice = null;
        menu?.Choose(value);
        Changed();
    }

    /// <summary>Closes the current dropdown.</summary>
    public void CloseChoice()
    {
        if (OpenChoice is null)
        {
            return;
        }

        OpenChoice = null;
        Changed();
    }

    /// <summary>Closes every floating layer.</summary>
    public void CloseLayers()
    {
        bool changed = ShowPresets || ShowSettings || ShowHelp || ShowSavePreset || OpenChoice is not null;
        ShowPresets = false;
        ShowSettings = false;
        ShowHelp = false;
        ShowSavePreset = false;
        OpenChoice = null;
        if (changed)
        {
            Changed();
        }
    }

    /// <summary>Re-enumerates endpoints and repairs selections.</summary>
    public void RefreshDevices()
    {
        bool hadCable = VirtualDevice is not null;
        _audio.RefreshDevices();
        ResolveDevices();
        if (!hadCable && VirtualDevice is not null && !VirtualEnabled)
        {
            VirtualEnabled = true;
            _preferences.VirtualEnabled = true;
            _audio.SetVirtualEnabled(true);
        }

        Status = IsLive ? Status : "Devices refreshed";
        if (IsLive)
        {
            StartAudio(silent: true);
        }
        else
        {
            Persist();
            Changed();
        }
    }

    /// <summary>Saves preferences and reports a change.</summary>
    private void Apply(VoiceSettings settings)
    {
        Settings = settings.Next();
        _preferences.Voice = Settings;
        _audio.UpdateSettings(Settings);
        Persist();
        Changed();
    }

    private void Changed() => Refresh?.Invoke();

    private void Persist()
    {
        if (!_persistPreferences)
        {
            return;
        }

        _preferences.InputDeviceId = InputDeviceId;
        _preferences.MonitorDeviceId = MonitorDeviceId;
        _preferences.VirtualDeviceId = VirtualDeviceId;
        _preferences.MonitoringEnabled = MonitoringEnabled;
        _preferences.VirtualEnabled = VirtualEnabled;
        _preferences.MonitorVolume = MonitorVolume;
        _preferences.ActiveStage = ActiveStage;
        _preferences.PresetName = PresetName;
        _preferences.Voice = Settings;
        _preferences.Save();
    }

    private void ResolveDevices()
    {
        AudioDevice? input = FindDevice(Inputs, _preferences.InputDeviceId);
        if (input is null)
        {
            input = Inputs.FirstOrDefault(device => device.IsDefault && !device.IsVirtualCable)
                ?? Inputs.FirstOrDefault(device => !device.IsVirtualCable)
                ?? Inputs.FirstOrDefault(device => device.IsDefault)
                ?? Inputs.FirstOrDefault();
        }

        InputDeviceId = input?.Id ?? "";

        AudioDevice? monitor = FindDevice(Outputs, _preferences.MonitorDeviceId);
        if (monitor?.IsVirtualCable == true)
        {
            monitor = null;
        }

        monitor ??= Outputs.FirstOrDefault(device => device.IsDefault && !device.IsVirtualCable)
            ?? Outputs.FirstOrDefault(device => !device.IsVirtualCable)
            ?? Outputs.FirstOrDefault();
        MonitorDeviceId = monitor?.Id ?? "";

        AudioDevice? cable = FindDevice(Outputs, _preferences.VirtualDeviceId);
        if (cable is not null && !cable.IsVirtualCable)
        {
            cable = null;
        }

        cable ??= FindVirtualCable();
        VirtualDeviceId = cable?.Id ?? "";
        if (cable is null)
        {
            VirtualEnabled = false;
        }
    }

    private AudioDevice? FindVirtualCable() =>
        Outputs.Where(device => device.IsVirtualCable)
            .OrderByDescending(device => device.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(device => device.Name.Contains("Input", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

    private VoicePreset? FindPreset(string name) =>
        _presets.FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    private static AudioDevice? FindDevice(IReadOnlyList<AudioDevice> devices, string id) =>
        string.IsNullOrEmpty(id) ? null : devices.FirstOrDefault(device => device.Id == id);

    private void LoadRecentTakes()
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Sonus Recordings");
            if (!Directory.Exists(folder))
            {
                return;
            }

            IEnumerable<string> files = Directory.EnumerateFiles(folder, "*_raw.wav")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(4);
            foreach (string raw in files)
            {
                string processed = raw.Replace("_raw.wav", "_processed.wav", StringComparison.OrdinalIgnoreCase);
                if (File.Exists(processed))
                {
                    TimeSpan duration = TimeSpan.Zero;
                    try
                    {
                        using NAudio.Wave.WaveFileReader reader = new(raw);
                        duration = reader.TotalTime;
                    }
                    catch
                    {
                        // A stale or half-written take is still listed.
                    }

                    Takes.Add(new RecordingResult(raw, processed, duration));
                }
            }
        }
        catch
        {
            // Recent takes are a convenience, never a reason to fail startup.
        }
    }

    private void TrimTakes()
    {
        while (Takes.Count > 6)
        {
            Takes.RemoveAt(Takes.Count - 1);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopRecording();
        _audio.Dispose();
        _player.Dispose();
    }
}
