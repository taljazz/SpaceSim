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
            SpeakAtlantean($"Maximum {GameConstants.MaxPortalAnchors} portal anchors reached. Use an existing anchor first.");
            return;
        }
        if (CrystalsCollected < GameConstants.PortalAnchorCost)
        {
            SpeakAtlantean($"Insufficient crystals. Need {GameConstants.PortalAnchorCost} to create portal anchor.");
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
        SpeakAtlantean($"Portal anchor '{name}' created. {PortalAnchors.Count}/{GameConstants.MaxPortalAnchors} anchors set.");
    }

    /// <summary>
    /// Teleports the ship to its first portal anchor. Gated by a cooldown
    /// (<see cref="GameConstants.PortalCooldown"/>) and a minimum average resonance
    /// (<see cref="GameConstants.PortalTravelResonance"/>); the reason is spoken if travel is denied.
    /// </summary>
    public void OpenPortalMenu()
    {
        if (PortalAnchors.Count == 0)
        {
            SpeakAtlantean("No portal anchors set. Create one with the P key.");
            return;
        }
        // Open a pick-list so EVERY anchor is reachable. (Previously this teleported only ever to the first
        // anchor, stranding the rest.) The menu's Enter calls TeleportToSelectedAnchor.
        OpenMenu(new PortalMenuMode(this));
    }

    /// <summary>
    /// Teleport to a specific anchor, gated by the portal cooldown (<see cref="GameConstants.PortalCooldown"/>)
    /// and a minimum average resonance (<see cref="GameConstants.PortalTravelResonance"/>). Speaks the reason
    /// and returns false if travel is denied; regenerates the universe around the arrival point on success.
    /// </summary>
    public bool TeleportToAnchor(PortalAnchor anchor)
    {
        if (SimulationTime - _lastPortalUse < GameConstants.PortalCooldown)
        {
            int remaining = (int)(GameConstants.PortalCooldown - (SimulationTime - _lastPortalUse));
            SpeakAtlantean($"Portal cooldown active. {remaining} seconds remaining.");
            return false;
        }
        if (Vec5.Mean(ResonanceLevels) < GameConstants.PortalTravelResonance)
        {
            SpeakAtlantean("Insufficient resonance for portal travel. Tune frequencies higher.");
            return false;
        }

        Array.Copy(anchor.Position, Position, N);
        _lastPortalUse = SimulationTime;
        NeedsUniverseRegeneration = true;   // a teleport is a large jump — rebuild the world around the arrival
        SpeakAtlantean($"Portal activated. Teleported to {anchor.Name}.");
        return true;
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
            SpeakAtlantean("Insufficient resonance for astral projection. Achieve 90% resonance in all realms.");
            return;
        }
        if (SimulationTime - _lastAstralReturn < GameConstants.AstralCooldown)
        {
            int remaining = (int)(GameConstants.AstralCooldown - (SimulationTime - _lastAstralReturn));
            SpeakAtlantean($"Astral cooldown active. {remaining} seconds remaining.");
            return;
        }

        AstralMode = true;
        AstralBodyPos = Vec5.Clone(Position);
        AstralTimer = GameConstants.AstralDuration;
        SpeakAtlantean("Astral projection initiated. Your consciousness expands beyond your light vehicle. Press B to return.");
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
        SpeakAtlantean("Returning to physical form. Astral projection complete.");
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
            SpeakAtlantean("Astral projection time limit reached. Returning to body.");
            ExitAstralMode();
            return;
        }

        float distFromBody = Vec5.Distance(Position, AstralBodyPos!);
        if (distFromBody > GameConstants.AstralProjectionRange)
        {
            if (!_astralTooFar)
            {
                SpeakAtlantean("Warning: Astral form too far from body. Connection weakening.");
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
            SpeakAtlantean("Insufficient resonance for intention navigation. Focus your mind and tune higher.");
            return;
        }
        IntentionActive = true;
        IntentionTimer = 0f;
        IntentionTarget = null;
        SpeakAtlantean("Intention navigation activated. Focus your intention on your destination...");
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
                SpeakAtlantean($"Intention manifested. Traveled {travelDist:F1} units toward target.");
            }
            else
            {
                SpeakAtlantean("No target locked. Intention dissipates without focus.");
            }
            IntentionActive = false;
            IntentionTimer = 0f;
        }
    }

    #endregion

    #region Temple / Ley Line / Pyramid Proximity

    /// <summary>
    /// Per-frame scan for the nearby temple. Picks the NEAREST CLAIMABLE temple in range (so a closer
    /// already-keyed temple or the central Halls of Amenti can't mask the one the player flew to). Holding any
    /// realm on a minor temple's note for <see cref="GameConstants.TempleClaimDwell"/> collects its key;
    /// reaching the Halls of Amenti with all keys and an enlightened/ascended mind grants the ascension
    /// blessing. The claim is checked every frame (not throttled) so a brief by-ear settle is never dropped.
    /// Sets <see cref="NearTemple"/>.
    /// </summary>
    public void CheckTempleProximity(List<Temple> temples, float dt)
    {
        float scanRange = GetEffectiveScanRange();
        Temple? nearestClaimable = null; float dClaim = float.MaxValue;
        Temple? nearestAny = null; float dAny = float.MaxValue;
        foreach (var temple in temples)
        {
            float dist = temple.DistanceTo(Position);
            if (dist >= scanRange) continue;
            if (dist < dAny) { dAny = dist; nearestAny = temple; }
            bool claimable = temple.KeyIndex >= 0 && !TempleKeys.Contains(temple.KeyIndex);
            if (claimable && dist < dClaim) { dClaim = dist; nearestClaimable = temple; }
        }
        NearTemple = nearestClaimable ?? nearestAny;

        if (NearTemple == null)
        {
            _templeNearbyAnnounced = false;
            _amentiSealedAnnounced = false;
            _templeDwell = 0f;
            return;
        }

        Temple t = NearTemple;
        int keyIdx = t.KeyIndex;

        if (keyIdx >= 0 && !TempleKeys.Contains(keyIdx))
        {
            // The key is claimed by resonating ANY ONE realm with the temple's frequency — this matches the
            // spoken "tune a realm to N Hz" instruction and the single-realm test used for pyramids/Solfeggio.
            // (Requiring all five would be impossible in default mode, where the three spatial realms are
            // pinned to the local target.) Tolerance widens with consciousness, mirroring the flight loop.
            float effectiveWidth = ResonanceWidth * GameConstants.ConsciousnessLevels[ConsciousnessStage].Mult;
            float resAtFreq = 0f;
            for (int i = 0; i < N; i++)
                resAtFreq = MathF.Max(resAtFreq, ResonancePhysics.Resonance(MathF.Abs(RDrive[i] - t.Frequency), effectiveWidth));

            if (resAtFreq > GameConstants.TempleKeyClaimResonance)
            {
                // Require a short continuous dwell on the note so a deliberate settle claims reliably while a
                // momentary fly-by (or autopilot sweeping past) does not.
                _templeDwell += dt;
                if (_templeDwell >= GameConstants.TempleClaimDwell)
                {
                    TempleKeys.Add(keyIdx);
                    _templeDwell = 0f;
                    _templeNearbyAnnounced = false;
                    GameEvents.RaiseTempleKeyCollected(this, t.KeyName, keyIdx, TempleKeys.Count);
                    if (TempleKeys.Count == GameConstants.MinorTempleCount)
                        SpeakAtlantean($"{t.KeyName} key acquired! All twelve temple keys collected. The Halls of Amenti now await your arrival.");
                    else
                        SpeakAtlantean($"Temple of {t.KeyName} visited. {t.KeyName} key acquired! {TempleKeys.Count} of {GameConstants.MinorTempleCount} keys collected.");
                }
            }
            else
            {
                _templeDwell = 0f;
                if (!_templeNearbyAnnounced)
                {
                    SpeakAtlantean(ByEarMode
                        ? $"Temple of {t.KeyName} nearby. Tune a higher realm toward its note by ear, until the beat steadies, to receive the key."
                        : $"Temple of {t.KeyName} nearby. Tune a higher realm to its note, {t.Frequency:F0}, to receive the key.");
                    _templeNearbyAnnounced = true;
                }
            }
        }
        else if (keyIdx == -1) // Halls of Amenti
        {
            if (TempleKeys.Count >= GameConstants.MasterTempleUnlockKeys &&
                (ConsciousnessStage == ConsciousnessLevel.Enlightened || ConsciousnessStage == ConsciousnessLevel.Ascended))
            {
                if (!VisitedAmenti)
                {
                    SpeakAtlantean("The Halls of Amenti open before you. You came not by power hoarded but by the way of the One: harmony held, consciousness raised, the twelve keys gathered. Ancient wisdom floods you; your light vehicle is forever blessed with swifter flight and ascended awareness.");
                    VisitedAmenti = true;
                    AmentiBlessingActive = true;
                    ResonanceWidth *= PHI;
                    ConsciousnessValue = 1f;
                    ConsciousnessStage = ConsciousnessLevel.Ascended;
                }
            }
            else if (!_amentiSealedAnnounced && TempleKeys.Count > 0)
            {
                // Only speak the locked-door line once the player is actually on the path (has a key). The ship
                // spawns on the origin, where Amenti sits, so without this guard the very first line a new pilot
                // hears is the endgame's "remain sealed" message, which reads as a failure state.
                int missing = GameConstants.MinorTempleCount - TempleKeys.Count;
                SpeakAtlantean($"The Halls of Amenti remain sealed. {missing} more temple keys needed, or consciousness level insufficient.");
                _amentiSealedAnnounced = true;
            }
        }
    }

    /// <summary>
    /// The frequency the by-ear cue and the fine tuning rate should steer toward this frame: a nearby claimable
    /// temple's key note, else a nearby pyramid's resonance band centre, else NaN (meaning "use the realm's own
    /// still centre"). Suppressed during the tutorial so it can't fight the tutorial's centre-based steps.
    /// </summary>
    public float CurrentObjectiveNote()
    {
        if (TutorialActive) return float.NaN;
        if (NearTemple != null && NearTemple.KeyIndex >= 0 && !TempleKeys.Contains(NearTemple.KeyIndex))
            return NearTemple.Frequency;
        if (NearPyramid != null)
            return (GameConstants.PyramidResonanceRange.Min + GameConstants.PyramidResonanceRange.Max) * 0.5f;
        return float.NaN;
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
            SpeakAtlantean($"Entering {CurrentLeyLine!.Name}. Speed enhanced.");
            _leyLineAnnounced = true;
        }
        else if (!OnLeyLine && wasOnLeyLine)
        {
            SpeakAtlantean("Leaving ley line. Normal speed resumed.");
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
            SpeakAtlantean($"Entering {NearPyramid.Name}. The resonance chamber awakens.");
            _pyramidAnnounced = true;
        }
        else if (NearPyramid == null && wasNear)
        {
            SpeakAtlantean("Leaving pyramid resonance chamber.");
            _pyramidAnnounced = false;
        }
    }

    #endregion

    #region Temple resonance reading

    /// <summary>
    /// Speak the nearby temple's resonance key (the T key, in flight). In normal mode it gives the exact
    /// frequency to tune a realm to; by ear it instead tells you how close your nearest realm already is.
    /// The master temple (Halls of Amenti) reports its keys-and-consciousness gate rather than a single note.
    /// </summary>
    public void ReportTempleResonance()
    {
        // Find the nearest temple on demand. NearTemple is only refreshed ~once per second, so reading it
        // directly could report a temple just left (or miss one just entered); recomputing here keeps the
        // spoken reading honest with where the player actually is.
        Temple? temple = null;
        float bestDist = float.MaxValue;
        float scanRange = GetEffectiveScanRange();
        foreach (var t in Temples)
        {
            float d = t.DistanceTo(Position);
            if (d < scanRange && d < bestDist) { bestDist = d; temple = t; }
        }

        if (temple == null)
        {
            Speak("No temple within range for a resonance reading.");
            return;
        }

        if (temple.Kind == TempleType.Master)
        {
            SpeakAtlantean($"The Halls of Amenti answer only to all twelve keys and an awakened mind. You hold {TempleKeys.Count} of {GameConstants.MinorTempleCount} keys.");
            return;
        }

        if (TempleKeys.Contains(temple.KeyIndex))
        {
            SpeakAtlantean($"Temple of {temple.KeyName}. Its key is already yours.");
            return;
        }

        if (ByEarMode)
        {
            // By-ear reading: how close your best-tuned realm sits to the temple's key — and whether that is
            // already enough to CLAIM it, so "very close" doesn't blur the actual grant threshold.
            float effectiveWidth = ResonanceWidth * GameConstants.ConsciousnessLevels[ConsciousnessStage].Mult;
            float bestRes = 0f;
            int bestRealm = 0;
            for (int i = 0; i < N; i++)
            {
                float r = ResonancePhysics.Resonance(MathF.Abs(RDrive[i] - temple.Frequency), effectiveWidth);
                if (r > bestRes) { bestRes = r; bestRealm = i; }
            }
            if (bestRes > GameConstants.TempleKeyClaimResonance)
                SpeakAtlantean($"Temple of {temple.KeyName}. Realm {bestRealm + 1} is close enough to claim its key. Hold steady.");
            else
                SpeakAtlantean($"Temple of {temple.KeyName}. Realm {bestRealm + 1} is {ResonanceWord(bestRes)} to its key.");
        }
        else
        {
            SpeakAtlantean($"Temple of {temple.KeyName}. Its resonance key is {temple.Frequency:F0} hertz.");
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
        // Pick the highest tier whose threshold we meet by walking the EXPLICIT low-to-high order array, not
        // the dictionary (whose enumeration order is not guaranteed by contract and would silently mis-rank).
        ConsciousnessLevel newStage = GameConstants.ConsciousnessLevelOrder[0];
        foreach (var level in GameConstants.ConsciousnessLevelOrder)
            if (ConsciousnessValue >= GameConstants.ConsciousnessLevels[level].Threshold)
                newStage = level;
        ConsciousnessStage = newStage;

        if (ConsciousnessStage != oldStage && !_consciousnessAnnounced)
        {
            int oldRank = Array.IndexOf(GameConstants.ConsciousnessLevelOrder, oldStage);
            int newRank = Array.IndexOf(GameConstants.ConsciousnessLevelOrder, ConsciousnessStage);
            if (newRank >= oldRank)
            {
                var info = GameConstants.ConsciousnessLevels[ConsciousnessStage];
                SpeakAtlantean($"Consciousness rises to {ConsciousnessStage}. {info.Desc}. The universe opens wider to your senses.");
            }
            else
            {
                // A demotion must NOT use the rising wording: the player's tuning width and hearing range just
                // shrank, and for a blind-first game this spoken line is their only signal of the change.
                SpeakAtlantean($"Consciousness recedes to {ConsciousnessStage}. The universe narrows; raise your resonance to reawaken.");
            }
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
    /// The dwelling / regeneration bath — the meditative heart that rewards <em>holding</em> resonance.
    /// When the pilot sustains high mean resonance while nearly still (and not anchored on a planet), a
    /// regeneration bath forms: integrity is steadily restored, consciousness rises along the contemplative
    /// path, and soft golden-chord swells deepen the soundscape. It disperses the moment resonance breaks.
    /// </summary>
    private void UpdateDwelling(float dt)
    {
        float avgRes = Vec5.Mean(ResonanceLevels);
        float speed = Vec5.Norm(Velocity);
        bool eligible = !LandedMode
                        && avgRes >= GameConstants.DwellResonanceThreshold
                        && speed < MaxVelocity * GameConstants.DwellStillFactor;

        if (eligible)
        {
            DwellTimer += dt;
            if (!InRegeneration && DwellTimer >= GameConstants.DwellEnterTime)
            {
                InRegeneration = true;
                _lastDwellSwell = SimulationTime;
                SpeakAtlantean("You settle into the resonance. A regeneration bath forms around you; rest here and be restored.");
            }
            if (InRegeneration)
            {
                ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + GameConstants.DwellHealRate * dt);
                ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + GameConstants.DwellConsciousnessRate * dt);
                if (SimulationTime - _lastDwellSwell >= GameConstants.DwellSwellInterval)
                {
                    _audio.AddSoundEffect(new GameSoundEffect(_audio.GoldenChordWaveform, volume: 0.3f * _audio.EffectVolume));
                    _lastDwellSwell = SimulationTime;
                }
            }
        }
        else if (InRegeneration)
        {
            InRegeneration = false;
            SpeakAtlantean("The resonance bath disperses.");
            DwellTimer = 0f;
        }
        else
        {
            DwellTimer = 0f;
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
                        SpeakAtlantean($"Brainwave state: {stateName}. {FormatName(stateInfo.State)} mode.");
                        if (stateInfo.Effect == BrainwaveEffect.AutoRepair)
                            ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + 0.05f);
                        else if (stateInfo.Effect == BrainwaveEffect.RiftVision)
                            SpeakAtlantean("Enhanced Harmonic Chamber perception activated.");
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
            SpeakAtlantean($"{GameUtils.SpacePascalCase(hType.ToString())} harmonic detected between {dimNames}.");

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

            // The velocity-affecting harmonics (Octave boost, Tritone jitter) are applied per-frame in
            // ApplyHarmonicVelocity, NOT here: this pass runs only periodically, so the per-frame velocity
            // recompute used to overwrite them and they did nothing.
            switch (hType)
            {
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
            }
        }
        foreach (string k in _harmonicsToRemove) ActiveHarmonics.Remove(k);
    }

    /// <summary>
    /// Apply the velocity-affecting harmonics — octave (a steady boost) and tritone (chaotic jitter) — EVERY
    /// frame, after the velocity recompute, for each still-active harmonic. This is the per-frame home for
    /// these bonuses; doing it in the periodic <see cref="ApplyHarmonicBonuses"/> let them be overwritten.
    /// Non-compounding: velocity is rebuilt from scratch each frame, so the multiplier is a constant boost.
    /// </summary>
    public void ApplyHarmonicVelocity()
    {
        foreach (var (_, (hType, dims, expiry)) in ActiveHarmonics)
        {
            if (SimulationTime > expiry) continue;
            if (hType == HarmonicType.Octave)
                foreach (int d in dims) Velocity[d] *= 1.1f;
            else if (hType == HarmonicType.Tritone)
                foreach (int d in dims) Velocity[d] += MathHelpers.RandomRange(-0.2f, 0.2f);
        }
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
        SpeakAtlantean("Crystal threshold reached. This is the way of accumulation: power gathered, the cycle renewed, your light vehicle reborn into a fresh universe. Yet the deeper way, the way of the One, still calls toward the Halls of Amenti.");
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
        SpeakAtlantean(warpMsg);

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
