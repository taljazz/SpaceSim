namespace SpaceSim.Models;

public class Temple : WorldObject
{
    public float Frequency;
    public TempleType Kind = TempleType.Minor;
    public int KeyIndex;
    public string KeyName = "";
}
