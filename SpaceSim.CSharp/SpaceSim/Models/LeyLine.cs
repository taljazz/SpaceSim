namespace SpaceSim.Models;

/// <summary>
/// An energy corridor connecting two temples. Flying along a ley line gives the player a large
/// speed boost — the universe's fast-travel highways. (Not a <see cref="WorldObject"/>: it's a
/// segment, not a point, so it carries its own start/end.)
/// </summary>
public class LeyLine
{
    /// <summary>One endpoint of the corridor (5D).</summary>
    public float[] Start = Vec5.Zero();

    /// <summary>The other endpoint of the corridor (5D).</summary>
    public float[] End = Vec5.Zero();

    /// <summary>The corridor's natural resonance frequency (Hz).</summary>
    public float Frequency;

    /// <summary>Object-type tag used for serialization/identification.</summary>
    public string Type = "ley_line";

    /// <summary>Display name (e.g. "Aries to Taurus").</summary>
    public string Name = "";

    /// <summary>Index of the temple at <see cref="Start"/>.</summary>
    public int TempleIndex1;

    /// <summary>Index of the temple at <see cref="End"/>.</summary>
    public int TempleIndex2;

    /// <summary>True for a major ley line (connects opposite temples) — rendered more prominently.</summary>
    public bool Major;

    /// <summary>True if this corridor runs to the central Halls of Amenti.</summary>
    public bool AmentiPath;
}
