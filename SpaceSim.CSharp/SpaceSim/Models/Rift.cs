namespace SpaceSim.Models;

public class Rift
{
    public float[] Position = Vec5.Zero();
    public float Timer;
    public string Type = "normal";
    public GameSoundEffect? Sound;
    public float LastBeepTime;
}
