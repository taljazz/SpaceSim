namespace SpaceSim.Models;

public class LeyLine
{
    public float[] Start = Vec5.Zero();
    public float[] End = Vec5.Zero();
    public float Frequency;
    public string Type = "ley_line";
    public string Name = "";
    public int TempleIndex1;
    public int TempleIndex2;
    public bool Major;
    public bool AmentiPath;
}
