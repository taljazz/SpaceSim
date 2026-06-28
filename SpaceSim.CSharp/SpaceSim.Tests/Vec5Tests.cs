using System;
using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="Vec5"/> — the 5D vector helpers that replaced numpy. Includes the new
/// allocation-free <see cref="Vec5.DistanceToSegment"/> used for ley-line proximity.
/// </summary>
public class Vec5Tests
{
    #region Basic vector ops

    [Fact]
    public void Zero_IsFiveZeros()
    {
        float[] z = Vec5.Zero();
        Assert.Equal(5, z.Length);
        Assert.All(z, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Norm_OfUnitAxis_IsOne()
    {
        Assert.Equal(1f, Vec5.Norm(Vec5.Create(1, 0, 0, 0, 0)));
    }

    [Fact]
    public void Distance_IsEuclidean()
    {
        // 3-4-5 triangle embedded in the first two dimensions.
        float[] a = Vec5.Create(0, 0, 0, 0, 0);
        float[] b = Vec5.Create(3, 4, 0, 0, 0);
        Assert.Equal(5f, Vec5.Distance(a, b));
    }

    [Fact]
    public void Distance_IsSymmetric()
    {
        float[] a = Vec5.Create(1, 2, 3, 4, 5);
        float[] b = Vec5.Create(5, 4, 3, 2, 1);
        Assert.Equal(Vec5.Distance(a, b), Vec5.Distance(b, a));
    }

    [Fact]
    public void Dot_ComputesInnerProduct()
    {
        float[] a = Vec5.Create(1, 2, 3, 4, 5);
        float[] b = Vec5.Create(5, 4, 3, 2, 1);
        // 5 + 8 + 9 + 8 + 5 = 35
        Assert.Equal(35f, Vec5.Dot(a, b));
    }

    [Fact]
    public void AddSubtractScale_Work()
    {
        float[] a = Vec5.Create(1, 2, 3, 4, 5);
        float[] b = Vec5.Create(10, 10, 10, 10, 10);

        Assert.Equal(Vec5.Create(11, 12, 13, 14, 15), Vec5.Add(a, b));
        Assert.Equal(Vec5.Create(-9, -8, -7, -6, -5), Vec5.Subtract(a, b));
        Assert.Equal(Vec5.Create(2, 4, 6, 8, 10), Vec5.Scale(a, 2f));
    }

    [Fact]
    public void InPlaceVariants_MutateFirstArgument()
    {
        float[] a = Vec5.Create(1, 2, 3, 4, 5);
        Vec5.AddInPlace(a, Vec5.Create(1, 1, 1, 1, 1));
        Assert.Equal(Vec5.Create(2, 3, 4, 5, 6), a);

        Vec5.ScaleInPlace(a, 0.5f);
        Assert.Equal(Vec5.Create(1, 1.5f, 2, 2.5f, 3), a);
    }

    [Fact]
    public void SubtractInto_WritesToResultBuffer()
    {
        float[] a = Vec5.Create(5, 5, 5, 5, 5);
        float[] b = Vec5.Create(1, 2, 3, 4, 5);
        float[] result = Vec5.Zero();
        Vec5.SubtractInto(a, b, result);
        Assert.Equal(Vec5.Create(4, 3, 2, 1, 0), result);
    }

    [Fact]
    public void Mean_AveragesComponents()
    {
        Assert.Equal(3f, Vec5.Mean(Vec5.Create(1, 2, 3, 4, 5)));
    }

    [Fact]
    public void All_AppliesPredicateToEveryComponent()
    {
        Assert.True(Vec5.All(Vec5.Create(1, 1, 1, 1, 1), v => v > 0));
        Assert.False(Vec5.All(Vec5.Create(1, 1, -1, 1, 1), v => v > 0));
    }

    [Fact]
    public void Clone_IsIndependentCopy()
    {
        float[] a = Vec5.Create(1, 2, 3, 4, 5);
        float[] copy = Vec5.Clone(a);
        copy[0] = 99f;
        Assert.Equal(1f, a[0]); // original untouched
    }

    #endregion

    #region DistanceToSegment

    [Fact]
    public void DistanceToSegment_ClosestPointInMiddle()
    {
        // Segment along X axis from 0 to 10; point sits at x=5, y=3.
        float[] a = Vec5.Create(0, 0, 0, 0, 0);
        float[] b = Vec5.Create(10, 0, 0, 0, 0);
        float[] p = Vec5.Create(5, 3, 0, 0, 0);

        var (dist, t) = Vec5.DistanceToSegment(p, a, b);
        Assert.Equal(3f, dist, precision: 5);
        Assert.Equal(0.5f, t, precision: 5);
    }

    [Fact]
    public void DistanceToSegment_ClampsBeforeStart()
    {
        float[] a = Vec5.Create(0, 0, 0, 0, 0);
        float[] b = Vec5.Create(10, 0, 0, 0, 0);
        float[] p = Vec5.Create(-5, 0, 0, 0, 0); // behind the start

        var (dist, t) = Vec5.DistanceToSegment(p, a, b);
        Assert.Equal(5f, dist, precision: 5); // distance to start point
        Assert.Equal(0f, t);
    }

    [Fact]
    public void DistanceToSegment_ClampsAfterEnd()
    {
        float[] a = Vec5.Create(0, 0, 0, 0, 0);
        float[] b = Vec5.Create(10, 0, 0, 0, 0);
        float[] p = Vec5.Create(15, 0, 0, 0, 0); // past the end

        var (dist, t) = Vec5.DistanceToSegment(p, a, b);
        Assert.Equal(5f, dist, precision: 5); // distance to end point
        Assert.Equal(1f, t);
    }

    [Fact]
    public void DistanceToSegment_PointOnSegment_IsZero()
    {
        float[] a = Vec5.Create(0, 0, 0, 0, 0);
        float[] b = Vec5.Create(10, 10, 10, 10, 10);
        float[] p = Vec5.Create(5, 5, 5, 5, 5); // exactly on the line

        var (dist, _) = Vec5.DistanceToSegment(p, a, b);
        Assert.Equal(0f, dist, precision: 4);
    }

    [Fact]
    public void DistanceToSegment_DegenerateSegment_ReturnsDistanceToPoint()
    {
        // a == b: the "segment" is a single point.
        float[] a = Vec5.Create(2, 0, 0, 0, 0);
        float[] p = Vec5.Create(2, 4, 0, 0, 0);

        var (dist, t) = Vec5.DistanceToSegment(p, a, a);
        Assert.Equal(4f, dist, precision: 5);
        Assert.Equal(0f, t);
    }

    #endregion

    #region WrapInto (universe torus)

    [Fact]
    public void WrapInto_FoldsOutOfBoundsCoordinatesIntoRange()
    {
        // The kind of temple/pyramid coordinates that were unreachable before the fix.
        float[] v = Vec5.Create(130.9f, -261.8f, 78.8f, 0f, -161.8f);
        Vec5.WrapInto(v, 100f);
        Assert.All(v, c => Assert.InRange(c, -100f, 100f));
    }

    [Fact]
    public void WrapInto_LeavesInBoundsPointsUnchanged()
    {
        float[] v = Vec5.Create(10f, -20f, 0f, 99f, -99f);
        Vec5.WrapInto(v, 100f);
        Assert.Equal(10f, v[0], precision: 3);
        Assert.Equal(-20f, v[1], precision: 3);
        Assert.Equal(0f, v[2], precision: 3);
        Assert.Equal(99f, v[3], precision: 3);
        Assert.Equal(-99f, v[4], precision: 3);
    }

    [Fact]
    public void WrapInto_MapsAPointToItsToroidalEquivalent()
    {
        // On a [-100, 100] torus (period 200), -261.8 folds to -61.8.
        float[] v = Vec5.Create(-261.8f, 0f, 0f, 0f, 0f);
        Vec5.WrapInto(v, 100f);
        Assert.Equal(-61.8f, v[0], precision: 3);
    }

    #endregion
}
