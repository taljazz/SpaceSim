using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;

namespace SpaceSim;

public partial class Ship
{
    // =========================================================================
    //  UPGRADE APPLY
    // =========================================================================

    private void ApplyUpgrade()
    {
        var upgrade = _upgrades[HudIndex];
        if (CrystalsCollected >= upgrade.Cost)
        {
            upgrade.Effect();
            CrystalsCollected -= upgrade.Cost;
            Speak($"{upgrade.Name} upgraded. Cost: {upgrade.Cost} crystals.");
        }
        else
        {
            Speak("Insufficient crystals.");
        }
    }

    // =========================================================================
    //  HUD / STARMAP / RIFT MENUS
    // =========================================================================

    public void UpdateHudItems(bool upgrade = false)
    {
        HudItems.Clear();
        if (upgrade)
        {
            foreach (var u in _upgrades)
                HudItems.Add($"{u.Name}: {u.Desc} Cost: {u.Cost}");
        }
        else
        {
            HudItems.Add($"Selected Realm: {SelectedDim + 1}");
            HudItems.Add($"Drive Freq: {RDrive[SelectedDim]:F2} Hz");
            HudItems.Add($"Target Freq: {FTarget[SelectedDim]:F2} Hz");
            HudItems.Add($"Harmonic Alignment: {ResonanceLevels[SelectedDim]:F2}");
            HudItems.Add($"Speed: {Vec5.Norm(Velocity):F2} u/s");
            HudItems.Add($"Vol: {(int)(_audio.MasterVolume * 100)}%");
            HudItems.Add($"Integrity: {ResonanceIntegrity:F2}");
            HudItems.Add($"Atlantean Crystals: {CrystalsCollected}");
            HudItems.Add($"Status: {(LandedMode ? "Anchored" : "In Flight")}");
            HudItems.Add($"Power: {ResonancePower.Average():F2}");
            HudItems.Add($"Tuaoi Mode: {TuaoiMode}");
            HudItems.Add($"Merkaba: {(MerkabaActive ? "Active" : "Inactive")}");
            HudItems.Add($"Temple Resonance: {(InTempleResonance ? "Active" : "Inactive")}");
            HudItems.Add($"Tuning Mode: {(TuningMode ? "Resonance (all realms)" : "Manual (higher realms only)")}");
            if (!TuningMode)
                HudItems.Add($"Speed Mode: {GameConstants.SpeedModeNames[SpeedMode]}");

            if (LandedMode)
            {
                HudItems.Add($"Cursor Pos: [{CursorPos[0]:F1}, {CursorPos[1]:F1}]");
                HudItems.Add($"Crystals Left: {CrystalCount - LockedCrystals.Count}");
            }
        }
    }

    private void SpeakHudItem()
    {
        if (HudItems.Count == 0) return;
        Speak(HudItems[HudIndex]);
    }

