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
    //  INPUT HANDLING
    // =========================================================================

    public void HandleInput(KeyboardState keys, KeyboardState prevKeys,
                            MouseState mouse, MouseState prevMouse,
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
            HandleMenuInput(keys, prevKeys, stars, planets, nebulae, IsKeyPressed);
            return;
        }

        bool shiftPressed = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        bool ctrlPressed = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool altPressed = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);

        // --- Number keys ---
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
                        Speak($"Preset {slot} overwritten. Frequencies: {FormatFreqs(RDrive)} hertz.");
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
                    Speak($"Preset {slot} saved. Frequencies: {FormatFreqs(RDrive)} hertz.");
                    _pendingPresetOverwrite = null;
                }
            }
            else if (shiftPressed)
            {
                // Recall preset
                if (FrequencyPresets.TryGetValue(slot, out var preset))
                {
                    Array.Copy(preset, RDrive, N);
                    Speak($"Preset {slot} recalled. Frequencies set to: {FormatFreqs(RDrive)} hertz.");
                }
                else
                    Speak($"Preset {slot} is empty. Use Control plus {slot} to save current frequencies.");
            }
            else
            {
                // Dimension selection (1-5 only)
                if (slot <= 5)
                {
                    string[] dimNames = { "x", "y", "z", "higher dimension one", "higher dimension two" };
                    SelectedDim = slot - 1;
                    Speak($"Tuning {dimNames[slot - 1]} dimension.");
                    _approachingLockAnnounced = false;
                }
            }
        }

        // --- Single press keys ---
        if (IsKeyPressed(Keys.J))
        {
            TuningMode = !TuningMode;
            string modeName = TuningMode ? "Resonance tuning mode" : "Manual mode";
            Speak($"Toggled to {modeName}.");
            DebugLogger.Log("Input", $"Tuning mode toggled: {modeName}");
        }

        if (IsKeyPressed(Keys.V))
        {
            VerboseMode = (VerboseMode + 1) % 3;
            string[] modes = { "Low", "Medium", "High" };
            Speak($"Verbosity mode: {modes[VerboseMode]}.");
        }

        if (IsKeyPressed(Keys.G) && SimulationTime - _lastTuaoiSwitch > GameConstants.TuaoiModeSwitchCooldown)
        {
            TuaoiModeIndex = (TuaoiModeIndex + 1) % GameConstants.TuaoiModeOrder.Length;
            TuaoiMode = GameConstants.TuaoiModeOrder[TuaoiModeIndex];
            var modeInfo = GameConstants.TuaoiModes[TuaoiMode];
            Speak($"Tuaoi Crystal: {Capitalize(TuaoiMode)} mode. {modeInfo.Desc}");
            DebugLogger.Log("Input", $"Tuaoi mode switched to: {TuaoiMode}");
            _lastTuaoiSwitch = SimulationTime;
        }

        if (IsKeyPressed(Keys.M))
        {
            StarmapMode = !StarmapMode;
            if (StarmapMode)
            {
                UpdateStarmapItems(stars, planets, nebulae);
                StarmapIndex = 0;
                SpeakStarmapItem();
            }
            else
                Speak("Exiting starmap.");
        }

        if (IsKeyPressed(Keys.C))
        {
            HighContrast = !HighContrast;
            Speak($"High contrast mode: {(HighContrast ? "on" : "off")}.");
        }

        if (IsKeyPressed(Keys.Q))
            Speak($"Target in selected dim: {FTarget[SelectedDim]:F2} Hz.");

        // Landing
        if (IsKeyPressed(Keys.L) && !LandedMode && !ctrlPressed)
        {
            float avgRes = Vec5.Mean(ResonanceLevels);
            float landingThreshold = GameConstants.LandingThreshold;
            if (NearestBody != null && NearestBody.Type == "planet")
                landingThreshold *= NearestBody.Difficulty;

            if (NearObject && avgRes > landingThreshold && NearestBody != null && NearestBody.Type == "planet")
            {
                LandingTimer = GameConstants.LandingTime;
                string eType = NearestBody.ExoplanetType ?? "super_earth";
                string eDesc = GameConstants.ExoplanetTypes[eType].Desc;
                Speak($"Initiating anchoring sequence on {eDesc}.");
            }
            else
            {
                ResonanceIntegrity -= 0.01f;
                if (!NearObject)
                    Speak("No celestial body nearby for anchoring. Minor integrity loss.");
                else if (avgRes <= landingThreshold)
                    Speak("Harmonic alignment too low for anchoring. Minor integrity loss.");
                else
                    Speak("Cannot anchor on this object. Minor integrity loss.");
            }
        }

        // Takeoff
        if (IsKeyPressed(Keys.T) && LandedMode)
        {
            LandedMode = false;
            LandedPlanet = null;
            LandedPlanetBody = null;
            StopBiomeSound();
            GameEvents.RaiseLandingEvent(this, false);
            Speak("Ascending from planet. Light vehicle disengaged.");
        }

        // Status
        if (IsKeyPressed(Keys.R))
        {
            string status = $"Position: {Vec5.Format(Position)}. Velocity: {Vec5.Format(Velocity)}. Resonance levels: {Vec5.Format(ResonanceLevels)}. View rotation: {ViewRotation:F2} radians. {(LandedMode ? "Landed on planet." : "In space.")} Integrity: {ResonanceIntegrity:F2}. Crystals: {CrystalsCollected}. Power levels: {Vec5.Format(ResonancePower)}.";
            Speak(status);
        }

        // HUD / Upgrade menu
        if (IsKeyPressed(Keys.U))
        {
            if (LandedMode && LockedCrystals.Count == CrystalCount)
            {
                UpgradeMode = true;
                HudIndex = 0;
                UpdateHudItems(upgrade: true);
                Speak($"Attunement menu. {CrystalsCollected} crystals available.");
                SpeakHudItem();
            }
            else
            {
                HudMode = true;
                HudIndex = 0;
                UpdateHudItems();
                SpeakHudItem();
            }
        }

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
                    Speak("Initiating rift charge sequence.");
                }
                else
                    Speak("Approach closer or increase resonance to charge.");
            }
            else
            {
                if (Rifts.Count > 0)
                {
                    RiftSelectionMode = true;
                    UpdateRiftItems();
                    RiftSelectionIndex = 0;
                    SpeakRiftItem();
                }
                else
                    Speak("No Harmonic Chambers detected.");
            }
        }

        // Speed mode toggle
        if (IsKeyPressed(Keys.Z) && !TuningMode)
        {
            SpeedMode = (SpeedMode + 1) % GameConstants.SpeedFactors.Length;
            Speak($"Speed mode toggled to {GameConstants.SpeedModeNames[SpeedMode]}.");
        }

        // Save/Load
        if (IsKeyPressed(Keys.S) && ctrlPressed) SaveGame();
        if (IsKeyPressed(Keys.L) && ctrlPressed) LoadGame();
        if (IsKeyPressed(Keys.A) && ctrlPressed)
        {
            AutosaveEnabled = !AutosaveEnabled;
            Speak($"Autosave {(AutosaveEnabled ? "enabled" : "disabled")}.");
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
                // Water blessing triggered (no-op in C# version, no wav generation)
                Speak("Water blessing activated.");
                _spacebarPressed = false;
                _spacebarHoldTimer = 0f;
            }
        }

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

        bool allowTuning = TuningMode || SelectedDim >= 3;
        if (allowTuning)
        {
            if (keys.IsKeyDown(Keys.Up))
                RDrive[SelectedDim] = MathF.Min(RDrive[SelectedDim] + rate * DT, GameConstants.FrequencyMax);
            if (keys.IsKeyDown(Keys.Down))
                RDrive[SelectedDim] = MathF.Max(RDrive[SelectedDim] - rate * DT, GameConstants.FrequencyMin);
        }
        else if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down))
        {
            Speak("Spatial dimension tuning locked in manual mode. Toggle with J for full access.");
        }

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

        // Manual navigation
        if (!TuningMode)
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
                    {
                        float dOverW = MathF.Sqrt(1f / targetRes - 1f);
                        float delta = ResonanceWidth * dOverW;
                        float deltaF = MathF.Sign(desiredVel[i]) * delta;
                        RDrive[i] = FTarget[i] + deltaF;
                    }
                }
                else
                    RDrive[i] = FTarget[i];
            }
        }

        // Zoom (mouse wheel)
        int scrollDelta = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            ZoomLevel += scrollDelta / 120f * GameConstants.ZoomStep;
            ZoomLevel = Math.Clamp(ZoomLevel, GameConstants.ZoomMin, GameConstants.ZoomMax);
        }

        // Zoom keys
        if (IsKeyPressed(Keys.OemCloseBrackets)) // ]
            ZoomLevel = MathF.Min(ZoomLevel + GameConstants.ZoomStep, GameConstants.ZoomMax);
        if (IsKeyPressed(Keys.OemOpenBrackets)) // [
            ZoomLevel = MathF.Max(ZoomLevel - GameConstants.ZoomStep, GameConstants.ZoomMin);
        if (IsKeyPressed(Keys.OemPipe) || IsKeyPressed(Keys.OemBackslash)) // backslash
            ZoomLevel = 1f;
    }

    private void HandleMenuInput(KeyboardState keys, KeyboardState prevKeys,
                                  List<CelestialBody> stars, List<CelestialBody> planets,
                                  List<CelestialBody> nebulae,
                                  Func<Keys, bool> IsKeyPressed)
    {
        string mode;
        if (RiftSelectionMode) mode = "rift";
        else if (StarmapMode) mode = "starmap";
        else if (UpgradeMode) mode = "upgrade";
        else mode = "hud";

        // Exit keys
        if (IsKeyPressed(Keys.M) && mode == "starmap") { StarmapMode = false; Speak("Exiting starmap."); }
        else if (IsKeyPressed(Keys.E) && mode == "rift") { RiftSelectionMode = false; Speak("Exiting rift selection."); }
        else if (IsKeyPressed(Keys.U) && (mode == "hud" || mode == "upgrade"))
        {
            HudMode = false; UpgradeMode = false; Speak("Exiting menu.");
        }

        // Navigation
        if (IsKeyPressed(Keys.Up))
        {
            if (mode is "starmap" or "rift")
            {
                var items = mode == "starmap" ? StarmapItems : null;
                int count = mode == "starmap" ? StarmapItems.Count : RiftItems.Count;
                if (count > 1)
                {
                    if (mode == "starmap") { StarmapIndex = (StarmapIndex - 1 + count) % count; SpeakStarmapItem(); }
                    else { RiftSelectionIndex = (RiftSelectionIndex - 1 + count) % count; SpeakRiftItem(); }
                }
            }
            else if (HudItems.Count > 1)
            {
                HudIndex = (HudIndex - 1 + HudItems.Count) % HudItems.Count;
                SpeakHudItem();
            }
        }

        if (IsKeyPressed(Keys.Down))
        {
            if (mode is "starmap" or "rift")
            {
                int count = mode == "starmap" ? StarmapItems.Count : RiftItems.Count;
                if (count > 1)
                {
                    if (mode == "starmap") { StarmapIndex = (StarmapIndex + 1) % count; SpeakStarmapItem(); }
                    else { RiftSelectionIndex = (RiftSelectionIndex + 1) % count; SpeakRiftItem(); }
                }
            }
            else if (HudItems.Count > 1)
            {
                HudIndex = (HudIndex + 1) % HudItems.Count;
                SpeakHudItem();
            }
        }

        // Select
        if (IsKeyPressed(Keys.Enter))
        {
            if (mode == "upgrade") ApplyUpgrade();
            else if (mode == "starmap") LockOnStarmapItem();
            else if (mode == "rift") LockOnRiftItem();
        }

        // First-letter jump in starmap
        if (mode == "starmap")
        {
            for (Keys k = Keys.A; k <= Keys.Z; k++)
            {
                if (!IsKeyPressed(k)) continue;
                char ch = (char)('a' + (k - Keys.A));
                for (int idx = 0; idx < StarmapItems.Count; idx++)
                {
                    if (StarmapItems[idx].Label.Length > 0 &&
                        char.ToLower(StarmapItems[idx].Label[0]) == ch)
                    {
                        StarmapIndex = idx;
                        SpeakStarmapItem();
                        break;
                    }
                }
                break;
            }
        }
    }

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
}
