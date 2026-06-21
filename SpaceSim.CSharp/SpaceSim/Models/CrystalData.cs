namespace SpaceSim.Models;

/// <summary>
/// One collectible crystal sitting on a landed planet's exploration grid. Its frequency profile
/// decides its chakra type and bonus when picked up.
/// </summary>
public class CrystalData
{
    /// <summary>The 5D frequency signature of this crystal.</summary>
    public float[] Freqs = Vec5.Zero();

    /// <summary>The rare Atlantean crystal type (e.g. "moldavite"), or null for an ordinary crystal.</summary>
    public string? AtlanteanType;

    /// <summary>True if this is a special/rare crystal with a unique effect rather than a plain one.</summary>
    public bool Special;
}
