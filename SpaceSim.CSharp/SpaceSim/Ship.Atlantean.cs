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
    #region Portal Anchor System

    /// <summary>
    /// Drops a portal anchor at the ship's current position so it can be teleported back to later.
    /// Costs <see cref="GameConstants.PortalAnchorCost"/> crystals and is capped at
    /// <see cref="GameConstants.MaxPortalAnchors"/>; both limits are announced if hit.
    /// </summary>
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

    /// <summary>
    /// Teleports the ship to its first portal anchor. Gated by a cooldown
    /// (<see cref="GameConstants.PortalCooldown"/>) and a minimum average resonance
    /// (<see cref="GameConstants.PortalTravelResonance"/>); the reason is spoken if travel is denied.
    /// </summary>
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

    #endregion

    #region Astral Projection

    /// <summary>
    /// Leaves the body to fly free as a disembodied form. Requires high resonance
    /// (<see cref="GameConstants.AstralProjectionResonance"/>) and respects a post-return cooldown.
    /// Stores the body's position so <see cref="ExitAstralMode"/> can snap back to it.
    /// </summary>
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

    /// <summary>
    /// Returns the consciousness to the physical body, restoring the saved position and starting the
    /// astral cooldown. Safe to call when not projecting (it simply does nothing).
    /// </summary>
    public void ExitAstralMode()
    {
        if (!AstralMode) return;
        Array.Copy(AstralBodyPos!, Position, N);
        AstralMode = false;
        AstralBodyPos = null;
        _lastAstralReturn = SimulationTime;
        Speak("Returning to physical form. Astral projection complete.");
    }

    /// <summary>
    /// Per-frame astral tick: counts down the duration (auto-returning at zero), tugs the form back
    /// toward the body if it strays past <see cref="GameConstants.AstralProjectionRange"/>, and
    /// applies the astral speed multiplier to the current velocity.
    /// </summary>
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

    #endregion

    #region Intention Navigation

    /// <summary>
    /// Begins focusing intention on the locked target. Requires a minimum resonance
    /// (<see cref="GameConstants.IntentionResonanceThreshold"/>); once started, the manifestation
    /// resolves after a short focus delay in <see cref="UpdateIntentionNavigation"/>.
    /// </summary>
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

    /// <summary>
    /// Per-frame intention tick: once the focus time elapses, leaps a fraction of the way toward the
    /// locked target (capped by <see cref="GameConstants.IntentionRange"/>), then clears the state.
    /// With no target locked, the intention simply dissipates.
    /// </summary>
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

    #endregion

    #region Temple / Ley Line / Pyramid Proximity

    /// <summary>
    /// Throttled (once per second) scan for a nearby temple. Tuning to a minor temple's frequency at
    /// high enough resonance collects its key; reaching the Halls of Amenti with all keys and an
    /// enlightened/ascended mind grants the ascension blessing. Sets <see cref="NearTemple"/>.
    /// </summary>
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
                    // The key is claimed by resonating ANY ONE realm with the temple's frequency — this
                    // matches the spoken "tune a realm to N Hz" instruction and the single-realm test used
                    // for pyramids and Solfeggio. (Requiring all five would be impossible in the default
                    // manual mode, where the three spatial realms are pinned to the local target.) The
                    // tolerance widens with consciousness, mirroring the main flight-resonance loop.
                    float effectiveWidth = ResonanceWidth * GameConstants.ConsciousnessLevels[ConsciousnessStage].Mult;
                    float resAtFreq = 0f;
                    for (int i = 0; i < N; i++)
                    {
                        float delta = MathF.Abs(RDrive[i] - temple.Frequency);
                        resAtFreq = MathF.Max(resAtFreq, ResonancePhysics.Resonance(delta, effectiveWidth));
                    }

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
                        Speak($"Temple of {temple.KeyName} nearby. Tune a realm to {temple.Frequency:F0} hertz to receive the key.");
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
                            Speak("The Halls of Amenti open before you. Ancient wisdom floods your consciousness. Your light vehicle is forever blessed: swifter flight and ascended awareness.");
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

    /// <summary>
    /// Tests whether the ship is riding any ley line this frame (point-to-segment distance within
    /// <see cref="GameConstants.LeyLineWidth"/>) and announces entering/leaving. Sets
    /// <see cref="OnLeyLine"/> / <see cref="CurrentLeyLine"/> for the speed boost applied elsewhere.
    /// </summary>
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

    /// <summary>
    /// Scans for a nearby pyramid (within the effective scan range) and announces entering/leaving its
    /// resonance chamber. Sets <see cref="NearPyramid"/>, which feeds the consciousness boost.
    /// </summary>
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

    #endregion

    #region Consciousness & Brainwave

    /// <summary>
    /// Per-frame consciousness drift: rises while average resonance is high, falls while it is low,
    /// and gains extra inside a tuned pyramid chamber. Promotes/demotes the
    /// <see cref="ConsciousnessStage"/> as thresholds are crossed and announces each change once.
    /// </summary>
    public void UpdateConsciousness(float dt)
    {
        float avgRes = Vec5.Mean(ResonanceLevels);

        // Meditative brainwave states accelerate consciousness growth (each band's multiplier sets how fast).
        float brainwaveMult = GameConstants.BrainwaveStates[CurrentBrainwave].Mult;
        if (avgRes > 0.8f)
            ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + GameConstants.ConsciousnessGainRate * brainwaveMult * dt);
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

    /// <summary>
    /// Maps the current drive frequencies onto a brainwave band (Delta/Theta/Alpha/Beta/Gamma) and,
    /// on entering a new band, announces it and applies its effect (e.g. auto-repair). Falls back to
    /// Beta when no band matches.
    /// </summary>
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
                            Speak("Enhanced Harmonic Chamber perception activated.");
                    }
                    return;
                }
            }
        }
        if (CurrentBrainwave != BrainwaveState.Beta)
            CurrentBrainwave = BrainwaveState.Beta;
    }

    #endregion

    #region Harmonic Detection

    // Reused buffers so the twice-per-second harmonic check allocates no fresh collections.
    private readonly Dictionary<string, HarmonicInfo> _detectedHarmonics = new();
    private readonly List<(HarmonicType HType, int[] Dims)> _newHarmonics = new();
    private readonly List<string> _harmonicsToRemove = new();

    /// <summary>
    /// Examines every pair of drive frequencies and reports the musical interval (octave, fifth,
    /// golden ratio, etc.) each pair currently forms, keyed by interval + dimension pair.
    /// </summary>
    /// <returns>A map of detected harmonics for this frame, consumed by <see cref="ApplyHarmonicBonuses"/>.</returns>
    public Dictionary<string, HarmonicInfo> DetectHarmonicRelationships()
    {
        _detectedHarmonics.Clear();

        for (int i = 0; i < N; i++)
        {
            for (int j = i + 1; j < N; j++)
            {
                if (!HarmonicMath.TryMatchRatio(RDrive[i], RDrive[j], out var hType)) continue;

                float ratio = MathF.Max(RDrive[i], RDrive[j]) / MathF.Min(RDrive[i], RDrive[j]);
                string key = $"{hType}_d{i + 1}_d{j + 1}";
                _detectedHarmonics[key] = new HarmonicInfo { HType = hType, Dims = new[] { i, j }, Ratio = ratio };
            }
        }

        return _detectedHarmonics;
    }

    /// <summary>
    /// Refreshes the set of active harmonics from this frame's detections, announcing and chiming any
    /// newly formed interval, then applies each still-active harmonic's ongoing gameplay bonus and
    /// expires the ones whose duration has elapsed.
    /// </summary>
    /// <param name="harmonics">The harmonics detected this frame (from <see cref="DetectHarmonicRelationships"/>).</param>
    public void ApplyHarmonicBonuses(Dictionary<string, HarmonicInfo> harmonics)
    {
        if (harmonics.Count == 0) return;

        _newHarmonics.Clear();

        foreach (var (key, info) in harmonics)
        {
            if (!ActiveHarmonics.ContainsKey(key))
                _newHarmonics.Add((info.HType, info.Dims));
            ActiveHarmonics[key] = (info.HType, info.Dims, SimulationTime + GameConstants.HarmonicBonusDuration);
        }

        // Announce & play chimes
        foreach (var (hType, dims) in _newHarmonics)
        {
            string dimNames = string.Join(" and ", dims.Select(d => $"Realm {d + 1}"));
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
        _harmonicsToRemove.Clear();
        foreach (var (key, (hType, dims, expiry)) in ActiveHarmonics)
        {
            if (SimulationTime > expiry) { _harmonicsToRemove.Add(key); continue; }

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
        foreach (string k in _harmonicsToRemove) ActiveHarmonics.Remove(k);
    }

    #endregion

    #region Ascension

    /// <summary>
    /// The crystal-renewal transition (a replayable rebirth, distinct from the one-time Halls of Amenti
    /// climax): resets position to the origin, switches on Golden Harmony, flags the universe for
    /// regeneration, clears rifts and sounds, and broadcasts the event so the player can keep growing
    /// toward the true ascension at the master temple.
    /// </summary>
    public void Ascend()
    {
        DebugLogger.Log("Ship", $"RENEWAL triggered with {CrystalsCollected} crystals");
        Speak("Crystal threshold reached. Your light vehicle renews, warping to a fresh universe to continue the journey toward the Halls of Amenti.");
        Array.Clear(Position);
        ActivateGoldenHarmony();
        NeedsUniverseRegeneration = true;
        GameEvents.RaiseUniverseRegenNeeded(this);
        SilenceAllWorldSounds();              // stop OpenAL world voices (ambients, rift hums, lock)
        Rifts.Clear();
        GameEvents.RaiseClearAllSounds(this); // and drain the NAudio mix (fallback loops, biome, one-shots)
        GameEvents.RaiseAscension(this);
    }

    #endregion

    #region Rift Entry

    /// <summary>
    /// Warps the ship through a rift: jitters the position by a golden-ratio-scaled offset, applies
    /// the rift kind's payoff (crystal gain, integrity damage, or a perfect-fifth crystal bonus),
    /// then silences and removes the rift and clears any lock targeting it.
    /// </summary>
    public void EnterRift(Rift rift)
    {
        DebugLogger.Log("Ship", $"Entering rift: type={rift.RiftKind}, pos={Vec5.Format(rift.Position)}");
        for (int i = 0; i < N; i++)
            Position[i] += MathHelpers.RandomRange(-20f, 20f) * PHI;

        // Build the warp announcement as one utterance (interrupt-by-default would drop the first line).
        string warpMsg = $"Entering {rift.RiftKind} Harmonic Chamber. Golden warp activated.";
        if (rift.RiftKind == RiftType.Crystal) CrystalsCollected += 1;
        else if (rift.RiftKind == RiftType.Hazard) ApplyIntegrityDamage(0.1f);
        else if (rift.RiftKind == RiftType.PerfectFifth)
        {
            CrystalBonus += 1;
            warpMsg += " It grants eternal crystal bounty.";
        }
        Speak(warpMsg);

        // Stop rift sound
        StopWorldLoop(ref rift.Sound);

        Rifts.Remove(rift);
        LockedRift = null;
        LockedTarget = null;
        LockedIsRift = false;
        _approachedRiftAnnounced = false;
        StopLockSound();
    }

    /// <summary>Silences and releases the active target-lock beacon, if one is playing.</summary>
    private void StopLockSound() => StopWorldLoop(ref LockSound);

    #endregion
}
