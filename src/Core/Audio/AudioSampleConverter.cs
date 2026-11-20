using NAudio.Wave;

namespace SonarDotNet.Core.Audio;

/// <summary>Converts interleaved device buffers into mono float samples.</summary>
public static class AudioSampleConverter
{
    private static readonly Guid IeeeFloatSubtype = new("00000003-0000-0010-8000-00aa00389b71");

    /// <summary>Returns whether a wave format stores IEEE float samples.</summary>
    public static bool IsFloat(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return true;
        }

        return format is WaveFormatExtensible extensible && extensible.SubFormat == IeeeFloatSubtype;
    }

    /// <summary>
    /// Converts a captured buffer to mono float frames. Returns the number of
    /// frames written to <paramref name="destination"/>.
    /// </summary>
    public static int ToMono(ReadOnlySpan<byte> source, WaveFormat format, Span<float> destination)
    {
        int channels = Math.Max(1, format.Channels);
        int blockAlign = format.BlockAlign;
        if (blockAlign <= 0 || source.Length < blockAlign)
        {
            return 0;
        }

        int frames = Math.Min(source.Length / blockAlign, destination.Length);
        bool isFloat = IsFloat(format);
        int bits = format.BitsPerSample;

        for (int frame = 0; frame < frames; frame++)
        {
            int offset = frame * blockAlign;
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                int sampleOffset = offset + (channel * bits / 8);
                sum += ReadSample(source, sampleOffset, bits, isFloat);
            }

            destination[frame] = sum / channels;
        }

        return frames;
    }

    private static float ReadSample(ReadOnlySpan<byte> source, int offset, int bits, bool isFloat)
    {
        switch (bits)
        {
            case 8:
                return (source[offset] - 128) / 128f;
            case 16:
                return BitConverter.ToInt16(source.Slice(offset, 2)) / 32768f;
            case 24:
            {
                int value = source[offset] | (source[offset + 1] << 8) | (source[offset + 2] << 16);
                if ((value & 0x800000) != 0)
                {
                    value |= unchecked((int)0xFF000000);
                }

                return value / 8388608f;
            }
            case 32:
                if (isFloat)
                {
                    return BitConverter.ToSingle(source.Slice(offset, 4));
                }

                return BitConverter.ToInt32(source.Slice(offset, 4)) / 2147483648f;
            default:
                return 0f;
        }
    }
}
