namespace SonarDotNet.Core.Audio;

/// <summary>A single-producer, single-consumer float ring buffer.</summary>
/// <remarks>
/// The audio thread only writes; the recording thread only reads. If the reader
/// falls behind, the write is dropped and counted rather than blocking the audio
/// callback.
/// </remarks>
public sealed class FloatRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private long _write;
    private long _read;
    private long _dropped;

    /// <summary>Creates a ring with a power-of-two capacity.</summary>
    public FloatRingBuffer(int capacity)
    {
        int size = 2;
        while (size < capacity)
        {
            size <<= 1;
        }

        _buffer = new float[size];
        _mask = size - 1;
    }

    /// <summary>The number of samples waiting to be read.</summary>
    public int Count => (int)Math.Min(int.MaxValue, Math.Max(0, Volatile.Read(ref _write) - Volatile.Read(ref _read)));

    /// <summary>Samples dropped because the reader was too slow.</summary>
    public long Dropped => Volatile.Read(ref _dropped);

    /// <summary>Writes samples, dropping any that do not fit.</summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        long write = Volatile.Read(ref _write);
        long read = Volatile.Read(ref _read);
        int free = _buffer.Length - (int)(write - read);
        int count = Math.Min(samples.Length, Math.Max(0, free));
        for (int i = 0; i < count; i++)
        {
            _buffer[(int)((write + i) & _mask)] = samples[i];
        }

        if (count > 0)
        {
            Volatile.Write(ref _write, write + count);
        }

        if (count < samples.Length)
        {
            Interlocked.Add(ref _dropped, samples.Length - count);
        }
    }

    /// <summary>Reads as many samples as fit.</summary>
    public int Read(Span<float> destination)
    {
        long read = Volatile.Read(ref _read);
        long write = Volatile.Read(ref _write);
        int available = (int)Math.Max(0, write - read);
        int count = Math.Min(destination.Length, available);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _buffer[(int)((read + i) & _mask)];
        }

        if (count > 0)
        {
            Volatile.Write(ref _read, read + count);
        }

        return count;
    }

    /// <summary>Drops everything currently buffered.</summary>
    public void Clear()
    {
        Volatile.Write(ref _read, Volatile.Read(ref _write));
    }
}
