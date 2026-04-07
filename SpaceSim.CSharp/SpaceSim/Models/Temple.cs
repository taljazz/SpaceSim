namespace SpaceSim.Models;

public class Temple
{
    public float[] Position = Vec5.Zero();
    public float Frequency;
    public string Type = "temple";
    public string TempleType = "minor"; // "minor" or "master"
    public int KeyIndex;
    public string KeyName = "";
}
