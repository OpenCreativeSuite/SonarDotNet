namespace SonarDotNet.Core.Dsp;

/// <summary>A second-order RBJ biquad section.</summary>
/// <remarks>
/// A value type with preallocated state, so the real-time path never touches the
/// heap when coefficients change.
/// </remarks>
public struct Biquad
{
    private float _b0;
    private float _b1;
    private float _b2;
    private float _a1;
    private float _a2;
    private float _z1;
    private float _z2;

    /// <summary>Resets the delay line without changing the coefficients.</summary>
    public void Reset()
    {
        _z1 = 0f;
        _z2 = 0f;
    }

    /// <summary>Configures a low-pass section.</summary>
    public void LowPass(float frequency, float q, int sampleRate)
    {
        (double w0, double cos, double alpha) = Terms(frequency, q, sampleRate);
        double b1 = 1.0 - cos;
        double b0 = b1 * 0.5;
        double b2 = b0;
        Normalize(b0, b1, b2, 1.0 + alpha, -2.0 * cos, 1.0 - alpha);
    }

    /// <summary>Configures a high-pass section.</summary>
    public void HighPass(float frequency, float q, int sampleRate)
    {
        (double w0, double cos, double alpha) = Terms(frequency, q, sampleRate);
        double b0 = (1.0 + cos) * 0.5;
        double b1 = -(1.0 + cos);
        double b2 = b0;
        Normalize(b0, b1, b2, 1.0 + alpha, -2.0 * cos, 1.0 - alpha);
    }

    /// <summary>Configures a peaking equaliser section.</summary>
    public void Peaking(float frequency, float gainDb, float q, int sampleRate)
    {
        (double w0, double cos, double alpha) = Terms(frequency, q, sampleRate);
        double a = Math.Pow(10.0, gainDb / 40.0);
        double b0 = 1.0 + (alpha * a);
        double b1 = -2.0 * cos;
        double b2 = 1.0 - (alpha * a);
        double a0 = 1.0 + (alpha / a);
        double a1 = -2.0 * cos;
        double a2 = 1.0 - (alpha / a);
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>Configures a low-shelf section.</summary>
    public void LowShelf(float frequency, float gainDb, float q, int sampleRate)
    {
        (double w0, double cos, double alpha) = Terms(frequency, q, sampleRate);
        double a = Math.Pow(10.0, gainDb / 40.0);
        double sqrtA = Math.Sqrt(a);
        double two = 2.0 * sqrtA * alpha;
        double b0 = a * ((a + 1.0) - ((a - 1.0) * cos) + two);
        double b1 = 2.0 * a * ((a - 1.0) - ((a + 1.0) * cos));
        double b2 = a * ((a + 1.0) - ((a - 1.0) * cos) - two);
        double a0 = (a + 1.0) + ((a - 1.0) * cos) + two;
        double a1 = -2.0 * ((a - 1.0) + ((a + 1.0) * cos));
        double a2 = (a + 1.0) + ((a - 1.0) * cos) - two;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>Configures a high-shelf section.</summary>
    public void HighShelf(float frequency, float gainDb, float q, int sampleRate)
    {
        (double w0, double cos, double alpha) = Terms(frequency, q, sampleRate);
        double a = Math.Pow(10.0, gainDb / 40.0);
        double sqrtA = Math.Sqrt(a);
        double two = 2.0 * sqrtA * alpha;
        double b0 = a * ((a + 1.0) + ((a - 1.0) * cos) + two);
        double b1 = -2.0 * a * ((a - 1.0) + ((a + 1.0) * cos));
        double b2 = a * ((a + 1.0) + ((a - 1.0) * cos) - two);
        double a0 = (a + 1.0) - ((a - 1.0) * cos) + two;
        double a1 = 2.0 * ((a - 1.0) - ((a + 1.0) * cos));
        double a2 = (a + 1.0) - ((a - 1.0) * cos) - two;
        Normalize(b0, b1, b2, a0, a1, a2);
    }

    /// <summary>Processes one sample.</summary>
    public float Process(float input)
    {
        float output = (_b0 * input) + _z1;
        _z1 = (_b1 * input) - (_a1 * output) + _z2;
        _z2 = (_b2 * input) - (_a2 * output);
        return output;
    }

    /// <summary>Evaluates the magnitude response at a frequency, in decibels.</summary>
    public readonly double MagnitudeDb(double frequency, int sampleRate)
    {
        double w = 2.0 * Math.PI * frequency / sampleRate;
        double cos1 = Math.Cos(w);
        double cos2 = Math.Cos(2.0 * w);
        double sin1 = Math.Sin(w);
        double sin2 = Math.Sin(2.0 * w);
        double numeratorReal = _b0 + (_b1 * cos1) + (_b2 * cos2);
        double numeratorImag = -(_b1 * sin1) - (_b2 * sin2);
        double denominatorReal = 1.0 + (_a1 * cos1) + (_a2 * cos2);
        double denominatorImag = -(_a1 * sin1) - (_a2 * sin2);
        double numerator = Math.Sqrt((numeratorReal * numeratorReal) + (numeratorImag * numeratorImag));
        double denominator = Math.Sqrt((denominatorReal * denominatorReal) + (denominatorImag * denominatorImag));
        return 20.0 * Math.Log10(Math.Max(1e-12, numerator / Math.Max(1e-12, denominator)));
    }

    private void Normalize(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        _b0 = (float)(b0 / a0);
        _b1 = (float)(b1 / a0);
        _b2 = (float)(b2 / a0);
        _a1 = (float)(a1 / a0);
        _a2 = (float)(a2 / a0);
    }

    private static (double W0, double Cos, double Alpha) Terms(float frequency, float q, int sampleRate)
    {
        double clamped = Math.Clamp(frequency, 10.0, (sampleRate * 0.49));
        double w0 = 2.0 * Math.PI * clamped / sampleRate;
        double cos = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2.0 * Math.Max(0.05, q));
        return (w0, cos, alpha);
    }
}
