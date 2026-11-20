using NAudio.Wave;
using SonarDotNet.Core.Audio;

namespace SonarDotNet.Tests;

public sealed class RecordingTests
{
    [Fact]
    public void RecordingSessionWritesBothWavFilesAndReportsDuration()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SonarDotNetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            RecordingResult result;
            using (RecordingSession session = new(folder))
            {
                float[] raw = Fx.Tone(220f, 4800, 0.2f);
                float[] processed = Fx.Tone(440f, 4800, 0.1f);
                for (int block = 0; block < 10; block++)
                {
                    session.Enqueue(raw, processed);
                }

                result = session.Stop();
            }

            Assert.True(File.Exists(result.RawPath));
            Assert.True(File.Exists(result.ProcessedPath));
            Assert.InRange(result.Duration.TotalSeconds, 0.95, 1.05);

            using WaveFileReader rawReader = new(result.RawPath);
            using WaveFileReader processedReader = new(result.ProcessedPath);
            Assert.Equal(RecordingSession.SampleRate, rawReader.WaveFormat.SampleRate);
            Assert.Equal(1, rawReader.WaveFormat.Channels);
            Assert.InRange(rawReader.TotalTime.TotalSeconds, 0.95, 1.05);
            Assert.InRange(processedReader.TotalTime.TotalSeconds, 0.95, 1.05);
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch
            {
                // Cleanup is not part of the assertion.
            }
        }
    }

    [Fact]
    public void FloatRingBufferReportsOverflowWithoutBlocking()
    {
        FloatRingBuffer ring = new(4);
        ring.Write([1f, 2f, 3f, 4f, 5f, 6f]);
        Assert.Equal(2, ring.Dropped);
        float[] output = new float[4];
        Assert.Equal(4, ring.Read(output));
        Assert.Equal([1f, 2f, 3f, 4f], output);
    }
}
