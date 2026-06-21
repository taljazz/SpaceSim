using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="ResonancePhysics"/> — the resonance curve and its inverse. These also
/// pin down "behaviour preserved": several tests assert the extracted helper produces exactly the
/// same value as the original inline formula it replaced.
/// </summary>
public class ResonancePhysicsTests
{
    #region Resonance

    [Fact]
    public void Resonance_PerfectMatch_ReturnsOne()
    {
        Assert.Equal(1f, ResonancePhysics.Resonance(0f, 10f));
    }

    [Fact]
    public void Resonance_IsSymmetricAroundZeroDelta()
    {
        Assert.Equal(ResonancePhysics.Resonance(5f, 10f), ResonancePhysics.Resonance(-5f, 10f));
    }

    [Fact]
    public void Resonance_DecreasesAsDeltaGrows()
    {
        float near = ResonancePhysics.Resonance(1f, 10f);
        float mid = ResonancePhysics.Resonance(5f, 10f);
        float far = ResonancePhysics.Resonance(20f, 10f);
        Assert.True(near > mid && mid > far);
    }

    [Fact]
    public void Resonance_AlwaysInZeroToOne()
    {
        for (float delta = -500f; delta <= 500f; delta += 7.3f)
        {
            float r = ResonancePhysics.Resonance(delta, 10f);
            Assert.InRange(r, 0f, 1f);
        }
    }

    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(3f, 10f)]
    [InlineData(-7.5f, 12.5f)]
    [InlineData(100f, 5f)]
    [InlineData(0.25f, 0.5f)]
    public void Resonance_MatchesLegacyInlineFormula(float delta, float width)
    {
        // The exact expression that previously lived inline in six methods.
        float legacy = 1f / (1f + (delta / width) * (delta / width));
        Assert.Equal(legacy, ResonancePhysics.Resonance(delta, width));
    }

    #endregion

    #region Inverse (DriveForTargetResonance)

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.8f)]
    [InlineData(0.95f)]
    public void DriveForTargetResonance_RoundTripsThroughResonance(float targetRes)
    {
        const float fTarget = 400f;
        const float width = 10f;

        float drive = ResonancePhysics.DriveForTargetResonance(fTarget, targetRes, width, +1f);
        float actualRes = ResonancePhysics.Resonance(drive - fTarget, width);

        Assert.Equal(targetRes, actualRes, precision: 4);
    }

    [Fact]
    public void DriveForTargetResonance_NonPositiveResonance_ReturnsTargetUnchanged()
    {
        Assert.Equal(400f, ResonancePhysics.DriveForTargetResonance(400f, 0f, 10f, +1f));
        Assert.Equal(400f, ResonancePhysics.DriveForTargetResonance(400f, -0.5f, 10f, -1f));
    }

    [Fact]
    public void DriveForTargetResonance_SignChoosesSideOfTarget()
    {
        float up = ResonancePhysics.DriveForTargetResonance(400f, 0.5f, 10f, +1f);
        float down = ResonancePhysics.DriveForTargetResonance(400f, 0.5f, 10f, -1f);
        Assert.True(up > 400f);
        Assert.True(down < 400f);
    }

    [Fact]
    public void DriveForTargetResonance_MatchesLegacyInlineFormula()
    {
        const float fTarget = 400f, width = 10f, targetRes = 0.6f, sign = -1f;

        // The exact expression that previously lived inline in the navigation/autopilot code.
        float dOverW = MathF.Sqrt(1f / targetRes - 1f);
        float delta = width * dOverW;
        float deltaF = sign * delta;
        float legacy = fTarget + deltaF;

        Assert.Equal(legacy, ResonancePhysics.DriveForTargetResonance(fTarget, targetRes, width, sign));
    }

    #endregion
}
