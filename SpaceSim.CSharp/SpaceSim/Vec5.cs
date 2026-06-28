using System;

namespace SpaceSim;

/// <summary>
/// Helper methods for 5-dimensional float arrays (replacing numpy).
/// </summary>
public static class Vec5
{
    /// <summary>The fixed dimensionality of every vector here: 3 spatial + 2 higher dimensions.</summary>
    public const int Dimensions = 5;

    #region Construction

    /// <summary>A fresh all-zero 5D vector.</summary>
    public static float[] Zero() => new float[Dimensions];

    /// <summary>A 5D vector from its five components.</summary>
    public static float[] Create(float x, float y, float z, float w, float v)
        => new[] { x, y, z, w, v };

    /// <summary>A copy of <paramref name="a"/> (so callers can mutate without aliasing).</summary>
    public static float[] Clone(float[] a) => (float[])a.Clone();

    #endregion

    #region Magnitude & distance

    /// <summary>Euclidean length of <paramref name="a"/>.</summary>
    public static float Norm(float[] a)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i] * a[i];
        return MathF.Sqrt(sum);
    }

    /// <summary>Euclidean distance between <paramref name="a"/> and <paramref name="b"/>.</summary>
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

    #endregion

    #region Arithmetic

    /// <summary>Component-wise sum, returning a new vector.</summary>
    public static float[] Add(float[] a, float[] b)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] + b[i];
        return r;
    }

    /// <summary>Adds <paramref name="b"/> into <paramref name="a"/> in place (no allocation).</summary>
    public static void AddInPlace(float[] a, float[] b)
    {
        for (int i = 0; i < Dimensions; i++)
            a[i] += b[i];
    }

    /// <summary>Component-wise difference <paramref name="a"/> - <paramref name="b"/>, returning a new vector.</summary>
    public static float[] Subtract(float[] a, float[] b)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] - b[i];
        return r;
    }

    /// <summary>Writes <paramref name="a"/> - <paramref name="b"/> into <paramref name="result"/> (no allocation).</summary>
    public static void SubtractInto(float[] a, float[] b, float[] result)
    {
        for (int i = 0; i < Dimensions; i++)
            result[i] = a[i] - b[i];
    }

    /// <summary>Dot product of <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static float Dot(float[] a, float[] b)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i] * b[i];
        return sum;
    }

    /// <summary>Scales <paramref name="a"/> by <paramref name="s"/>, returning a new vector.</summary>
    public static float[] Scale(float[] a, float s)
    {
        var r = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            r[i] = a[i] * s;
        return r;
    }

    /// <summary>Scales <paramref name="a"/> by <paramref name="s"/> in place (no allocation).</summary>
    public static void ScaleInPlace(float[] a, float s)
    {
        for (int i = 0; i < Dimensions; i++)
            a[i] *= s;
    }

    /// <summary>
    /// Wraps each component into the universe torus [-half, half] in place — the same fold the ship's
    /// position uses, so positioned objects stay inside the reachable volume instead of sitting off the
    /// edge, where the wrapping ship could never catch them.
    /// </summary>
    public static void WrapInto(float[] a, float half)
    {
        float period = half * 2f;
        for (int i = 0; i < Dimensions; i++)
            a[i] = ((a[i] + half) % period + period) % period - half;
    }

    #endregion

    #region Reductions

    /// <summary>Arithmetic mean of the five components.</summary>
    public static float Mean(float[] a)
    {
        float sum = 0;
        for (int i = 0; i < Dimensions; i++)
            sum += a[i];
        return sum / Dimensions;
    }

    /// <summary>True if every component satisfies <paramref name="predicate"/> (e.g. all dimensions in resonance).</summary>
    public static bool All(float[] a, Func<float, bool> predicate)
    {
        for (int i = 0; i < Dimensions; i++)
            if (!predicate(a[i])) return false;
        return true;
    }

    #endregion

    #region Segment geometry

    /// <summary>
    /// Shortest distance from point <paramref name="p"/> to the line segment running from
    /// <paramref name="a"/> to <paramref name="b"/>, computed <b>without allocating</b> (unlike
    /// <see cref="Subtract"/> / <see cref="Scale"/> / <see cref="Add"/>). This runs every frame for
    /// every ley line, so keeping it garbage-free matters.
    /// </summary>
    /// <param name="p">The query point.</param>
    /// <param name="a">Segment start.</param>
    /// <param name="b">Segment end.</param>
    /// <returns>
    /// The distance, plus <c>T</c> in [0, 1] giving where along the segment the closest point falls
    /// (0 = at <paramref name="a"/>, 1 = at <paramref name="b"/>).
    /// </returns>
    public static (float Distance, float T) DistanceToSegment(float[] p, float[] a, float[] b)
    {
        // Project (p - a) onto (b - a): t = dot(p-a, b-a) / |b-a|^2, then clamp to the segment.
        float abLenSq = 0f;
        float dot = 0f;
        for (int i = 0; i < Dimensions; i++)
        {
            float ab = b[i] - a[i];
            abLenSq += ab * ab;
            dot += (p[i] - a[i]) * ab;
        }

        // Degenerate segment (a == b): fall back to a plain point-to-point distance.
        if (abLenSq < 1e-12f)
            return (Distance(p, a), 0f);

        float t = Math.Clamp(dot / abLenSq, 0f, 1f);

        // Distance from p to the clamped closest point a + t*(b - a) — still allocation-free.
        float sum = 0f;
        for (int i = 0; i < Dimensions; i++)
        {
            float closest = a[i] + t * (b[i] - a[i]);
            float d = p[i] - closest;
            sum += d * d;
        }
        return (MathF.Sqrt(sum), t);
    }

    #endregion

    #region Formatting

    /// <summary>A human-readable "[x, y, z, w, v]" string, for logs and screen-reader output.</summary>
    public static string Format(float[] a, int decimals = 1)
    {
        var parts = new string[Dimensions];
        for (int i = 0; i < Dimensions; i++)
            parts[i] = MathF.Round(a[i], decimals).ToString($"F{decimals}");
        return $"[{string.Join(", ", parts)}]";
    }

    #endregion
}
