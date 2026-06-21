namespace SpaceSim.Models;

/// <summary>
/// Abstract base class for all objects that exist in the 5D universe.
/// Provides shared position, type, and distance calculation.
/// </summary>
public abstract class WorldObject
{
    /// <summary>5D position in the universe.</summary>
    public float[] Position { get; set; } = Vec5.Zero();

    /// <summary>Calculate 5D distance from this object to a position.</summary>
    public float DistanceTo(float[] other) => Vec5.Distance(Position, other);

    /// <summary>Calculate 5D distance from this object to another WorldObject.</summary>
    public float DistanceTo(WorldObject other) => Vec5.Distance(Position, other.Position);
}
