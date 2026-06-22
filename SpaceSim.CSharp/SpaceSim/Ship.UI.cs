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
    #region Upgrade apply

    /// <summary>
    /// Apply the upgrade highlighted in the attunement menu, spending its crystal cost. Does nothing
    /// (beyond an "insufficient crystals" announcement) when the player can't afford it.
    /// </summary>
    internal void ApplyUpgrade()
    {
        var upgrade = _upgrades[HudIndex];
        if (CrystalsCollected >= upgrade.Cost)
        {
            upgrade.Effect();
            CrystalsCollected -= upgrade.Cost;
            Speak($"{upgrade.Name} attuned. Cost: {upgrade.Cost} crystals.");
        }
        else
        {
            Speak("Insufficient crystals.");
        }
    }

    #endregion

    #region HUD / starmap / rift menus

    /// <summary>Open a menu: make it active, build its rows, select the first, and announce it.</summary>
    private void OpenMenu(MenuMode menu)
    {
        ActiveMenu = menu;
        menu.Populate();
        menu.SelectedIndex = 0;
        menu.SpeakCurrent();
    }

    /// <summary>
    /// Rebuild the rows shown by the status/upgrade menu. When <paramref name="upgrade"/> is true the
    /// rows are the purchasable attunements; otherwise they are the live ship read-out (selected realm,
    /// frequencies, integrity, Tuaoi mode, and — when anchored — cursor position and crystals left).
    /// </summary>
    /// <param name="upgrade">True to list upgrades, false to list the status read-out.</param>
    public void UpdateHudItems(bool upgrade = false)
    {
        HudItems.Clear();
        if (upgrade)
        {
            // Attunement menu: one row per upgrade, showing its description and crystal cost.
            foreach (var u in _upgrades)
                HudItems.Add($"{u.Name}: {u.Desc} Cost: {u.Cost}");
        }
        else
        {
            // Status read-out: a snapshot of the ship's current resonance/navigation state.
            HudItems.Add($"Selected Realm: {SelectedDim + 1}");
            HudItems.Add($"Drive Freq: {RDrive[SelectedDim]:F2} Hz");
            HudItems.Add($"Target Freq: {FTarget[SelectedDim]:F2} Hz");
            HudItems.Add($"Resonance: {ResonanceLevels[SelectedDim] * 100f:F0} percent");
            HudItems.Add($"Speed: {Vec5.Norm(Velocity):F2} u/s");
            HudItems.Add($"Vol: {(int)(_audio.MasterVolume * 100)}%");
            HudItems.Add($"Integrity: {ResonanceIntegrity * 100f:F0} percent");
            HudItems.Add($"Atlantean Crystals: {CrystalsCollected}");
            HudItems.Add($"Status: {(LandedMode ? "Anchored" : "In Flight")}");
            HudItems.Add($"Power: {ResonancePower.Average() * 100f:F0} percent");
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

    /// <summary>Announce the currently highlighted status/upgrade row via the screen reader.</summary>
    internal void SpeakHudItem()
    {
        if (HudItems.Count == 0) return;
        Speak(HudItems[HudIndex]);
    }

    /// <summary>
    /// Rebuild the starmap scanner list: every star, planet, nebula, and rift within scanner range,
    /// plus every temple and pyramid (always listed, as the Atlantean objectives), each labelled with
    /// its type, distance, and bearing, then sorted nearest-first. Prepends an "Unlock target" row when
    /// a non-rift target is currently locked, and a placeholder when empty.
    /// </summary>
    public void UpdateStarmapItems(List<CelestialBody> stars, List<CelestialBody> planets, List<CelestialBody> nebulae)
    {
        StarmapItems.Clear();
        if (LockedTarget != null && !LockedIsRift)
            StarmapItems.Add(new StarmapItem { Label = "Unlock target", IsUnlockAction = true });

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
                string label = $"Star {i + 1} ({sDesc}) at dist {dist:F1}, angle {angle:F1} degrees (cannot anchor)";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(stars[i].Position), Kind = StarmapItemKind.Star }));
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
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(planets[i].Position), Kind = StarmapItemKind.Planet }));
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
                string label = $"Nebula {i + 1} ({nDesc}) at dist {dist:F1}, angle {angle:F1} degrees (cannot anchor)";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(nebulae[i].Position), Kind = StarmapItemKind.Nebula }));
            }
        }

        for (int i = 0; i < Rifts.Count; i++)
        {
            float dist = Vec5.Distance(Position, Rifts[i].Position);
            if (dist < GameConstants.ScannerRange)
            {
                var proj = ProjectRelative(Rifts[i].Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                string label = $"Harmonic Chamber {i + 1} ({Rifts[i].RiftKind}) at dist {dist:F1}, angle {angle:F1} degrees";
                items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(Rifts[i].Position), Kind = StarmapItemKind.Rift, ItemRift = Rifts[i] }));
            }
        }

        // Temples and pyramids are the Atlantean objectives — always listed regardless of distance, so the
        // player can pick one and autopilot to it from anywhere. Minor temples show the frequency to tune to
        // (or that their key is already collected); the master temple is the Halls of Amenti.
        foreach (var temple in Temples)
        {
            float dist = Vec5.Distance(Position, temple.Position);
            var proj = ProjectRelative(temple.Position);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            // Keep the name first and any frequency/status clause LAST, so the lock-confirmation name
            // parse in LockOnStarmapItem (which trims at " at") reads back a clean object name.
            string label;
            if (temple.Kind == TempleType.Master)
                label = $"Halls of Amenti at dist {dist:F1}, angle {angle:F1} degrees";
            else if (TempleKeys.Contains(temple.KeyIndex))
                label = $"Temple of {temple.KeyName} at dist {dist:F1}, angle {angle:F1} degrees, key collected";
            else
                label = $"Temple of {temple.KeyName} at dist {dist:F1}, angle {angle:F1} degrees, tune a realm to {temple.Frequency:F0} hertz for the key";
            items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(temple.Position), Kind = StarmapItemKind.Temple }));
        }

        foreach (var pyramid in Pyramids)
        {
            float dist = Vec5.Distance(Position, pyramid.Position);
            var proj = ProjectRelative(pyramid.Position);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            string label = $"{pyramid.Name} at dist {dist:F1}, angle {angle:F1} degrees, tune a realm to {pyramid.Frequency:F0} hertz to activate";
            items.Add((dist, new StarmapItem { Label = label, Position = Vec5.Clone(pyramid.Position), Kind = StarmapItemKind.Pyramid }));
        }

        // Present nearest objects first so the most relevant targets are at the top of the list.
        items.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        foreach (var (_, item) in items)
            StarmapItems.Add(item);

        if (StarmapItems.Count == 0)
            StarmapItems.Add(new StarmapItem { Label = "No objects in scanner range." });
    }

    /// <summary>Announce the currently highlighted starmap row via the screen reader.</summary>
    internal void SpeakStarmapItem()
    {
        if (StarmapItems.Count == 0) return;
        Speak(StarmapItems[StarmapIndex].Label);
    }

    /// <summary>
    /// Act on the highlighted starmap row: either unlock the current target, or lock onto the selected
    /// object — starting a looping homing beep (rift-flavoured for rifts) for audio navigation.
    /// </summary>
    internal void LockOnStarmapItem()
    {
        var sel = StarmapItems[StarmapIndex];
        if (sel.IsUnlockAction)
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
        LockedIsRift = sel.Kind == StarmapItemKind.Rift;
        LockedRift = LockedIsRift ? sel.ItemRift : null;
        _approachedRiftAnnounced = false;
        // Start the homing beacon now so the lock is confirmed audibly even while the menu is still
        // open (Ship.Update, which then keeps it positioned, is skipped during in-sim menus).
        UpdateWorldLoop(ref LockSound, LockedIsRift ? _audio.RiftBeepWaveform : _audio.BeepWaveform,
                        LockedTarget, _audio.BeepVolume);

        string name = sel.Label.Contains(" at") ? sel.Label[..sel.Label.IndexOf(" at")] : sel.Label;
        Speak($"Locked on to {name}.");
    }

    /// <summary>
    /// Rebuild the rift-selection list: every known rift labelled with its kind, distance, and bearing,
    /// sorted nearest-first. Prepends an "Unlock rift" row when a rift is locked, placeholder when empty.
    /// </summary>
    public void UpdateRiftItems()
    {
        RiftItems.Clear();
        if (LockedRift != null)
            RiftItems.Add(new RiftMenuItem { Label = "Unlock Harmonic Chamber", IsUnlockAction = true });

        var items = new List<(float Dist, RiftMenuItem Item)>();
        for (int i = 0; i < Rifts.Count; i++)
        {
            float dist = Vec5.Distance(Position, Rifts[i].Position);
            var proj = ProjectRelative(Rifts[i].Position);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            string label = $"Harmonic Chamber {i + 1} ({Rifts[i].RiftKind}) at dist {dist:F1}, angle {angle:F1} degrees";
            items.Add((dist, new RiftMenuItem { Label = label, Position = Vec5.Clone(Rifts[i].Position), Rift = Rifts[i] }));
        }
        items.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        foreach (var (_, item) in items) RiftItems.Add(item);

        if (RiftItems.Count == 0)
            RiftItems.Add(new RiftMenuItem { Label = "No Harmonic Chambers detected." });
    }

    /// <summary>Announce the currently highlighted rift row via the screen reader.</summary>
    internal void SpeakRiftItem()
    {
        if (RiftItems.Count == 0) return;
        Speak(RiftItems[RiftSelectionIndex].Label);
    }

    /// <summary>
    /// Act on the highlighted rift row: either unlock the current rift, or lock onto the selected one —
    /// starting the looping rift homing beep for audio navigation toward the harmonic chamber.
    /// </summary>
    internal void LockOnRiftItem()
    {
        var sel = RiftItems[RiftSelectionIndex];
        if (sel.IsUnlockAction)
        {
            LockedRift = null;
            LockedTarget = null;
            LockedIsRift = false;
            _approachedRiftAnnounced = false;
            StopLockSound();
            Speak("Harmonic Chamber unlocked.");
            return;
        }
        if (sel.Position == null) return;

        LockedRift = sel.Rift;
        LockedTarget = sel.Position;
        LockedIsRift = true;
        _approachedRiftAnnounced = false;
        // Start the homing beacon now (see LockOnStarmapItem) so the lock beeps even inside the menu.
        UpdateWorldLoop(ref LockSound, _audio.RiftBeepWaveform, LockedTarget, _audio.BeepVolume);

        string name = sel.Label.Contains(" at") ? sel.Label[..sel.Label.IndexOf(" at")] : sel.Label;
        Speak($"Locked on to {name} for beeping and navigation.");
    }

    #endregion
}
