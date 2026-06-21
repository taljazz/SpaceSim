using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="SpatialAudioMath"/> — the pure 5D→3D positioning and PCM conversion that
/// the OpenAL/HRTF layer relies on. (The OpenAL calls themselves can only be verified by ear at
/// runtime; this pins down the math that feeds them.)
/// </summary>
public class SpatialAudioMathTests
{
    private static float[] Origin => new float[5];

    #region ToListenerSpace

    [Fact]
    public void TargetDirectlyAhead_MapsToNegativeZ()
    {
        // +Y in world (forward) with no rotation -> OpenAL forward is -Z.
        var (x, y, z) = SpatialAudioMath.ToListenerSpace(Origin, new float[] { 0, 10, 0, 0, 0 }, 0f);
        Assert.Equal(0f, x, 4);
        Assert.Equal(0f, y, 4);
        Assert.Equal(-10f, z, 4);
    }

    [Fact]
    public void TargetToTheRight_MapsToPositiveX()
    {
        var (x, y, z) = SpatialAudioMath.ToListenerSpace(Origin, new float[] { 10, 0, 0, 0, 0 }, 0f);
        Assert.Equal(10f, x, 4);
        Assert.Equal(0f, y, 4);
        Assert.Equal(0f, z, 4);
    }

    [Fact]
    public void TargetAbove_MapsToPositiveY()
    {
        // Third spatial dimension (index 2) drives elevation.
        var (x, y, z) = SpatialAudioMath.ToListenerSpace(Origin, new float[] { 0, 0, 7, 0, 0 }, 0f);
        Assert.Equal(0f, x, 4);
        Assert.Equal(7f, y, 4);
        Assert.Equal(0f, z, 4);
    }

    [Fact]
    public void RelativeToShip_NotAbsolute()
    {
        float[] ship = { 100, 100, 0, 0, 0 };
        float[] target = { 100, 110, 0, 0, 0 }; // 10 ahead of the ship
        var (x, y, z) = SpatialAudioMath.ToListenerSpace(ship, target, 0f);
        Assert.Equal(0f, x, 4);
        Assert.Equal(-10f, z, 4);
    }

    [Fact]
    public void Rotation_MixesHigherDimensions_LikeTheProjection()
    {
        // At 90 degrees the azimuth axis reads the higher dim (index 3), matching project_to_2d.
        float[] target = { 0, 0, 0, 8, 0 }; // only higher-dim-4 offset
        var (x, _, _) = SpatialAudioMath.ToListenerSpace(Origin, target, MathF.PI / 2f);
        Assert.Equal(8f, x, 3); // dw * sin(90) = 8
    }

    [Fact]
    public void DistancePreserved_ForSpatialDimsAtZeroRotation()
    {
        float[] target = { 3, 4, 12, 0, 0 }; // 3-4-12 -> magnitude 13
        var (x, y, z) = SpatialAudioMath.ToListenerSpace(Origin, target, 0f);
        float mag = MathF.Sqrt(x * x + y * y + z * z);
        Assert.Equal(13f, mag, 3);
    }

    #endregion

    #region FloatToPcm16

    [Fact]
    public void FloatToPcm16_ScalesAndClamps()
    {
        short[] pcm = SpatialAudioMath.FloatToPcm16(new[] { 0f, 1f, -1f, 2f, -2f, 0.5f });
        Assert.Equal(0, pcm[0]);
        Assert.Equal(short.MaxValue, pcm[1]);
        Assert.Equal(-short.MaxValue, pcm[2]);
        Assert.Equal(short.MaxValue, pcm[3]);  // clamped from 2f
        Assert.Equal(-short.MaxValue, pcm[4]); // clamped from -2f
        Assert.Equal((short)(0.5f * short.MaxValue), pcm[5]);
    }

    [Fact]
    public void FloatToPcm16_PreservesLength()
    {
        Assert.Equal(441, SpatialAudioMath.FloatToPcm16(new float[441]).Length);
    }

    #endregion
}
