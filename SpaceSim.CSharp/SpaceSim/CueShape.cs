using System;

namespace SpaceSim;

/// <summary>
/// Pure shaping for the tune-by-ear cue (unit-tested). From the SIGNED detune of the selected realm
/// against its still centre it derives everything the player needs to hear: a full-band closeness
/// (warmer as you near the target), a countable pulse rate (slowing to almost-still at the lock), a
/// lock-fill blend, and a discrete direction (sharp / flat / centred). The old cue conveyed only
/// magnitude within a narrow band and was sign-symmetric, so the player could hear neither how far they
/// were across most of the range nor which way to tune; this helper fixes both.
/// </summary>
public static class CueShape
{
    /// <summary>The shaped cue for one buffer.</summary>
    public readonly struct Cue
    {
        /// <summary>0 (cold/far) .. 1 (locked) — drives the cue's loudness.</summary>
        public readonly float Closeness;
        /// <summary>Tremolo pulse rate (Hz): slow near lock, brisk-but-countable far (kept under the flutter-fusion limit).</summary>
        public readonly float PulseRate;
        /// <summary>0..1 fill toward a steady (un-pulsed) tone at the lock.</summary>
        public readonly float LockBlend;
        /// <summary>+1 sharp (drive above target → tune down), -1 flat (tune up), 0 centred.</summary>
        public readonly int Direction;

        public Cue(float closeness, float pulseRate, float lockBlend, int direction)
        {
            Closeness = closeness;
            PulseRate = pulseRate;
            LockBlend = lockBlend;
            Direction = direction;
        }
    }

    /// <param name="signedDelta">RDrive minus the realm's still-centre target, in Hz. Positive = sharp.</param>
    public static Cue Compute(float signedDelta)
    {
        float ad = MathF.Abs(signedDelta);

        // Coarse "hot/cold" across a useful operating range (NOT the whole ~850 Hz band, which would be
        // nearly flat where the player actually tunes). Monotonic, reaches 0 at CueCoarseRange.
        float closeness = Math.Clamp(1f - ad / GameConstants.CueCoarseRange, 0f, 1f);

        // Countable pulse: slow at the lock, rising to a brisk-but-still-countable rate at/beyond the
        // fine band (TremoloMax is held under ~12 Hz, where pulses fuse into a buzz).
        float fineFrac = Math.Clamp(ad / GameConstants.BeatCueRange, 0f, 1f);
        float pulseRate = GameConstants.TremoloMin + (GameConstants.TremoloMax - GameConstants.TremoloMin) * fineFrac;

        // Fill toward a steady tone right at the lock.
        float lockBlend = Math.Clamp(1f - ad / GameConstants.BeatLockZone, 0f, 1f);

        // Direction: silent (centred) within a small deadband so it doesn't dither at the lock.
        int direction = ad < GameConstants.CueDeadband ? 0 : (signedDelta > 0f ? 1 : -1);

        return new Cue(closeness, pulseRate, lockBlend, direction);
    }
}
