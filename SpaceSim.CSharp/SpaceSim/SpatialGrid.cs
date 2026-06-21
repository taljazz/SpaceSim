using System;
using System.Collections.Generic;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// 2D spatial hash grid for fast proximity queries on celestial bodies.
/// Hashes on dimensions 0 and 1 (the primary spatial plane), with wrapping
/// at the universe boundary (±100 units). Callers still do exact 5D distance
/// checks on the returned candidates.
/// </summary>
public class SpatialGrid
{
    private const float CellSize = 25f;
    private const int GridDim = 8; // 200 / 25
    private const float WorldOffset = 100f; // shifts [-100, 100] to [0, 200]
    private const int TotalCells = GridDim * GridDim;

    private readonly List<CelestialBody>[] _cells;

    public SpatialGrid()
    {
        _cells = new List<CelestialBody>[TotalCells];
        for (int i = 0; i < TotalCells; i++)
            _cells[i] = new List<CelestialBody>(16); // pre-size for typical load
    }

    /// <summary>
    /// Rebuild the grid from scratch. Call once per frame after positions are updated.
    /// </summary>
    public void Rebuild(List<CelestialBody> bodies)
    {
        for (int i = 0; i < TotalCells; i++)
            _cells[i].Clear();

        for (int i = 0; i < bodies.Count; i++)
        {
            int idx = CellIndex(bodies[i].Position);
            _cells[idx].Add(bodies[i]);
        }
    }

    /// <summary>
    /// Find all bodies whose 2D cell neighborhood overlaps with the query radius.
    /// Clears and fills the results list. Caller should do exact 5D distance filtering.
    /// </summary>
    public void GetNearby(float[] pos, float radius, List<CelestialBody> results)
    {
        results.Clear();

        int cx = CellX(pos[0]);
        int cy = CellY(pos[1]);
        int neighborRange = (int)MathF.Ceiling(radius / CellSize);

        for (int dx = -neighborRange; dx <= neighborRange; dx++)
        {
            int nx = ((cx + dx) % GridDim + GridDim) % GridDim; // wrap-safe modulo
            for (int dy = -neighborRange; dy <= neighborRange; dy++)
            {
                int ny = ((cy + dy) % GridDim + GridDim) % GridDim;
                var cell = _cells[ny * GridDim + nx];
                for (int i = 0; i < cell.Count; i++)
                    results.Add(cell[i]);
            }
        }
    }

    private static int CellX(float x)
    {
        int c = (int)MathF.Floor((x + WorldOffset) / CellSize);
        return ((c % GridDim) + GridDim) % GridDim;
    }

    private static int CellY(float y)
    {
        int c = (int)MathF.Floor((y + WorldOffset) / CellSize);
        return ((c % GridDim) + GridDim) % GridDim;
    }

    private static int CellIndex(float[] pos)
    {
        return CellY(pos[1]) * GridDim + CellX(pos[0]);
    }
}
