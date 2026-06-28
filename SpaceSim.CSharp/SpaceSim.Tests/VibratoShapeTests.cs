using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>Tests for <see cref="VibratoShape.DepthRate"/> — the pure resonance-to-vibrato mapping.</summary>
public class VibratoShapeTests
{
    [Fact]
    public void ZeroResonance_GivesMinimums()
    {
        var (depth, rate) = VibratoShape.DepthRate(0f);
        Assert.Equal(VibratoShape.DepthMin, depth, 4);
        Assert.Equal(VibratoShape.RateMin, rate, 4);
    }

    [Fact]
    public void FullResonance_GivesMaximums()
    {
        var (depth, rate) = VibratoShape.DepthRate(1f);
        Assert.Equal(VibratoShape.DepthMax, depth, 4);
        Assert.Equal(VibratoShape.RateMax, rate, 4);
    }

    [Fact]
    public void Midpoint_IsHalfway()
    {
        var (depth, rate) = VibratoShape.DepthRate(0.5f);
        Assert.Equal((VibratoShape.DepthMin + VibratoShape.DepthMax) / 2f, depth, 4);
        Assert.Equal((VibratoShape.RateMin + VibratoShape.RateMax) / 2f, rate, 4);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(2f)]
    public void OutOfRangeResonance_IsClamped(float res)
    {
        var (depth, rate) = VibratoShape.DepthRate(res);
        Assert.InRange(depth, VibratoShape.DepthMin, VibratoShape.DepthMax);
        Assert.InRange(rate, VibratoShape.RateMin, VibratoShape.RateMax);
    }

    [Fact]
    public void Depth_IsMonotonicInResonance()
    {
        Assert.True(VibratoShape.DepthRate(0.2f).Depth < VibratoShape.DepthRate(0.8f).Depth);
        Assert.True(VibratoShape.DepthRate(0.2f).Rate < VibratoShape.DepthRate(0.8f).Rate);
    }
}
