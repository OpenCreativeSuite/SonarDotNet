using SonarDotNet.Core;
using SonarDotNet.Core.Audio;
using SonarDotNet.Core.Dsp;

namespace SonarDotNet.Tests;

public sealed class DspTests
{
    [Fact]
    public void NoiseGateClosesBelowThresholdAndOpensForVoice()
    {
        NoiseGate gate = new();
        gate.Configure(new NoiseSettings { ThresholdDb = -40f, ReductionDb = 48f, Suppress = false, Smoothness = 0f }, 48000);

        float quiet = 0f;
        float loud = 0f;
        for (int i = 0; i < 24000; i++)
        {
            quiet = gate.Process(0.001f, 48000);
        }

        Assert.True(MathF.Abs(quiet) < 0.0005f);

        for (int i = 0; i < 24000; i++)
        {
            loud = gate.Process(0.5f, 48000);
        }

        Assert.True(loud > 0.4f);
    }

    [Fact]
    public void ToneCurveBoostsTheSelectedBand()
    {
        ToneSettings tone = new()
        {
            LowCutHz = 40f,
            HighCutHz = 20000f,
            Bands = [
                new() { Label = "Body", Kind = ToneBandKind.Peak, Frequency = 1000f, GainDb = 9f, Q = 1f },
                new() { GainDb = 0f },
                new() { GainDb = 0f },
                new() { GainDb = 0f },
            ],
        };

        double atBand = ToneCurve.ResponseDb(tone, 1000f);
        double farAway = ToneCurve.ResponseDb(tone, 100f);
        Assert.InRange(atBand, 7.5, 10.5);
        Assert.InRange(farAway, -2.0, 2.0);
    }

    [Fact]
    public void CompressorReducesLoudMaterialMoreThanQuietMaterial()
    {
        VoiceProcessor processor = new();
        processor.Configure(new VoiceSettings
        {
            Noise = new NoiseSettings { Enabled = false },
            Tone = new ToneSettings { Enabled = false },
            Dynamics = new DynamicsSettings
            {
                Enabled = true,
                ThresholdDb = -30f,
                Ratio = 6f,
                AttackMs = 1f,
                ReleaseMs = 40f,
                MakeupDb = 0f,
                Limiter = false,
            },
            Character = new CharacterSettings { Enabled = false },
        });

        float[] loud = Fx.Tone(440f, 24000, 0.7f);
        float[] quiet = Fx.Tone(440f, 24000, 0.03f);
        float loudPeak = Peak(processor.Process(loud));
        processor.Reset();
        processor.Configure(processor.Settings);
        float quietPeak = Peak(processor.Process(quiet));

        Assert.True(loudPeak < 0.7f);
        Assert.True(quietPeak > 0.02f);
        Assert.True(loudPeak / quietPeak < 0.7f / 0.03f);
    }

    [Fact]
    public void LimiterKeepsOutputBelowFullScale()
    {
        Limiter limiter = new();
        float peak = 0f;
        for (int i = 0; i < 48000; i++)
        {
            float value = limiter.Process((i % 2 == 0 ? 1.8f : -1.8f), 48000, enabled: true);
            peak = MathF.Max(peak, MathF.Abs(value));
        }

        Assert.True(peak <= 1f);
    }

    [Fact]
    public void PitchShifterRaisesThePerceivedPitch()
    {
        PitchShifter shifter = new();
        shifter.Configure(12f);
        float[] input = Fx.Tone(440f, 48000, 0.3f);
        int crossings = 0;
        float previous = 0f;
        for (int i = 0; i < input.Length; i++)
        {
            float output = shifter.Process(input[i]);
            if (i > 4096 && previous <= 0f && output > 0f)
            {
                crossings++;
            }

            previous = output;
        }

        float frequency = crossings / (float)((input.Length - 4096) / 48000.0);
        Assert.InRange(frequency, 780f, 980f);
    }

    [Fact]
    public void PitchShifterIsATransparentBypassAtZeroSemitones()
    {
        PitchShifter shifter = new();
        shifter.Configure(0f);
        Assert.Equal(0.25f, shifter.Process(0.25f));
        Assert.Equal(-0.5f, shifter.Process(-0.5f));
    }

    [Fact]
    public void CharacterStageSurvivesPitchChanges()
    {
        CharacterProcessor character = new(48000);
        character.Configure(new CharacterSettings { Enabled = true, PitchSemitones = 7f, Warmth = 20f, Space = 8f });
        float peak = 0f;
        for (int i = 0; i < 24000; i++)
        {
            float value = character.Process(MathF.Sin(i * 0.05f) * 0.4f);
            Assert.True(float.IsFinite(value));
            peak = MathF.Max(peak, MathF.Abs(value));
        }

        Assert.True(peak > 0.01f);
    }

    [Fact]
    public void CharacterStageStaysFiniteAndBounded()
    {
        CharacterProcessor character = new(48000);
        character.Configure(new CharacterSettings { Enabled = true, Warmth = 100f, Space = 100f, SpaceMix = 1f });
        float peak = 0f;
        for (int i = 0; i < 48000; i++)
        {
            float value = character.Process(MathF.Sin(i * 0.05f) * 0.8f);
            Assert.True(float.IsFinite(value));
            peak = MathF.Max(peak, MathF.Abs(value));
        }

        Assert.InRange(peak, 0.1f, 4f);
    }

