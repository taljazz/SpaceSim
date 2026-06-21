namespace SpaceSim.Models;

/// <summary>
/// A dimensional rift (a "Harmonic Chamber") the player can charge into for a warp or a bonus.
/// Rifts spawn at random, live for a limited time, and emit a beeping locator sound.
/// </summary>
public class Rift : WorldObject
{
    /// <summary>Which flavour of rift this is — decides the reward on entry (boost, crystal, hazard, …).</summary>
    public RiftType RiftKind = RiftType.Normal;

    /// <summary>Seconds of life left before the rift fades away.</summary>
    public float Timer;

    /// <summary>The looping locator hum currently playing for this rift, if any (a positional world sound).</summary>
    public WorldSound? Sound;

    /// <summary>Simulation time of the last locator beep, used to space them out.</summary>
    public float LastBeepTime;
}
