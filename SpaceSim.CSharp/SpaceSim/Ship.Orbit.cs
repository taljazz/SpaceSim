using System;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// The orbit mechanic: lock a celestial object in the scanner, then circle it (O key) rather than flying
/// straight in and stopping. This is the only reliable way to stay with a planet, which is forever drifting
/// along its own orbit around its star — fly to a snapshot of where it was and you just arrive in empty space.
/// </summary>
public partial class Ship
{
    #region Orbit state

    /// <summary>The live body behind the current lock, so a moving target stays tracked (null for static targets and rifts).</summary>
    public CelestialBody? LockedBody;

    /// <summary>True while the ship is circling its locked target instead of flying toward it.</summary>
    public bool IsOrbiting;

    private float _orbitAngle;    // current angle around the target in the horizontal (X / Z) plane
    private float _orbitRadiusH;  // orbit radius in that plane
    private float _orbitOff1, _orbitOff3, _orbitOff4;  // fixed offsets in the other realms, preserved while orbiting

    #endregion

    #region Orbit control

    /// <summary>
    /// O key. Enters orbit around the currently locked object, or — if already orbiting — breaks free and
    /// hands control back to manual flight (releasing the lock too, so the autopilot doesn't re-engage).
    /// </summary>
    public void ToggleOrbit()
    {
        if (IsOrbiting)
        {
            IsOrbiting = false;
            LockedTarget = null;
            LockedBody = null;
            LockedIsRift = false;
            StopLockSound();
            SpeakNav("Breaking orbit. Manual flight resumed.");
            return;
        }

        if (LockedTarget == null)
        {
            SpeakNav("Lock onto an object in the scanner first, then press O to orbit it.");
            return;
        }
        if (LockedIsRift)
        {
            SpeakNav("Harmonic Chambers cannot be orbited. Charge through one with E instead.");
            return;
        }

        EnterOrbit();
        SpeakNav("Entering orbit. Press O again to break free.");
    }

    /// <summary>Sets up the orbit from the ship's current position relative to the locked target.</summary>
    private void EnterOrbit()
    {
        IsOrbiting = true;
        float[] c = LockedTarget!;
        float dx = Position[0] - c[0];
        float dz = Position[2] - c[2];
        _orbitRadiusH = MathF.Sqrt(dx * dx + dz * dz);
        if (_orbitRadiusH < GameConstants.OrbitMinRadius) _orbitRadiusH = GameConstants.OrbitMinRadius;
        _orbitAngle = MathF.Atan2(dz, dx);
        _orbitOff1 = Position[1] - c[1];
        _orbitOff3 = Position[3] - c[3];
        _orbitOff4 = Position[4] - c[4];
        DebugLogger.Log("Orbit", $"ENTER around {Vec5.Format(c)} radius={_orbitRadiusH:F1}");
    }

    /// <summary>
    /// Called each frame from <see cref="Update"/>: holds the ship on a circular path in the horizontal plane
    /// around the (live, moving) locked target, preserving its offset in the other realms.
    /// </summary>
    private void UpdateOrbit(float dt)
    {
        if (!IsOrbiting) return;
        // The lock can vanish (target unlocked) or we may have anchored — either way, stop orbiting.
        if (LockedTarget == null || LandedMode) { IsOrbiting = false; return; }

        // Cap the angular rate so a wide orbit (e.g. pressing O while still far out) never whips the ship
        // around faster than its top speed; close orbits use the full, gentle rate.
        float omega = MathF.Min(GameConstants.OrbitAngularSpeed, MaxVelocity / MathF.Max(_orbitRadiusH, 0.01f));
        _orbitAngle += omega * dt;
        if (_orbitAngle > MathF.Tau) _orbitAngle -= MathF.Tau;

        float[] c = LockedTarget;
        Position[0] = c[0] + _orbitRadiusH * MathF.Cos(_orbitAngle);
        Position[2] = c[2] + _orbitRadiusH * MathF.Sin(_orbitAngle);
        Position[1] = c[1] + _orbitOff1;
        Position[3] = c[3] + _orbitOff3;
        Position[4] = c[4] + _orbitOff4;
        for (int i = 0; i < N; i++)
            Position[i] = ((Position[i] + 100f) % 200f + 200f) % 200f - 100f;

        // Analytic orbital velocity (tangent to the circle) so engine Doppler and feel match the motion.
        float v = _orbitRadiusH * omega;
        Array.Clear(Velocity);
        Velocity[0] = -v * MathF.Sin(_orbitAngle);
        Velocity[2] = v * MathF.Cos(_orbitAngle);

        // Hold full resonance while orbiting (the orbit moves us, not the drives), so the engine stays in
        // tune and the player can anchor a planet straight out of orbit.
        for (int i = 0; i < N; i++) RDrive[i] = FTarget[i];
    }

    #endregion
}
