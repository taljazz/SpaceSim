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
    #region Input Handling

    /// <summary>
    /// The per-frame keyboard dispatcher — the single place every player action enters the game. Reads
    /// the current and previous keyboard state (so it can tell a fresh key-press from a held key), then
    /// routes to the right behaviour: menu navigation when a menu is open, otherwise the full
    /// flight/tuning/exploration control set. (The game is keyboard-only; there is no mouse input.)
    /// </summary>
    /// <param name="keys">This frame's keyboard snapshot.</param>
    /// <param name="prevKeys">Last frame's keyboard snapshot, used for edge (just-pressed) detection.</param>
    /// <param name="stars">Star bodies (passed through for context-sensitive actions).</param>
    /// <param name="planets">Planet bodies (passed through for context-sensitive actions).</param>
    /// <param name="nebulae">Nebula bodies (passed through for context-sensitive actions).</param>
    public void HandleInput(KeyboardState keys, KeyboardState prevKeys,
                            List<CelestialBody> stars, List<CelestialBody> planets,
                            List<CelestialBody> nebulae)
    {
        // Edge-detection helper
        bool IsKeyPressed(Keys k) => keys.IsKeyDown(k) && !prevKeys.IsKeyDown(k);
        bool IsKeyReleased(Keys k) => !keys.IsKeyDown(k) && prevKeys.IsKeyDown(k);

        // Update input time for idle detection
        if (keys.GetPressedKeyCount() > 0)
        {
            _lastInputTime = SimulationTime;
            if (IdleMode)
            {
                IdleMode = false;
                Speak("Resuming active control.");
            }
        }

        // --- Menu mode input ---
        if (IsInMenuMode)
        {
            HandleMenuInput(IsKeyPressed);
            return;
        }

        bool shiftPressed = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        bool ctrlPressed = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool altPressed = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);

        #region Number keys (presets / dimension select)

        // --- Number keys ---
        // 1-9 are overloaded by modifier: Ctrl = save preset, Shift = recall preset,
        // bare 1-5 = pick which dimension the Up/Down tuning keys act on.
        Keys[] numKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 };
        for (int idx = 0; idx < numKeys.Length; idx++)
        {
            if (!IsKeyPressed(numKeys[idx])) continue;
            int slot = idx + 1;

            if (ctrlPressed)
            {
                // Save frequency preset
                if (FrequencyPresets.ContainsKey(slot))
                {
                    if (_pendingPresetOverwrite == slot && (SimulationTime - _pendingPresetTime) < 3f)
                    {
                        FrequencyPresets[slot] = (float[])RDrive.Clone();
                        Speak($"Preset {slot} overwritten. Tones: {FormatFreqs(RDrive)}.");
                        _pendingPresetOverwrite = null;
                    }
                    else
                    {
                        _pendingPresetOverwrite = slot;
                        _pendingPresetTime = SimulationTime;
                        Speak($"Preset {slot} already exists. Press Control plus {slot} again to overwrite.");
                    }
                }
                else
                {
                    FrequencyPresets[slot] = (float[])RDrive.Clone();
                    Speak($"Preset {slot} saved. Tones: {FormatFreqs(RDrive)}.");
                    _pendingPresetOverwrite = null;
                }
            }
            else if (shiftPressed)
            {
                // Recall preset
                if (FrequencyPresets.TryGetValue(slot, out var preset))
                {
                    Array.Copy(preset, RDrive, N);
                    Speak($"Preset {slot} recalled. Tones set to: {FormatFreqs(RDrive)}.");
                }
                else
                    Speak($"Preset {slot} is empty. Use Control plus {slot} to save current frequencies.");
            }
            else
            {
                // Dimension selection. In full tuning mode every realm is yours, so any of 1-5 selects. In
                // normal flight only the two higher realms (4 and 5) are hand-tunable; pressing 1-3 then
                // explains that those realms fly themselves rather than selecting them.
                if (slot <= 5)
                {
                    if (!TuningMode && !LandedMode && slot <= 3)
                    {
                        Speak($"Realm {slot} flies itself as you move. Press 4 or 5 to tune a higher realm by ear.");
                    }
                    else
                    {
                        SelectedDim = slot - 1;
                        LastTuneTime = SimulationTime; // selecting a realm wakes the by-ear cue
                        // Always spoken — tuning is essential and never filtered by a buffer. With tune-by-ear
                        // off, newcomers also hear the realm's current tone to start from; by ear, just the realm.
                        Speak(ByEarMode
                            ? $"Tuning Realm {slot}."
                            : $"Tuning Realm {slot}. Current tone {RDrive[slot - 1]:F0}.");
                        _approachingLockAnnounced = false;
                    }
                }
            }
        }

        #endregion

        #region Single-press action keys

        // --- Single press keys ---
        if (IsKeyPressed(Keys.J))
        {
            TuningMode = !TuningMode;
            // Leaving full tuning with a spatial realm selected would strand Up/Down (in normal flight only
            // the higher realms are hand-tunable), so snap the selection back to a higher realm.
            if (!TuningMode && SelectedDim < 3) SelectedDim = 3;
            string modeName = TuningMode ? "Resonance tuning mode" : "Manual mode";
            Speak($"Toggled to {modeName}.");
            DebugLogger.Log("Input", $"Tuning mode toggled: {modeName}");
        }

        if (IsKeyPressed(Keys.V))
        {
            VerboseMode = (VerboseMode + 1) % 3;
            string[] modes = { "Low", "Medium", "High" };
            SpeakSystem($"Verbosity mode: {modes[VerboseMode]}.");
        }

        if (IsKeyPressed(Keys.G) && SimulationTime - _lastTuaoiSwitch > GameConstants.TuaoiModeSwitchCooldown)
        {
            TuaoiModeIndex = (TuaoiModeIndex + 1) % GameConstants.TuaoiModeOrder.Length;
            SetTuaoiMode(GameConstants.TuaoiModeOrder[TuaoiModeIndex]);
            SpeakAtlantean($"The Tuaoi turns to its {TuaoiMode} face. {Capitalize(_cachedTuaoiInfo.Desc)}.");
            DebugLogger.Log("Input", $"Tuaoi mode switched to: {TuaoiMode}");
            _lastTuaoiSwitch = SimulationTime;
        }

        if (IsKeyPressed(Keys.M))
            OpenMenu(new StarmapMenuMode(this));

        if (IsKeyPressed(Keys.Q))
        {
            if (ByEarMode)
                Speak($"Realm {SelectedDim + 1}: {ResonanceWord(ResonanceLevels[SelectedDim])}.");
            else
                Speak($"The selected realm's true note: {FTarget[SelectedDim]:F2}.");
        }

        if (IsKeyPressed(Keys.N))
        {
            ByEarMode = !ByEarMode;
            Speak(ByEarMode
                ? "By ear mode on. Target tones will not be spoken; tune by the beat and by realm closeness."
                : "By ear mode off. Target tones will be spoken.");
        }

        #endregion

        #region Landing, takeoff, status & menus

        // Landing
        if (IsKeyPressed(Keys.L) && !LandedMode && !ctrlPressed)
        {
            float avgRes = Vec5.Mean(ResonanceLevels);
            float landingThreshold = GameConstants.LandingThreshold;
            if (NearestBody != null && NearestBody.BodyType == CelestialBodyType.Planet)
                landingThreshold *= NearestBody.Difficulty;

            if (NearObject && avgRes > landingThreshold && NearestBody != null && NearestBody.BodyType == CelestialBodyType.Planet)
            {
                LandingTimer = GameConstants.LandingTime;
                var eType = NearestBody.ExoplanetClass ?? ExoplanetType.SuperEarth;
                string eDesc = GameConstants.ExoplanetTypes[eType].Desc;
                SpeakNav($"Initiating anchoring sequence on {eDesc}.");
            }
            else
            {
                ApplyIntegrityDamage(0.01f);
                if (!NearObject)
                    Speak("No celestial body nearby for anchoring. Minor integrity loss.");
                else if (avgRes <= landingThreshold)
                    Speak("Resonance too low for anchoring. Minor integrity loss.");
                else
                    Speak("Cannot anchor on this object. Minor integrity loss.");
            }
        }

        // Takeoff. Capture the landed state first so the in-flight temple readout below can't ALSO fire on
        // the same T press that just took off (the takeoff clears LandedMode in this same frame).
        bool wasLandedForT = LandedMode;
        if (IsKeyPressed(Keys.T) && LandedMode && !shiftPressed)
        {
            LandedMode = false;
            LandedPlanet = null;
            LandedPlanetBody = null;
            LockedTarget = null;       // ascending frees you — don't auto-fly or orbit back to the planet you left
            LockedBody = null;
            IsOrbiting = false;
            StopLockSound();
            StopBiomeSound();
            GameEvents.RaiseLandingEvent(this, false);
            SpeakNav("Ascending from planet. Light vehicle disengaged.");
        }

        // Temple resonance reading — in flight only (T is takeoff while anchored), near a temple, report its
        // key. Plain T only: Shift+T is reserved for repeating the tutorial line.
        if (IsKeyPressed(Keys.T) && !wasLandedForT && !shiftPressed)
            ReportTempleResonance();

        // Status — a concise, meaningful read-out (no raw 5D vectors or radians to decode by ear).
        if (IsKeyPressed(Keys.R))
        {
            float avgResP = Vec5.Mean(ResonanceLevels) * 100f;
            float bearing = (ViewRotation * 180f / MathF.PI) % 360f;
            if (bearing < 0f) bearing += 360f;
            string status = $"Resonance {avgResP:F0} percent. Integrity {ResonanceIntegrity * 100f:F0} percent. " +
                            $"Speed {Vec5.Norm(Velocity):F1}. Heading {bearing:F0} degrees. " +
                            $"{(LandedMode ? "Anchored." : "In space.")} {CrystalsCollected} crystals.";
            Speak(status);
        }

        // Replay the last announcement (useful if a line was missed).
        if (IsKeyPressed(Keys.Tab))
            RepeatLastAnnouncement();

        // Orbit the locked object, or break a current orbit — a stable way to stay with a moving planet.
        if (IsKeyPressed(Keys.O) && !LandedMode)
            ToggleOrbit();

        // Speech buffers: [ and ] cycle which buffer is focused; holding Ctrl with them moves the focused
        // buffer to reorder the list; , and . browse the focused buffer's history.
        bool ctrlBuffer = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        if (IsKeyPressed(Keys.OemOpenBrackets)) { if (ctrlBuffer) MoveSpeechBuffer(-1); else CycleSpeechBuffer(-1); }
        if (IsKeyPressed(Keys.OemCloseBrackets)) { if (ctrlBuffer) MoveSpeechBuffer(+1); else CycleSpeechBuffer(+1); }
        if (IsKeyPressed(Keys.OemComma)) BrowseSpeechBuffer(-1);
        if (IsKeyPressed(Keys.OemPeriod)) BrowseSpeechBuffer(+1);

        // HUD / Upgrade menu. While anchored on a planet, U opens the attunement menu so crystals are
        // always spendable (no need to clear every crystal on the planet first — that gate was a
        // soft-lock); while flying it opens the HUD readout.
        if (IsKeyPressed(Keys.U))
        {
            if (LandedMode)
            {
                Speak($"Attunement menu. {CrystalsCollected} crystals available.");
                OpenMenu(new UpgradeMenuMode(this));
            }
            else
                OpenMenu(new HudMenuMode(this));
        }

        #endregion

        #region Rift interaction

        // Rift interaction
        if (IsKeyPressed(Keys.E) && !LandedMode)
        {
            if (LockedIsRift && LockedTarget != null)
            {
                float dist = Vec5.Distance(Position, LockedTarget);
                float avgRes = Vec5.Mean(ResonanceLevels);
                if (dist < GameConstants.RiftAlignmentTolerance && avgRes > GameConstants.RiftEntryResThreshold)
                {
                    if (LockedRift != null) EnterRift(LockedRift);
                }
                else if (dist < GameConstants.RiftAlignmentTolerance && avgRes > GameConstants.RiftEntryResThreshold / 2f)
                {
                    RiftChargeTimer = GameConstants.RiftChargeTime;
                    SpeakNav("Initiating Harmonic Chamber charge sequence.");
                }
                else
                    Speak("Approach closer or increase resonance to charge.");
            }
            else
            {
                if (Rifts.Count > 0)
                    OpenMenu(new RiftMenuMode(this));
                else
                    Speak("No Harmonic Chambers detected.");
            }
        }

        #endregion

        #region Speed, save/load & Atlantean actions

        // Speed mode toggle
        if (IsKeyPressed(Keys.Z) && !TuningMode)
        {
            SpeedMode = (SpeedMode + 1) % GameConstants.SpeedFactors.Length;
            SpeakSystem($"Speed mode toggled to {GameConstants.SpeedModeNames[SpeedMode]}.");
        }

        // Save/Load
        if (IsKeyPressed(Keys.S) && ctrlPressed) SaveGame();
        if (IsKeyPressed(Keys.L) && ctrlPressed) LoadGame();
        if (IsKeyPressed(Keys.A) && ctrlPressed)
        {
            AutosaveEnabled = !AutosaveEnabled;
            SpeakSystem($"Autosave {(AutosaveEnabled ? "enabled" : "disabled")}.");
        }

        // Portal anchors
        if (IsKeyPressed(Keys.P) && !shiftPressed && !LandedMode)
            CreatePortalAnchor();
        if (IsKeyPressed(Keys.P) && shiftPressed && !LandedMode)
            UsePortalAnchor();

        // Astral projection
        if (IsKeyPressed(Keys.B) && !LandedMode)
        {
            if (AstralMode) ExitAstralMode();
            else EnterAstralMode();
        }

        // Intention navigation
        if (IsKeyPressed(Keys.I) && !LandedMode && !IntentionActive)
            StartIntentionNavigation();

        #endregion

        #region Landed-mode controls (crystal grid)

        // Landed-mode specific
        if (LandedMode)
        {
            if (IsKeyPressed(Keys.F)) { ScanNearestCrystal(); _approachingLockAnnounced = false; }
            if (IsKeyPressed(Keys.X)) CollectCrystal();

            bool moved = false;
            if (IsKeyPressed(Keys.W)) { CursorPos[1] += 1; moved = true; }
            if (IsKeyPressed(Keys.S) && !ctrlPressed) { CursorPos[1] -= 1; moved = true; }
            if (IsKeyPressed(Keys.A) && !ctrlPressed) { CursorPos[0] -= 1; moved = true; }
            if (IsKeyPressed(Keys.D)) { CursorPos[0] += 1; moved = true; }

            if (moved)
            {
                CursorPos[0] = Math.Clamp(CursorPos[0], -GameConstants.GridSize, GameConstants.GridSize);
                CursorPos[1] = Math.Clamp(CursorPos[1], -GameConstants.GridSize, GameConstants.GridSize);
                if (SimulationTime - _lastCursorSpeakTime > GameConstants.CursorSpeechCooldown)
                {
                    Speak($"Cursor at [{CursorPos[0]:F1}, {CursorPos[1]:F1}].");
                    _lastCursorSpeakTime = SimulationTime;
                }
            }
        }

        #endregion

        #region Volume & water blessing

        // Volume controls
        HandleVolumeKeys(keys, prevKeys, shiftPressed, ctrlPressed, altPressed, IsKeyPressed);

        // Spacebar hold (water blessing)
        if (IsKeyPressed(Keys.Space))
        {
            _spacebarPressed = true;
            _spacebarHoldTimer = 0f;
        }
        if (IsKeyReleased(Keys.Space))
        {
            _spacebarPressed = false;
            _spacebarHoldTimer = 0f;
        }
        if (_spacebarPressed)
        {
            _spacebarHoldTimer += DT;
            if (_spacebarHoldTimer >= GameConstants.WaterBlessingHoldTime &&
                Vec5.All(ResonanceLevels, r => r > GameConstants.WaterBlessingResThreshold))
            {
                // Reward the demanding perfect-resonance ritual with a timed protective + healing aura.
                WaterBlessingTimer = GameConstants.WaterBlessingDuration;
                SpeakAtlantean("Water blessing activated. Your light vehicle is bathed in healing light, shielded and restored for one minute.");
                _spacebarPressed = false;
                _spacebarHoldTimer = 0f;
            }
        }

        #endregion

        #region Frequency tuning (Up/Down)

        // Tuning with Up/Down
        float rate = LandedMode ? GameConstants.TuningRatePlanet : GameConstants.TuningRate;
        if (LandedMode && CrystalPositions.Count > 0)
        {
            var dists = new float[CrystalPositions.Count];
            for (int i = 0; i < CrystalPositions.Count; i++)
                dists[i] = LockedCrystals.Contains(i) ? float.MaxValue : Dist2D(CursorPos, CrystalPositions[i]);
            int nearest = MathHelpers.ArgMin(dists);
            if (dists[nearest] < float.MaxValue)
            {
                float delta = MathF.Abs(RDrive[SelectedDim] - CrystalFreqs[nearest].Freqs[SelectedDim]);
                rate = GameConstants.TuningRatePlanet * (delta / 50f + 0.1f);
                rate = MathF.Max(1f, MathF.Min(GameConstants.TuningRatePlanet, rate));
                if (delta < GameConstants.ApproachingLockThreshold)
                {
                    if (!_approachingLockAnnounced)
                    {
                        GameEvents.RaisePlaySound(this, _audio.BeepWaveform, volume: _audio.BeepVolume);
                        _approachingLockAnnounced = true;
                    }
                    if (SimulationTime - _lastApproachingBeepTime > 1f)
                    {
                        GameEvents.RaisePlaySound(this, _audio.BeepWaveform, volume: _audio.BeepVolume);
                        _lastApproachingBeepTime = SimulationTime;
                    }
                }
                else if (delta > 15f)
                    _approachingLockAnnounced = false;
            }
        }

        // Manual flight or tuning input takes control back from a non-rift autopilot lock (rift locks
        // stay — you charge into a chamber with E). This is why locking a temple or pyramid no longer
        // "freezes" the drives: touch the controls and you are flying manually again.
        bool ctrlHeld = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool manualFlightInput = keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.S) ||
                                 keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.PageUp) || keys.IsKeyDown(Keys.PageDown);
        // Once the pilot has actually flown, the first-rest tuning nudge becomes eligible to fire.
        if (manualFlightInput && !LandedMode) _hasFlownThisSession = true;
        // Up/Down only count as "taking control" in full tuning mode, where they actually retune; in
        // manual flight the realms tune themselves, so those keys shouldn't drop your autopilot.
        bool manualTuneInput = TuningMode && (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down));
        if (LockedTarget != null && !LockedIsRift && !LandedMode && !ctrlHeld && (manualFlightInput || manualTuneInput))
        {
            DebugLogger.Log("Lock", $"RELEASED by manual input at dist {Vec5.Distance(Position, LockedTarget):F1}");
            LockedTarget = null;
            LockedBody = null;
            IsOrbiting = false;
            StopLockSound();
            SpeakNav("Manual control resumed.");
        }

        // Higher realms are hand-tunable in free flight (keys 4/5 select); full tuning mode (J) tunes any
        // selected realm; and on a planet Up/Down always tune the selected realm toward the crystal under
        // your cursor. Tuning is only gated off mid-flight while the ship is flying itself (an autopilot lock).
        bool allowTuning = TuningMode || LandedMode || (SelectedDim >= 3 && LockedTarget == null);

        // Self-fining: when tending a higher realm in flight, the knob sweeps quickly when far and eases to a
        // fine crawl near its target, so you can settle gently by ear. The target is the realm's cue target —
        // a nearby claimable temple/pyramid note when one is in range (so you can settle onto the objective to
        // claim it), otherwise the realm's own still centre.
        if (!LandedMode && !TuningMode && SelectedDim >= 3)
            rate = TuningDynamics.TuningRate(RDrive[SelectedDim] - CueTargetFreqs[SelectedDim]);

        if (allowTuning)
        {
            if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down))
                LastTuneTime = SimulationTime; // keep the by-ear cue present while actively tuning
            // Engaging higher-realm tuning yourself dismisses the first-rest teaching nudge.
            if (SelectedDim >= 3 && (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down)))
                _hasTunedHigherRealm = true;
            if (keys.IsKeyDown(Keys.Up))
                RDrive[SelectedDim] = MathF.Min(RDrive[SelectedDim] + rate * DT, GameConstants.FrequencyMax);
            if (keys.IsKeyDown(Keys.Down))
                RDrive[SelectedDim] = MathF.Max(RDrive[SelectedDim] - rate * DT, GameConstants.FrequencyMin);
        }
        else if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down))
        {
            // Up/Down pressed but tuning isn't active right now — guide the player to what works.
            if (LockedTarget != null)
                Speak("Your craft is tuning for you while it flies. Take manual control to tune by hand.");
            else
                Speak("Press 4 or 5 to choose a higher realm, then Up and Down to tune it. Press J for full manual tuning.");
        }

        #endregion

        #region View rotation & manual navigation

        // Rotation (disabled on planet)
        if (LandedMode)
        {
            RotatingLeft = false;
            RotatingRight = false;
            return;
        }

        RotatingLeft = keys.IsKeyDown(Keys.Left);
        RotatingRight = keys.IsKeyDown(Keys.Right);
        if (RotatingLeft) ViewRotation -= GameConstants.RotationSpeed * DT;
        if (RotatingRight) ViewRotation += GameConstants.RotationSpeed * DT;
        ViewRotation %= MathF.Tau;
        if (ViewRotation < 0) ViewRotation += MathF.Tau;

        if ((RotatingLeft || RotatingRight) && SimulationTime - _lastRotationSoundTime > GameConstants.RotationSoundDuration)
        {
            float pan = RotatingLeft ? -1f : 1f;
            GameEvents.RaisePlaySound(this, _audio.RotationWhooshWaveform, pan: pan, volume: _audio.EffectVolume);
            _lastRotationSoundTime = SimulationTime;
        }

        // Manual navigation. Skipped while an autopilot lock is active so it doesn't reset the drives
        // every frame and stall the autopilot's glide (manual input above releases the lock first).
        if (!TuningMode && LockedTarget == null)
        {
            float[] desiredVel = new float[3];
            float thrust = MaxVelocity * GameConstants.SpeedFactors[SpeedMode];
            if (keys.IsKeyDown(Keys.W)) desiredVel[1] += thrust;
            if (keys.IsKeyDown(Keys.S) && !ctrlPressed) desiredVel[1] -= thrust;
            if (keys.IsKeyDown(Keys.A) && !ctrlPressed) desiredVel[0] -= thrust;
            if (keys.IsKeyDown(Keys.D)) desiredVel[0] += thrust;
            if (keys.IsKeyDown(Keys.PageDown)) desiredVel[2] += thrust;
            if (keys.IsKeyDown(Keys.PageUp)) desiredVel[2] -= thrust;

            for (int i = 0; i < 3; i++)
            {
                if (desiredVel[i] != 0)
                {
                    float targetRes = MathF.Min(0.999f, MathF.Abs(desiredVel[i]) / MaxVelocity);
                    if (targetRes > 0)
                        RDrive[i] = ResonancePhysics.DriveForTargetResonance(
                            FTarget[i], targetRes, ResonanceWidth, MathF.Sign(desiredVel[i]));
                }
                else
                    RDrive[i] = FTarget[i];
            }

            // Realms 4 and 5 are not auto-tuned here — they are the player's to tend by ear (keys 4 and 5
            // plus Up/Down). Leaving their drive frequencies untouched lets that by-ear tuning take effect.
        }

        #endregion
    }

    #endregion

    #region Menu & volume input helpers

    /// <summary>
    /// Routes input to the currently open <see cref="MenuMode"/>: its exit key closes it, Up/Down
    /// move the highlight (wrapping), Enter selects, and the menu may claim extra keys of its own.
    /// </summary>
    private void HandleMenuInput(Func<Keys, bool> IsKeyPressed)
    {
        if (ActiveMenu == null) return;

        // Each menu closes on its own exit key.
        if (IsKeyPressed(ActiveMenu.ExitKey))
        {
            Speak(ActiveMenu.ExitMessage);
            ActiveMenu = null;
            return;
        }

        // Up/Down navigate (with wrap), Enter selects, plus any menu-specific keys
        // (e.g. the starmap's first-letter jump).
        if (IsKeyPressed(Keys.Up)) ActiveMenu.MoveUp();
        if (IsKeyPressed(Keys.Down)) ActiveMenu.MoveDown();
        if (IsKeyPressed(Keys.Enter))
        {
            ActiveMenu.Select();
            if (ActiveMenu == null) return;   // Select may close the menu (e.g. locking a destination)
        }
        ActiveMenu.HandleExtraKeys(IsKeyPressed);
    }

    /// <summary>
    /// Handles the +/- volume keys. The active modifier picks which mixer level moves: Alt = drive,
    /// Shift = beeps, Ctrl = effects, none = master. Each step is 1% and the new level is announced.
    /// </summary>
    private void HandleVolumeKeys(KeyboardState keys, KeyboardState prevKeys,
                                   bool shift, bool ctrl, bool alt,
                                   Func<Keys, bool> IsKeyPressed)
    {
        if (IsKeyPressed(Keys.OemPlus))
        {
            if (alt) { _audio.DriveVolume = MathF.Min(1f, _audio.DriveVolume + 0.01f); Speak($"Drive volume at {(int)(_audio.DriveVolume * 100)} percent."); }
            else if (shift) { _audio.BeepVolume = MathF.Min(1f, _audio.BeepVolume + 0.01f); Speak($"Beep volume at {(int)(_audio.BeepVolume * 100)} percent."); }
            else if (ctrl) { _audio.EffectVolume = MathF.Min(1f, _audio.EffectVolume + 0.01f); Speak($"Effect volume at {(int)(_audio.EffectVolume * 100)} percent."); }
            else { _audio.MasterVolume = MathF.Min(1f, _audio.MasterVolume + 0.01f); Speak($"Master volume at {(int)(_audio.MasterVolume * 100)} percent."); }
        }
        if (IsKeyPressed(Keys.OemMinus))
        {
            if (alt) { _audio.DriveVolume = MathF.Max(0f, _audio.DriveVolume - 0.01f); Speak($"Drive volume at {(int)(_audio.DriveVolume * 100)} percent."); }
            else if (shift) { _audio.BeepVolume = MathF.Max(0f, _audio.BeepVolume - 0.01f); Speak($"Beep volume at {(int)(_audio.BeepVolume * 100)} percent."); }
            else if (ctrl) { _audio.EffectVolume = MathF.Max(0f, _audio.EffectVolume - 0.01f); Speak($"Effect volume at {(int)(_audio.EffectVolume * 100)} percent."); }
            else { _audio.MasterVolume = MathF.Max(0f, _audio.MasterVolume - 0.01f); Speak($"Master volume at {(int)(_audio.MasterVolume * 100)} percent."); }
        }
    }

    #endregion
}
