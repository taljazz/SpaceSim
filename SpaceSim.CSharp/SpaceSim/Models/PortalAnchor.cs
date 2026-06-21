namespace SpaceSim.Models;

/// <summary>
/// A player-placed bookmark in space. Press P to drop one (costs crystals); press Shift+P to
/// teleport back to it later.
/// </summary>
public class PortalAnchor : WorldObject
{
    /// <summary>Display name of the anchor (e.g. "Anchor 1").</summary>
    public string Name = "";

    /// <summary>Simulation time at which the anchor was created.</summary>
    public float CreatedTime;
}
