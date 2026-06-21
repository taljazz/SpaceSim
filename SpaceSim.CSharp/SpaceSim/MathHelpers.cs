using System;
using System.Collections.Generic;

namespace SpaceSim;

/// <summary>
/// Small numeric helpers that fill in for the handful of numpy/random utilities the original Python
/// relied on. Generic, stateless, and used all over generation and gameplay.
/// </summary>
public static class MathHelpers
{
    #region Arrays (numpy stand-ins)

    /// <summary>
    /// Evenly spaced values from <paramref name="start"/> to <paramref name="end"/> inclusive — the
    /// equivalent of numpy's <c>linspace</c>. A <paramref name="count"/> of 1 just returns <paramref name="start"/>.
    /// </summary>
    public static float[] Linspace(float start, float end, int count)
    {
        var result = new float[count];
        if (count == 1)
        {
            result[0] = start;
            return result;
        }
        float step = (end - start) / (count - 1);
        for (int i = 0; i < count; i++)
            result[i] = start + i * step;
        return result;
    }

    /// <summary>Index of the smallest value in <paramref name="values"/> (first one wins on ties).</summary>
    public static int ArgMin(IList<float> values)
    {
        int minIdx = 0;
        float minVal = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] < minVal)
            {
                minVal = values[i];
                minIdx = i;
            }
        }
        return minIdx;
    }

    /// <summary>Index of the largest value in <paramref name="values"/> (first one wins on ties).</summary>
    public static int ArgMax(float[] values)
    {
        int maxIdx = 0;
        float maxVal = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > maxVal)
            {
                maxVal = values[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    #endregion

    #region Random

    /// <summary>
    /// Picks a key at random, weighted by its associated probability (the values should sum to ~1).
    /// Used to roll celestial types from their distribution tables.
    /// </summary>
    public static TEnum WeightedRandomChoice<TEnum>(Dictionary<TEnum, float> probabilities, Random? rng = null) where TEnum : notnull
    {
        rng ??= Random.Shared;
        float roll = rng.NextSingle();
        float cumulative = 0;
        TEnum last = default!;
        foreach (var kvp in probabilities)
        {
            cumulative += kvp.Value;
            last = kvp.Key;
            if (roll < cumulative)
                return kvp.Key;
        }
        // Fallback to last item (rounding)
        return last;
    }

    /// <summary>A uniform random float in the half-open range [<paramref name="min"/>, <paramref name="max"/>).</summary>
    public static float RandomRange(float min, float max, Random? rng = null)
    {
        rng ??= Random.Shared;
        return min + rng.NextSingle() * (max - min);
    }

    #endregion

    #region Scalar

    /// <summary>Clamp <paramref name="value"/> into [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static float Clamp(float value, float min, float max)
        => Math.Clamp(value, min, max);

    /// <summary>Linear interpolation from <paramref name="a"/> to <paramref name="b"/> by fraction <paramref name="t"/>.</summary>
    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    #endregion
}
