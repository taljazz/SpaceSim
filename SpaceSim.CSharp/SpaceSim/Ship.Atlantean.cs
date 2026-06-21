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
    //  PORTAL ANCHOR SYSTEM
    // =========================================================================

    public void CreatePortalAnchor()
    {
        if (PortalAnchors.Count >= GameConstants.MaxPortalAnchors)
        {
            Speak($"Maximum {GameConstants.MaxPortalAnchors} portal anchors reached. Use an existing anchor first.");
            return;
        }
        if (CrystalsCollected < GameConstants.PortalAnchorCost)
        {
            Speak($"Insufficient crystals. Need {GameConstants.PortalAnchorCost} to create portal anchor.");
            return;
        }

        CrystalsCollected -= GameConstants.PortalAnchorCost;
        string name = $"Anchor {PortalAnchors.Count + 1}";
        PortalAnchors.Add(new PortalAnchor
        {
            Position = Vec5.Clone(Position),
            Name = name,
            CreatedTime = SimulationTime
        });
        Speak($"Portal anchor '{name}' created. {PortalAnchors.Count}/{GameConstants.MaxPortalAnchors} anchors set.");
    }

    public void UsePortalAnchor()
    {
        if (PortalAnchors.Count == 0)
        {
            Speak("No portal anchors set. Create one with P key.");
            return;
        }
        if (SimulationTime - _lastPortalUse < GameConstants.PortalCooldown)
        {
            int remaining = (int)(GameConstants.PortalCooldown - (SimulationTime - _lastPortalUse));
            Speak($"Portal cooldown active. {remaining} seconds remaining.");
            return;
        }
        if (Vec5.Mean(ResonanceLevels) < GameConstants.PortalTravelResonance)
        {
            Speak("Insufficient resonance for portal travel. Tune frequencies higher.");
            return;
        }

        var anchor = PortalAnchors[0];
        Array.Copy(anchor.Position, Position, N);
        _lastPortalUse = SimulationTime;
        Speak($"Portal activated. Teleported to {anchor.Name}.");
    }

    // =========================================================================
    //  ASTRAL PROJECTION
    // =========================================================================

    public void EnterAstralMode()
    {
        if (Vec5.Mean(ResonanceLevels) < GameConstants.AstralProjectionResonance)
        {
            Speak("Insufficient resonance for astral projection. Achieve 90% resonance in all realms.");
            return;
        }
        if (SimulationTime - _lastAstralReturn < GameConstants.AstralCooldown)
        {
            int remaining = (int)(GameConstants.AstralCooldown - (SimulationTime - _lastAstralReturn));
            Speak($"Astral cooldown active. {remaining} seconds remaining.");
            return;
        }

        AstralMode = true;
        AstralBodyPos = Vec5.Clone(Position);
        AstralTimer = GameConstants.AstralDuration;
        Speak("Astral projection initiated. Your consciousness expands beyond your light vehicle. Press B to return.");
    }

    public void ExitAstralMode()
    {
        if (!AstralMode) return;
        Array.Copy(AstralBodyPos!, Position, N);
        AstralMode = false;
        AstralBodyPos = null;
        _lastAstralReturn = SimulationTime;
        Speak("Returning to physical form. Astral projection complete.");
    }

    public void UpdateAstralMode(float dt)
    {
        if (!AstralMode) return;

        AstralTimer -= dt;
        if (AstralTimer <= 0)
        {
            Speak("Astral projection time limit reached. Returning to body.");
            ExitAstralMode();
            return;
        }

        float distFromBody = Vec5.Distance(Position, AstralBodyPos!);
        if (distFromBody > GameConstants.AstralProjectionRange)
        {
            if (!_astralTooFar)
            {
                Speak("Warning: Astral form too far from body. Connection weakening.");
                _astralTooFar = true;
            }
            float[] dir = Vec5.Subtract(AstralBodyPos!, Position);
            for (int i = 0; i < N; i++)
                Position[i] += dir[i] * 0.1f;
        }
        else
        {
            _astralTooFar = false;
        }

        Vec5.ScaleInPlace(Velocity, GameConstants.AstralSpeedMult);
    }

    // =========================================================================
    //  INTENTION NAVIGATION
    // =========================================================================

    public void StartIntentionNavigation()
    {
        if (Vec5.Mean(ResonanceLevels) < GameConstants.IntentionResonanceThreshold)
        {
            Speak("Insufficient resonance for intention navigation. Focus your mind and tune higher.");
            return;
        }
        IntentionActive = true;
        IntentionTimer = 0f;
        IntentionTarget = null;
        Speak("Intention navigation activated. Focus your intention on your destination...");
    }

    public void UpdateIntentionNavigation(float dt)
    {
        if (!IntentionActive) return;

        IntentionTimer += dt;

        if (IntentionTimer >= GameConstants.IntentionActivationTime)
        {
            if (LockedTarget != null)
            {
                float[] dir = Vec5.Subtract(LockedTarget, Position);
                float dist = Vec5.Norm(dir);
                float travelDist = MathF.Min(dist * GameConstants.IntentionPrecision, GameConstants.IntentionRange);
                if (dist > 0)
                {
                    for (int i = 0; i < N; i++)
                        Position[i] += (dir[i] / dist) * travelDist;
                }
                Speak($"Intention manifested. Traveled {travelDist:F1} units toward target.");
            }
            else
            {
                Speak("No target locked. Intention dissipates without focus.");
            }
            IntentionActive = false;
            IntentionTimer = 0f;
        }
    }

    // =========================================================================
    //  TEMPLE / LEY LINE / PYRAMID PROXIMITY
    // =========================================================================

    public void CheckTempleProximity(List<Temple> temples)
    {
        if (SimulationTime - _lastTempleCheck < 1f) return;

        NearTemple = null;
        float scanRange = GetEffectiveScanRange();

        foreach (var temple in temples)
        {
            float dist = temple.DistanceTo(Position);
            if (dist < scanRange)
            {
                NearTemple = temple;
                int keyIdx = temple.KeyIndex;

                if (keyIdx >= 0 && !TempleKeys.Contains(keyIdx))
                {
                    // Check resonance at temple frequency
                    float resAtFreq = 0;
                    for (int i = 0; i < N; i++)
                    {
                        float delta = MathF.Abs(RDrive[i] - temple.Frequency);
                        resAtFreq += ResonancePhysics.Resonance(delta, ResonanceWidth);
                    }
                    resAtFreq /= N;

                    if (resAtFreq > 0.7f)
                    {
                        TempleKeys.Add(keyIdx);
                        _templeNearbyAnnounced = false;
                        GameEvents.RaiseTempleKeyCollected(this, temple.KeyName, keyIdx, TempleKeys.Count);
                        if (TempleKeys.Count == GameConstants.MinorTempleCount)
                            Speak($"{temple.KeyName} key acquired! All twelve temple keys collected. The Halls of Amenti now await your arrival.");
                        else
                            Speak($"Temple of {temple.KeyName} visited. {temple.KeyName} key acquired! {TempleKeys.Count}/{GameConstants.MinorTempleCount} keys collected.");
                    }
                    else if (!_templeNearbyAnnounced)
                    {
                        Speak($"Temple of {temple.KeyName} nearby. Tune to {temple.Frequency:F1} Hz to receive the key.");
                        _templeNearbyAnnounced = true;
                    }
                }
                else if (keyIdx == -1) // Halls of Amenti
                {
                    if (TempleKeys.Count >= GameConstants.MasterTempleUnlockKeys &&
                        (ConsciousnessStage == ConsciousnessLevel.Enlightened || ConsciousnessStage == ConsciousnessLevel.Ascended))
                    {
                        if (!VisitedAmenti)
                        {
                            Speak("The Halls of Amenti open before you. Ancient wisdom floods your consciousness.");
                            VisitedAmenti = true;
                            AmentiBlessingActive = true;
                            ResonanceWidth *= PHI;
                            ConsciousnessValue = 1f;
                            ConsciousnessStage = ConsciousnessLevel.Ascended;
                        }
                    }
                    else if (!_amentiSealedAnnounced)
                    {
                        int missing = GameConstants.MinorTempleCount - TempleKeys.Count;
                        Speak($"The Halls of Amenti remain sealed. {missing} more temple keys needed, or consciousness level insufficient.");
                        _amentiSealedAnnounced = true;
                    }
                }
                break;
            }
        }

        // Reset flags when not near any temple
        if (NearTemple == null)
        {
            _templeNearbyAnnounced = false;
            _amentiSealedAnnounced = false;
        }

        _lastTempleCheck = SimulationTime;
    }

    public void CheckLeyLineProximity(List<LeyLine> leyLines)
    {
        bool wasOnLeyLine = OnLeyLine;
        OnLeyLine = false;
        CurrentLeyLine = null;

        foreach (var line in leyLines)
        {
            // Allocation-free point-to-segment distance — runs every frame for every ley line.
            var (distToLine, _) = Vec5.DistanceToSegment(Position, line.Start, line.End);

            if (distToLine < GameConstants.LeyLineWidth)
            {
                OnLeyLine = true;
                CurrentLeyLine = line;
                break;
            }
        }

        if (OnLeyLine && !wasOnLeyLine && !_leyLineAnnounced)
        {
            Speak($"Entering {CurrentLeyLine!.Name}. Speed enhanced.");
            _leyLineAnnounced = true;
        }
        else if (!OnLeyLine && wasOnLeyLine)
        {
            Speak("Leaving ley line. Normal speed resumed.");
            _leyLineAnnounced = false;
        }
    }

    public void CheckPyramidProximity(List<Pyramid> pyramids)
    {
        bool wasNear = NearPyramid != null;
        NearPyramid = null;

        float scanRange = GetEffectiveScanRange();
        foreach (var pyr in pyramids)
        {
            float dist = pyr.DistanceTo(Position);
            if (dist < scanRange)
            {
                NearPyramid = pyr;
                break;
            }
        }

        if (NearPyramid != null && !wasNear && !_pyramidAnnounced)
        {
            Speak($"Entering {NearPyramid.Name}. Resonance chamber at 118 Hz activated.");
            _pyramidAnnounced = true;
        }
        else if (NearPyramid == null && wasNear)
        {
            Speak("Leaving pyramid resonance chamber.");
            _pyramidAnnounced = false;
        }
    }

    // =========================================================================
    //  CONSCIOUSNESS & BRAINWAVE
    // =========================================================================

    public void UpdateConsciousness(float dt)
    {
        float avgRes = Vec5.Mean(ResonanceLevels);

        if (avgRes > 0.8f)
            ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + GameConstants.ConsciousnessGainRate * dt);
        else if (avgRes < 0.3f)
            ConsciousnessValue = MathF.Max(0f, ConsciousnessValue - GameConstants.ConsciousnessDecayRate * dt);

        // Pyramid boost
        if (NearPyramid != null)
        {
            bool freqMatch = false;
            for (int i = 0; i < N; i++)
            {
                if (RDrive[i] >= GameConstants.PyramidResonanceRange.Min &&
                    RDrive[i] <= GameConstants.PyramidResonanceRange.Max)
                {
                    freqMatch = true;
                    break;
                }
            }
            if (freqMatch)
                ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + GameConstants.ConsciousnessGainRate * GameConstants.PyramidConsciousnessBoost * dt);
        }

        var oldStage = ConsciousnessStage;
        foreach (var (levelName, levelInfo) in GameConstants.ConsciousnessLevels)
        {
            if (ConsciousnessValue >= levelInfo.Threshold)
                ConsciousnessStage = levelName;
        }

        if (ConsciousnessStage != oldStage && !_consciousnessAnnounced)
        {
            var info = GameConstants.ConsciousnessLevels[ConsciousnessStage];
            Speak($"Consciousness level: {ConsciousnessStage}. {info.Desc}.");
            DebugLogger.Log("Ship", $"Consciousness changed: {oldStage} -> {ConsciousnessStage} (value={ConsciousnessValue:F3})");
            GameEvents.RaiseConsciousnessChanged(this, oldStage.ToString(), ConsciousnessStage.ToString(), ConsciousnessValue);
            _consciousnessAnnounced = true;
        }
        else if (ConsciousnessStage == oldStage)
        {
            _consciousnessAnnounced = false;
        }
    }

    public void DetectBrainwaveState()
    {
        foreach (var (stateName, stateInfo) in GameConstants.BrainwaveStates)
        {
            float fMin = stateInfo.FreqMin;
            float fMax = stateInfo.FreqMax;

            for (int i = 0; i < N; i++)
            {
                float driveFreq = RDrive[i];
                if ((driveFreq / 50f >= fMin && driveFreq / 50f <= fMax) ||
                    (driveFreq >= fMin * 50f && driveFreq <= fMax * 50f))
                {
                    if (CurrentBrainwave != stateName)
                    {
                        CurrentBrainwave = stateName;
                        Speak($"Brainwave state: {stateName}. {FormatName(stateInfo.State)} mode.");
                        if (stateInfo.Effect == BrainwaveEffect.AutoRepair)
                            ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + 0.05f);
                        else if (stateInfo.Effect == BrainwaveEffect.RiftVision)
                            Speak("Enhanced rift perception activated.");
                    }
                    return;
                }
            }
        }
        if (CurrentBrainwave != BrainwaveState.Beta)
            CurrentBrainwave = BrainwaveState.Beta;
    }

    // =========================================================================
    //  HARMONIC DETECTION
    // =========================================================================

    public Dictionary<string, HarmonicInfo> DetectHarmonicRelationships()
    {
        var detected = new Dictionary<string, HarmonicInfo>();

        for (int i = 0; i < N; i++)
        {
            for (int j = i + 1; j < N; j++)
            {
                if (!HarmonicMath.TryMatchRatio(RDrive[i], RDrive[j], out var hType)) continue;

                float ratio = MathF.Max(RDrive[i], RDrive[j]) / MathF.Min(RDrive[i], RDrive[j]);
                string key = $"{hType}_d{i + 1}_d{j + 1}";
                detected[key] = new HarmonicInfo { HType = hType, Dims = new[] { i, j }, Ratio = ratio };
            }
        }

        return detected;
    }

    public void ApplyHarmonicBonuses(Dictionary<string, HarmonicInfo> harmonics)
    {
        if (harmonics.Count == 0) return;

        var newHarmonics = new List<(HarmonicType HType, int[] Dims)>();

        foreach (var (key, info) in harmonics)
        {
            if (!ActiveHarmonics.ContainsKey(key))
                newHarmonics.Add((info.HType, info.Dims));
            ActiveHarmonics[key] = (info.HType, info.Dims, SimulationTime + GameConstants.HarmonicBonusDuration);
        }

        // Announce & play chimes
        foreach (var (hType, dims) in newHarmonics)
        {
            string dimNames = string.Join(" and ", dims.Select(d => $"dimension {d + 1}"));
            // Space the PascalCase interval name so the screen reader says "Perfect Fifth", not "PerfectFifth".
            Speak($"{GameUtils.SpacePascalCase(hType.ToString())} harmonic detected between {dimNames}.");

            float[]? chime = hType switch
            {
                HarmonicType.Octave => _audio.OctaveChime,
                HarmonicType.PerfectFifth => _audio.FifthChime,
                HarmonicType.Golden => _audio.GoldenChime,
                HarmonicType.PerfectFourth => _audio.FourthChime,
                HarmonicType.MajorThird => _audio.MajorThirdChime,
                HarmonicType.MinorThird => _audio.MinorThirdChime,
                HarmonicType.MajorSixth => _audio.MajorSixthChime,
                HarmonicType.MinorSixth => _audio.MinorSixthChime,
                HarmonicType.Tritone => _audio.TritoneChime,
                _ => null
            };
            if (chime != null)
                GameEvents.RaisePlaySound(this, chime, volume: _audio.EffectVolume);
            GameEvents.RaiseHarmonicDetected(this, hType.ToString(), dims);
        }

        // Apply bonuses & expire old
        var toRemove = new List<string>();
        foreach (var (key, (hType, dims, expiry)) in ActiveHarmonics)
        {
            if (SimulationTime > expiry) { toRemove.Add(key); continue; }

            switch (hType)
            {
                case HarmonicType.Octave:
                    foreach (int d in dims)
                        Velocity[d] *= 1.1f;
                    break;
                case HarmonicType.PerfectFifth:
                    DissonanceTimer = MathF.Max(0, DissonanceTimer - 0.1f);
                    break;
                case HarmonicType.PerfectFourth:
                    ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + 0.001f);
                    break;
                case HarmonicType.MajorSixth:
                    foreach (int d in dims)
                        ResonancePower[d] += 0.05f;
                    break;
                case HarmonicType.Tritone:
                    foreach (int d in dims)
                        Velocity[d] += MathHelpers.RandomRange(-0.2f, 0.2f);
                    break;
            }
        }
        foreach (string k in toRemove) ActiveHarmonics.Remove(k);
    }

    // =========================================================================
    //  ASCENSION
    // =========================================================================

    public void Ascend()
    {
        DebugLogger.Log("Ship", $"ASCENSION triggered with {CrystalsCollected} crystals");
        Speak("Ascension achieved! Warping to harmonious new universe.");
        Array.Clear(Position);
        ActivateGoldenHarmony();
        NeedsUniverseRegeneration = true;
        GameEvents.RaiseUniverseRegenNeeded(this);
        Rifts.Clear();
        GameEvents.RaiseClearAllSounds(this);
        GameEvents.RaiseAscension(this);
    }

    // =========================================================================
    //  RIFT ENTRY
    // =========================================================================

    public void EnterRift(Rift rift)
    {
        DebugLogger.Log("Ship", $"Entering rift: type={rift.RiftKind}, pos={Vec5.Format(rift.Position)}");
        for (int i = 0; i < N; i++)
            Position[i] += MathHelpers.RandomRange(-20f, 20f) * PHI;

        Speak($"Entering {rift.RiftKind} rift-golden warp activated.");

        if (rift.RiftKind == RiftType.Crystal) CrystalsCollected += 1;
        else if (rift.RiftKind == RiftType.Hazard) ResonanceIntegrity -= 0.1f;
        else if (rift.RiftKind == RiftType.PerfectFifth)
        {
            CrystalBonus += 1;
            Speak("Perfect fifth rift grants eternal crystal bounty.");
        }

        // Stop rift sound
        if (rift.Sound != null) { rift.Sound.Loop = false; rift.Sound.Volume = 0; }

        Rifts.Remove(rift);
        LockedRift = null;
        LockedTarget = null;
        LockedIsRift = false;
        _approachedRiftAnnounced = false;
        StopLockSound();
    }

    private void StopLockSound()
    {
        if (LockSound != null)
        {
            LockSound.Loop = false;
            LockSound.Volume = 0;
            LockSound = null;
        }
    }
}
