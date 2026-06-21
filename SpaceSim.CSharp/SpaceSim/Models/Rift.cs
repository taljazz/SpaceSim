namespace SpaceSim.Models;

public class Rift : WorldObject
{
    public RiftType RiftKind = RiftType.Normal;
    public float Timer;
    public GameSoundEffect? Sound;
    public float LastBeepTime;
}
