using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="MathHelpers"/> — the small numeric utilities (argmin/argmax, linspace,
/// lerp, weighted random choice) that the rest of the game leans on.
/// </summary>
public class MathHelpersTests
{
    #region Linspace

    [Fact]
    public void Linspace_SingleSample_ReturnsStart()
    {
        float[] result = MathHelpers.Linspace(5f, 10f, 1);
        Assert.Single(result);
        Assert.Equal(5f, result[0]);
    }

    [Fact]
    public void Linspace_EvenlySpacedInclusiveEndpoints()
    {
        float[] result = MathHelpers.Linspace(0f, 1f, 5);
        Assert.Equal(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }, result);
    }

    #endregion

    #region ArgMin / ArgMax

    [Fact]
    public void ArgMin_FindsIndexOfSmallest()
    {
        Assert.Equal(2, MathHelpers.ArgMin(new[] { 3f, 1.5f, 0.2f, 9f }));
    }

    [Fact]
    public void ArgMax_FindsIndexOfLargest()
    {
        Assert.Equal(3, MathHelpers.ArgMax(new[] { 3f, 1.5f, 0.2f, 9f }));
    }

    [Fact]
    public void ArgMin_TiesReturnFirstOccurrence()
    {
        Assert.Equal(0, MathHelpers.ArgMin(new[] { 1f, 1f, 1f }));
    }

    #endregion

    #region Lerp / Clamp

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 5f)]
    [InlineData(1f, 10f)]
    public void Lerp_Interpolates(float t, float expected)
    {
        Assert.Equal(expected, MathHelpers.Lerp(0f, 10f, t));
    }

    [Fact]
    public void Clamp_BoundsValue()
    {
        Assert.Equal(0f, MathHelpers.Clamp(-5f, 0f, 10f));
        Assert.Equal(10f, MathHelpers.Clamp(50f, 0f, 10f));
        Assert.Equal(5f, MathHelpers.Clamp(5f, 0f, 10f));
    }

    #endregion

    #region RandomRange / WeightedRandomChoice

    [Fact]
    public void RandomRange_StaysWithinBounds()
    {
        var rng = new Random(123);
        for (int i = 0; i < 1000; i++)
        {
            float v = MathHelpers.RandomRange(10f, 20f, rng);
            Assert.InRange(v, 10f, 20f);
        }
    }

    [Fact]
    public void WeightedRandomChoice_CertainOption_AlwaysChosen()
    {
        var probs = new System.Collections.Generic.Dictionary<string, float> { ["only"] = 1.0f };
        var rng = new Random(7);
        for (int i = 0; i < 50; i++)
            Assert.Equal("only", MathHelpers.WeightedRandomChoice(probs, rng));
    }

    [Fact]
    public void WeightedRandomChoice_ZeroWeightOption_NeverChosenOverCertainOne()
    {
        // "never" has weight 0, so the cumulative test always falls through to "always".
        var probs = new System.Collections.Generic.Dictionary<string, float>
        {
            ["never"] = 0.0f,
            ["always"] = 1.0f,
        };
        var rng = new Random(99);
        for (int i = 0; i < 50; i++)
            Assert.Equal("always", MathHelpers.WeightedRandomChoice(probs, rng));
    }

    #endregion
}
