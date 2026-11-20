using System.Text.Json;

namespace SonarDotNet.Core;

/// <summary>Small preferences that survive between launches.</summary>
public sealed class SonusSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>The selected capture endpoint id.</summary>
    public string InputDeviceId { get; set; } = "";

    /// <summary>The selected headphone endpoint id.</summary>
    public string MonitorDeviceId { get; set; } = "";

    /// <summary>The selected virtual-cable playback endpoint id.</summary>
    public string VirtualDeviceId { get; set; } = "";

    /// <summary>Whether headphone monitoring was left on.</summary>
    public bool MonitoringEnabled { get; set; }

    /// <summary>Whether the virtual microphone was left on.</summary>
    public bool VirtualEnabled { get; set; } = true;

    /// <summary>Headphone level, 0 to 1.</summary>
    public float MonitorVolume { get; set; } = 0.8f;

    /// <summary>The last selected processing stage.</summary>
    public StudioStage ActiveStage { get; set; } = StudioStage.Tone;

    /// <summary>The name of the last applied preset.</summary>
    public string PresetName { get; set; } = "Natural";

    /// <summary>Whether audio should begin capturing at launch.</summary>
    public bool LiveOnLaunch { get; set; } = true;

    /// <summary>The processing values last used, so edits survive a restart.</summary>
    public VoiceSettings? Voice { get; set; }

    /// <summary>The folder preferences are stored in.</summary>
    public static string Folder
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Sonus");
        }
    }

    /// <summary>The full preferences file path.</summary>
    public static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>Loads preferences, returning defaults when no file exists.</summary>
    public static SonusSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                SonusSettings? loaded = JsonSerializer.Deserialize<SonusSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // A broken preferences file must never stop the application.
        }

        return new SonusSettings();
    }

    /// <summary>Writes preferences, ignoring failures.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Preferences are a convenience; failure is not worth interrupting.
        }
    }
}
