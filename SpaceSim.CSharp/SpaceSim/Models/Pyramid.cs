namespace SpaceSim.Models;

/// <summary>
/// A pyramid resonance chamber (118 Hz). Tuning to its frequency nearby grants greatly amplified
/// healing and faster consciousness growth.
/// </summary>
public class Pyramid : WorldObject
{
    /// <summary>The pyramid's resonance frequency (Hz) — around the Great Pyramid's 118 Hz.</summary>
    public float Frequency;

    /// <summary>Display name of the pyramid.</summary>
    public string Name = "";

    /// <summary>Index of this pyramid within the generated set.</summary>
    public int Index;

    /// <summary>Flavour-text description for the screen reader.</summary>
    public string Desc = "";
}
