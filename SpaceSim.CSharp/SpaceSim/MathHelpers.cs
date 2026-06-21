using System;
using System.Collections.Generic;

namespace SpaceSim;

public static class MathHelpers
{
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

    public static float RandomRange(float min, float max, Random? rng = null)
    {
        rng ??= Random.Shared;
        return min + rng.NextSingle() * (max - min);
    }

    public static float Clamp(float value, float min, float max)
        => Math.Clamp(value, min, max);

    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}