    public void UpdateStarmapItems(List<CelestialBody> stars, List<CelestialBody> planets, List<CelestialBody> nebulae)
    {
        StarmapItems.Clear();
        if (LockedTarget != null && !LockedIsRift)
            StarmapItems.Add(new StarmapItem { Label = "Unlock target", Position = null, ItemType = null, ItemRift = null });

        var items = new List<(float Dist, StarmapItem Item)>();

        for (int i = 0; i < stars.Count; i++)
        {
            float dist = Vec5.Distance(Position, stars[i].Position);
            if (dist < GameConstants.ScannerRange)
            {
                var proj = ProjectRelative(stars[i].Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                var sType = stars[i].StellarClass ?? StellarType.MainSequence;
                string sDesc = GameConstants.StellarTypes[sType].Desc;
                string label = $"Star {i + 1} ({sDesc}) at dist {dist:F1}, angle {angle:F1} degrees (unlandable)";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(stars[i].Position), ItemType = "star", ItemRift = null }));
            }
        }

        for (int i = 0; i < planets.Count; i++)
        {
            float dist = Vec5.Distance(Position, planets[i].Position);
            if (dist < GameConstants.ScannerRange)
            {
                var proj = ProjectRelative(planets[i].Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                var eType = planets[i].ExoplanetClass ?? ExoplanetType.SuperEarth;
                string eDesc = GameConstants.ExoplanetTypes[eType].Desc;
                string label = $"Planet {i + 1} ({eDesc}) at dist {dist:F1}, angle {angle:F1} degrees";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(planets[i].Position), ItemType = "planet", ItemRift = null }));
            }
        }

        for (int i = 0; i < nebulae.Count; i++)
        {
            float dist = Vec5.Distance(Position, nebulae[i].Position);
            if (dist < GameConstants.ScannerRange)
            {
                var proj = ProjectRelative(nebulae[i].Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                var nType = nebulae[i].NebulaClass ?? NebulaType.Emission;
                string nDesc = GameConstants.NebulaTypes[nType].Desc;
                string label = $"Nebula {i + 1} ({nDesc}) at dist {dist:F1}, angle {angle:F1} degrees (unlandable)";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(nebulae[i].Position), ItemType = "nebula", ItemRift = null }));
            }
        }

        for (int i = 0; i < Rifts.Count; i++)
        {
            float dist = Vec5.Distance(Position, Rifts[i].Position);
            if (dist < GameConstants.ScannerRange)
            {
                var proj = ProjectRelative(Rifts[i].Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                string label = $"Rift {i + 1} ({Rifts[i].RiftKind}) at dist {dist:F1}, angle {angle:F1} degrees";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(Rifts[i].Position), ItemType = "rift", ItemRift = Rifts[i] }));
            }
        }

        items.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        foreach (var (_, item) in items)
            StarmapItems.Add(item);

        if (StarmapItems.Count == 0)
            StarmapItems.Add(new StarmapItem { Label = "No objects in scanner range.", Position = null, ItemType = null, ItemRift = null });
    }

    private void SpeakStarmapItem()
    {
        if (StarmapItems.Count == 0) return;
        Speak(StarmapItems[StarmapIndex].Label);
    }

    private void LockOnStarmapItem()
    {
        var sel = StarmapItems[StarmapIndex];
        if (sel.Label == "Unlock target")
        {
            LockedTarget = null;
            LockedIsRift = false;
            _approachedRiftAnnounced = false;
            StopLockSound();
            Speak("Target unlocked.");
            return;
        }
        if (sel.Position == null) return;

        LockedTarget = sel.Position;
        LockedIsRift = sel.ItemType == "rift";
        LockedRift = LockedIsRift ? sel.ItemRift : null;

        float[] waveform = LockedIsRift ? _audio.RiftBeepWaveform : _audio.BeepWaveform;
        StopLockSound();
        LockSound = new GameSoundEffect(waveform, loop: true, volume: _audio.BeepVolume);
        _audio.AddSoundEffect(LockSound);
        _approachedRiftAnnounced = false;

        string name = sel.Label.Contains(" at") ? sel.Label[..sel.Label.IndexOf(" at")] : sel.Label;
        Speak($"Locked on to {name}.");
    }

    public void UpdateRiftItems()
    {
        RiftItems.Clear();
        if (LockedRift != null)
            RiftItems.Add(new RiftMenuItem { Label = "Unlock rift", Position = null, RiftType = null, Rift = null });

        var items = new List<(float Dist, RiftMenuItem Item)>();
        for (int i = 0; i < Rifts.Count; i++)
        {
            float dist = Vec5.Distance(Position, Rifts[i].Position);
            var proj = ProjectRelative(Rifts[i].Position);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            string label = $"Rift {i + 1} ({Rifts[i].RiftKind}) at dist {dist:F1}, angle {angle:F1} degrees";
            items.Add((dist, new RiftMenuItem { Label = label, Position = Vec5.Clone(Rifts[i].Position), RiftType = Rifts[i].RiftKind.ToString(), Rift = Rifts[i] }));
        }
        items.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        foreach (var (_, item) in items) RiftItems.Add(item);

        if (RiftItems.Count == 0)
            RiftItems.Add(new RiftMenuItem { Label = "No rifts detected.", Position = null, RiftType = null, Rift = null });
    }

    private void SpeakRiftItem()
    {
        if (RiftItems.Count == 0) return;
        Speak(RiftItems[RiftSelectionIndex].Label);
    }

    private void LockOnRiftItem()
    {
        var sel = RiftItems[RiftSelectionIndex];
        if (sel.Label == "Unlock rift")
        {
            LockedRift = null;
            LockedTarget = null;
            LockedIsRift = false;
            _approachedRiftAnnounced = false;
            StopLockSound();
            Speak("Rift unlocked.");
            return;
        }
        if (sel.Position == null) return;

        LockedRift = sel.Rift;
        LockedTarget = sel.Position;
        LockedIsRift = true;
        StopLockSound();
        LockSound = new GameSoundEffect(_audio.RiftBeepWaveform, loop: true, volume: _audio.BeepVolume);
        _audio.AddSoundEffect(LockSound);
        _approachedRiftAnnounced = false;

        string name = sel.Label.Contains(" at") ? sel.Label[..sel.Label.IndexOf(" at")] : sel.Label;
        Speak($"Locked on to {name} for beeping and navigation.");
    }
}
