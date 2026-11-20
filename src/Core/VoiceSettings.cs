namespace SonarDotNet.Core;

/// <summary>The four conceptual processing stages shown in the interface.</summary>
public enum StudioStage
{
    Clean,
    Tone,
    Dynamics,
    Voice,
    Space,
}

/// <summary>One movable point on the tone curve.</summary>
public enum ToneBandKind
{
    LowShelf,
    Peak,
    HighShelf,
}

/// <summary>One band of the voice tone shaper.</summary>
public sealed record ToneBand
{
    /// <summary>The short name shown beside the point.</summary>
    public string Label { get; init; } = "Body";

    /// <summary>The filter shape.</summary>
    public ToneBandKind Kind { get; init; } = ToneBandKind.Peak;

    /// <summary>Centre or corner frequency in hertz.</summary>
    public float Frequency { get; init; } = 500f;

    /// <summary>Boost or cut in decibels.</summary>
    public float GainDb { get; init; }

    /// <summary>Filter width. Peaking bands only.</summary>
    public float Q { get; init; } = 0.9f;

    /// <summary>Whether this band participates in the curve.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>Background noise treatment.</summary>
public sealed record NoiseSettings
{
    /// <summary>Whether the stage is active.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The level below which sound is treated as room noise.</summary>
    public float ThresholdDb { get; init; } = -46f;

    /// <summary>How far the gate closes, in decibels.</summary>
    public float ReductionDb { get; init; } = 42f;

    /// <summary>How gradually the gate opens and closes.</summary>
    public float Smoothness { get; init; } = 0.55f;

    /// <summary>Adds gentle expansion under the threshold.</summary>
    public bool Suppress { get; init; } = true;
}

/// <summary>The interactive frequency response.</summary>
public sealed record ToneSettings
{
    /// <summary>Whether the stage is active.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Low-frequency roll-off in hertz.</summary>
    public float LowCutHz { get; init; } = 82f;

    /// <summary>High-frequency roll-off in hertz.</summary>
    public float HighCutHz { get; init; } = 16000f;

    /// <summary>The four draggable points.</summary>
    public ToneBand[] Bands { get; init; } =
    [
        new() { Label = "Low", Kind = ToneBandKind.LowShelf, Frequency = 150f, GainDb = -1f, Q = 0.7f },
        new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 520f, GainDb = 0.5f, Q = 0.9f },
        new() { Label = "Presence", Kind = ToneBandKind.Peak, Frequency = 2900f, GainDb = 1.5f, Q = 1.1f },
        new() { Label = "Air", Kind = ToneBandKind.HighShelf, Frequency = 9000f, GainDb = 1f, Q = 0.7f },
    ];

    /// <summary>Returns a copy with one band replaced.</summary>
    public ToneSettings WithBand(int index, ToneBand band)
    {
        if (index < 0 || index >= Bands.Length)
        {
            return this;
        }

        ToneBand[] copy = [.. Bands];
        copy[index] = band;
        return this with { Bands = copy };
    }
}

/// <summary>Consistency and loudness treatment.</summary>
public sealed record DynamicsSettings
{
    /// <summary>Whether the stage is active.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The level where compression begins.</summary>
    public float ThresholdDb { get; init; } = -24f;

    /// <summary>How firmly peaks above the threshold are held.</summary>
    public float Ratio { get; init; } = 3f;

    /// <summary>How quickly the compressor responds.</summary>
    public float AttackMs { get; init; } = 12f;

    /// <summary>How quickly it lets go.</summary>
    public float ReleaseMs { get; init; } = 150f;

    /// <summary>Make-up gain after compression.</summary>
    public float MakeupDb { get; init; } = 2f;

    /// <summary>Whether the output limiter is engaged.</summary>
    public bool Limiter { get; init; } = true;
}

/// <summary>Personality: harmonic warmth and a little space.</summary>
public sealed record CharacterSettings
{
    /// <summary>Whether the voice-shaping stage is active.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether reverb and echo are active.</summary>
    public bool SpaceEnabled { get; init; } = true;

    /// <summary>Saturation amount, 0 to 100.</summary>
    public float Warmth { get; init; } = 16f;

    /// <summary>Reverb send, 0 to 100.</summary>
    public float Space { get; init; } = 8f;

    /// <summary>Pitch change in semitones, -12 to +12.</summary>
    public float PitchSemitones { get; init; }

    /// <summary>Formant shift in semitones. Positive is a smaller vocal tract.</summary>
    public float FormantSemitones { get; init; }

    /// <summary>Resonance focus, -1 (dark) to +1 (forward).</summary>
    public float Resonance { get; init; }

    /// <summary>High-frequency character, -1 (dark) to +1 (bright).</summary>
    public float Brightness { get; init; }

    /// <summary>Echo delay in milliseconds.</summary>
    public float DelayMs { get; init; } = 240f;

    /// <summary>Echo feedback, 0 to 1.</summary>
    public float DelayFeedback { get; init; } = 0.28f;

    /// <summary>Echo wet level, 0 to 1.</summary>
    public float DelayMix { get; init; }

    /// <summary>Wet/dry balance of the reverb, 0 to 1.</summary>
    public float SpaceMix { get; init; } = 0.55f;
}

/// <summary>The complete, immutable voice-processing configuration.</summary>
public sealed record VoiceSettings
{
    /// <summary>Input trim before the noise stage.</summary>
    public float InputGainDb { get; init; }

    /// <summary>Noise stage settings.</summary>
    public NoiseSettings Noise { get; init; } = new();

    /// <summary>Tone stage settings.</summary>
    public ToneSettings Tone { get; init; } = new();

    /// <summary>Dynamics stage settings.</summary>
    public DynamicsSettings Dynamics { get; init; } = new();

    /// <summary>Character stage settings.</summary>
    public CharacterSettings Character { get; init; } = new();

    /// <summary>A counter used by the editor to know when to redraw.</summary>
    public int Revision { get; init; }

    /// <summary>Returns a copy with a new revision number.</summary>
    public VoiceSettings Next() => this with { Revision = Revision + 1 };
}
