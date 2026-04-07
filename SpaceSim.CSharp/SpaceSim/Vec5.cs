using System;

namespace SpaceSim;

/// <summary>
/// Helper methods for 5-dimensional float arrays (replacing numpy).
/// </summary>
public static class Vec5
{
    public const int Dimensions = 5;

    public static float[] Zero() => new float[Dimensions];

    public static float[] Create(float x, float y, float z, float w, float v)
        => new[] { x, y, z, w, v };

    public static float[] Clone(float[] a) => (float[])a.Clone();

    public static float Norm(float[] a)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i] * a[i];
        return MathF.Sqrt(sum);
    }

    public static float Distance(float[] a, float[] b)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    public static float[] Add(float[] a, float[] b)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] + b[i];
        return r;
    }

    public static void AddInPlace(float[] a, float[] b)
    {
        for (int i = 0; i < Dimensions; i++)
            a[i] += b[i];
    }

    public static float[] Subtract(float[] a, float[] b)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] - b[i];
        return r;
    }

    public static float Dot(float[] a, float[] b)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i] * b[i];
        return sum;
    }

    public static float[] Scale(float[] a, float s)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] * s;
        return r;
    }

    public static void ScaleInPlace(float[] a, float s)
    {
        for (int i = 0; i < Dimensions; i++)
            a[i] *= s;
    }

    public static float Mean(float[] a)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i];
        return sum / Dimensions;
    }

    public static bool All(float[] a, Func<float, bool> predicate)
    {
        for (int i = 0; i < Dimensions; i++)
            if (!predicate(a[i])) return false;
        return true;
    }

    public static string Format(float[] a, int decimals = 1)
    {
        var parts = new string[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            parts[i] = MathF.Round(a[i], decimals).ToString($"F{decimals}");
        return $"[{string.Join(", ", parts)}]";
    }
}
