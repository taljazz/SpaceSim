namespace SpaceSim.Models;

/// <summary>
/// One of the 12 zodiac temples (or the central Halls of Amenti). Tuning to a temple's frequency
/// nearby awards its key; collecting all 12 keys unlocks the master temple.
/// </summary>
public class Temple : WorldObject
{
    /// <summary>The frequency (Hz) the player must resonate with to claim this temple's key.</summary>
    public float Frequency;

    /// <summary>Whether this is a minor (zodiac) temple or the master Halls of Amenti.</summary>
    public TempleType Kind = TempleType.Minor;

    /// <summary>Index of this temple's key in the 12-key collection.</summary>
    public int KeyIndex;

    /// <summary>Display name of the key (e.g. a zodiac sign).</summary>
    public string KeyName = "";
}