    [Fact]
    public void StreamingResamplerPreservesAConstantAndProducesTheExpectedLength()
    {
        StreamingResampler resampler = new(44100, 48000);
        float[] input = new float[4410];
        Array.Fill(input, 0.25f);
        float[] output = new float[resampler.MaximumOutputCount(input.Length)];
        int written = resampler.Process(input, output);

        Assert.InRange(written, 4798, 4804);
        Assert.All(output.AsSpan(10, written - 20).ToArray(), sample => Assert.InRange(sample, 0.249f, 0.251f));
    }

    [Fact]
    public void AudioSampleConverterDownmixesStereoPcm16()
    {
        byte[] bytes = new byte[8];
        short left = 16384;
        short right = -16384;
        BitConverter.GetBytes(left).CopyTo(bytes, 0);
        BitConverter.GetBytes(right).CopyTo(bytes, 2);
        BitConverter.GetBytes(left).CopyTo(bytes, 4);
        BitConverter.GetBytes(right).CopyTo(bytes, 6);
        float[] mono = new float[2];

        int frames = AudioSampleConverter.ToMono(bytes, new NAudio.Wave.WaveFormat(48000, 16, 2), mono);

        Assert.Equal(2, frames);
        Assert.Equal(0f, mono[0], 3);
        Assert.Equal(0f, mono[1], 3);
    }

    [Fact]
    public void RingBufferWrapsWithoutLosingOrder()
    {
        FloatRingBuffer ring = new(8);
        ring.Write([1f, 2f, 3f, 4f, 5f, 6f]);
        float[] first = new float[4];
        Assert.Equal(4, ring.Read(first));
        Assert.Equal([1f, 2f, 3f, 4f], first);

        ring.Write([7f, 8f, 9f, 10f]);
        float[] second = new float[6];
        Assert.Equal(6, ring.Read(second));
        Assert.Equal([5f, 6f, 7f, 8f, 9f, 10f], second);
    }

    [Fact]
    public void DynamicsCurveLeavesQuietSignalsAloneAndCompressesLoudOnes()
    {
        float quiet = DynamicsCurve.Evaluate(-45f, -24f, 4f, 0f);
        float loud = DynamicsCurve.Evaluate(-6f, -24f, 4f, 0f);

        Assert.Equal(-45f, quiet, 2);
        Assert.Equal(-19.5f, loud, 1);
    }

    [Fact]
    public void VirtualCableDetectionRecognisesCommonDrivers()
    {
        Assert.True(WindowsAudioService.LooksLikeVirtualCable("CABLE Input (VB-Audio Virtual Cable)"));
        Assert.True(WindowsAudioService.LooksLikeVirtualCable("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)"));
        Assert.True(WindowsAudioService.LooksLikeVirtualCable("Line 1 (Virtual Audio Cable)"));
        Assert.False(WindowsAudioService.LooksLikeVirtualCable("Speakers (Realtek(R) Audio)"));
    }

    [Fact]
    public void ToneChainPassesASignal()
    {
        float[] input = Fx.Tone(440f, 4800, 0.5f);
        Biquad highPass = new();
        highPass.HighPass(82f, 0.72f, 48000);
        Biquad lowPass = new();
        lowPass.LowPass(16000f, 0.72f, 48000);
        float hp = 0f;
        float hpLp = 0f;
        float all = 0f;
        Biquad[] bands = new Biquad[4];
        bands[0].LowShelf(150f, -1f, 0.7f, 48000);
        bands[1].Peaking(520f, 0.5f, 0.9f, 48000);
        bands[2].Peaking(2900f, 1.5f, 1.1f, 48000);
        bands[3].HighShelf(9000f, 1f, 0.7f, 48000);
        foreach (float sample in input)
        {
            float a = highPass.Process(sample);
            hp = MathF.Max(hp, MathF.Abs(a));
            float b = lowPass.Process(a);
            hpLp = MathF.Max(hpLp, MathF.Abs(b));
            float c = b;
            for (int i = 0; i < bands.Length; i++)
            {
                c = bands[i].Process(c);
            }

            all = MathF.Max(all, MathF.Abs(c));
        }

        Assert.True(all > 0.1f, $"hp={hp:0.000} hpLp={hpLp:0.000} all={all:0.000}");
    }

    [Fact]
    public void VoiceProcessorConfiguresItsFilters()
    {
        VoiceProcessor processor = new();
        processor.Configure(new VoiceSettings());
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Biquad lowCut = (Biquad)typeof(VoiceProcessor).GetField("_lowCut", flags)!.GetValue(processor)!;
        Biquad highCut = (Biquad)typeof(VoiceProcessor).GetField("_highCut", flags)!.GetValue(processor)!;
        Biquad[] bands = (Biquad[])typeof(VoiceProcessor).GetField("_bands", flags)!.GetValue(processor)!;
        string message = $"low={lowCut.MagnitudeDb(440, 48000):0.000} high={highCut.MagnitudeDb(440, 48000):0.000} " +
            string.Join(",", bands.Select(band => band.MagnitudeDb(440, 48000).ToString("0.000")));
        Assert.True(lowCut.MagnitudeDb(440, 48000) > -3, message);
    }

    [Fact]
    public void DefaultPipelineStillPassesAVoiceSignal()
    {
        VoiceProcessor processor = new();
        processor.Configure(new VoiceSettings());
        float[] input = Fx.Tone(440f, 48000, 0.5f);
        float[] output = processor.Process(input);

        Assert.True(output.Max(sample => MathF.Abs(sample)) > 0.02f);
    }

    private static float Peak(float[] samples)
    {
        float peak = 0f;
        foreach (float sample in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(sample));
        }

        return peak;
    }
}
