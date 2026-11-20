using System.Text.Json;

namespace SonarDotNet.Core;

/// <summary>A named voice configuration.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Description">A one-line explanation of the sound.</param>
/// <param name="Settings">The processing values.</param>
/// <param name="BuiltIn">Whether this preset ships with the application.</param>
public sealed record VoicePreset(string Name, string Description, VoiceSettings Settings, bool BuiltIn);

/// <summary>Built-in sounds and the user's local preset library.</summary>
public static class PresetLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>The presets every installation starts with.</summary>
    public static IReadOnlyList<VoicePreset> BuiltIn { get; } = CreateBuiltIn();

    /// <summary>The folder user presets are written to.</summary>
    public static string Folder
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Sonus", "Presets");
        }
    }

    /// <summary>Loads the built-ins followed by any user presets that parse.</summary>
    public static List<VoicePreset> Load()
    {
        List<VoicePreset> presets = [.. BuiltIn];
        try
        {
            if (Directory.Exists(Folder))
            {
                foreach (string path in Directory.EnumerateFiles(Folder, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        VoicePreset? preset = JsonSerializer.Deserialize<VoicePreset>(File.ReadAllText(path), JsonOptions);
                        if (preset?.Settings is not null && !string.IsNullOrWhiteSpace(preset.Name))
                        {
                            presets.Add(preset with { BuiltIn = false });
                        }
                    }
                    catch
                    {
                        // One damaged preset must not hide the rest of the library.
                    }
                }
            }
        }
        catch
        {
            // Presets are a convenience; an unreadable folder is not fatal.
        }

        return presets;
    }

    /// <summary>Writes a user preset, replacing a preset with the same name.</summary>
    public static VoicePreset Save(string name, string description, VoiceSettings settings)
    {
        string clean = string.IsNullOrWhiteSpace(name) ? "My Voice" : name.Trim();
        VoicePreset preset = new(clean, description, settings with { Revision = 0 }, false);
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath(clean), JsonSerializer.Serialize(preset, JsonOptions));
        }
        catch
        {
            // The caller keeps the preset in memory even if the disk is read-only.
        }

        return preset;
    }

    /// <summary>Deletes a user preset by name.</summary>
    public static void Delete(string name)
    {
        try
        {
            File.Delete(FilePath(name));
        }
        catch
        {
            // A missing or locked file is harmless here.
        }
    }

    /// <summary>Builds the stable file path for a preset name.</summary>
    public static string FilePath(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name.Trim().Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "My Voice";
        }

        return Path.Combine(Folder, safe + ".json");
    }

    private static VoicePreset[] CreateBuiltIn() =>
    [
        new(
            "Natural",
            "A clean, everyday voice with a gentle lift.",
            new VoiceSettings(),
            true),
        new(
            "Broadcast",
            "Forward and steady, with the room pushed back.",
            new VoiceSettings
            {
                Noise = new NoiseSettings { ThresholdDb = -42f, ReductionDb = 48f, Suppress = true },
                Tone = new ToneSettings
                {
                    LowCutHz = 105f,
                    HighCutHz = 15500f,
                    Bands = [
                        new() { Label = "Low", Kind = ToneBandKind.LowShelf, Frequency = 150f, GainDb = -2.5f, Q = 0.7f },
                        new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 430f, GainDb = -1f, Q = 1f },
                        new() { Label = "Presence", Kind = ToneBandKind.Peak, Frequency = 3200f, GainDb = 3.5f, Q = 1.15f },
                        new() { Label = "Air", Kind = ToneBandKind.HighShelf, Frequency = 8500f, GainDb = 2f, Q = 0.7f },
                    ],
                },
                Dynamics = new DynamicsSettings { ThresholdDb = -27f, Ratio = 4f, AttackMs = 8f, ReleaseMs = 130f, MakeupDb = 4f },
                Character = new CharacterSettings { Warmth = 20f, Space = 5f },
            },
            true),
        new(
            "Warm",
            "Rounded lows and a soft top, made for close listening.",
            new VoiceSettings
            {
                Noise = new NoiseSettings { ThresholdDb = -49f, ReductionDb = 34f, Smoothness = 0.7f },
                Tone = new ToneSettings
                {
                    LowCutHz = 65f,
                    HighCutHz = 14500f,
                    Bands = [
                        new() { Label = "Low", Kind = ToneBandKind.LowShelf, Frequency = 180f, GainDb = 3f, Q = 0.7f },
                        new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 650f, GainDb = 1.5f, Q = 0.85f },
                        new() { Label = "Presence", Kind = ToneBandKind.Peak, Frequency = 2600f, GainDb = 0.5f, Q = 1f },
                        new() { Label = "Air", Kind = ToneBandKind.HighShelf, Frequency = 7800f, GainDb = -1f, Q = 0.7f },
                    ],
                },
                Dynamics = new DynamicsSettings { ThresholdDb = -26f, Ratio = 2.6f, AttackMs = 18f, ReleaseMs = 180f, MakeupDb = 2.5f },
                Character = new CharacterSettings { Warmth = 32f, Space = 12f },
            },
            true),
        new(
            "Crisp",
            "Clear consonants and air for streaming and tutorials.",
            new VoiceSettings
            {
                Noise = new NoiseSettings { ThresholdDb = -44f, ReductionDb = 44f },
                Tone = new ToneSettings
                {
                    LowCutHz = 120f,
                    HighCutHz = 17500f,
                    Bands = [
                        new() { Label = "Low", Kind = ToneBandKind.LowShelf, Frequency = 150f, GainDb = -2f, Q = 0.7f },
                        new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 480f, GainDb = -0.5f, Q = 1f },
                        new() { Label = "Presence", Kind = ToneBandKind.Peak, Frequency = 3800f, GainDb = 4f, Q = 1.2f },
                        new() { Label = "Air", Kind = ToneBandKind.HighShelf, Frequency = 10000f, GainDb = 3.5f, Q = 0.7f },
                    ],
                },
                Dynamics = new DynamicsSettings { ThresholdDb = -30f, Ratio = 3.2f, AttackMs = 6f, ReleaseMs = 110f, MakeupDb = 4.5f },
                Character = new CharacterSettings { Warmth = 9f, Space = 4f },
            },
            true),
        new(
            "Podcast",
            "Comfortable for long sessions, with a low, even level.",
            new VoiceSettings
            {
                Noise = new NoiseSettings { ThresholdDb = -47f, ReductionDb = 40f, Smoothness = 0.65f },
                Tone = new ToneSettings
                {
                    LowCutHz = 90f,
                    HighCutHz = 15000f,
                    Bands = [
                        new() { Label = "Low", Kind = ToneBandKind.LowShelf, Frequency = 170f, GainDb = 1.5f, Q = 0.7f },
                        new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 560f, GainDb = 1f, Q = 0.9f },
                        new() { Label = "Presence", Kind = ToneBandKind.Peak, Frequency = 3000f, GainDb = 2f, Q = 1.1f },
                        new() { Label = "Air", Kind = ToneBandKind.HighShelf, Frequency = 9000f, GainDb = -0.5f, Q = 0.7f },
                    ],
                },
                Dynamics = new DynamicsSettings { ThresholdDb = -31f, Ratio = 3.8f, AttackMs = 10f, ReleaseMs = 220f, MakeupDb = 5f },
                Character = new CharacterSettings { Warmth = 18f, Space = 7f },
            },
            true),
    ];
}
