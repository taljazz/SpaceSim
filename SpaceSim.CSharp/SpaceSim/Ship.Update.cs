using System;
using System.Collections.Generic;
using System.IO;

using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;

namespace SpaceSim;

public partial class Ship
{
    // =========================================================================
    //  MAIN UPDATE
    // =========================================================================

    public void Update(float dt, List<CelestialBody> celestialBodies, KeyboardState keys,
                       List<Temple>? temples, List<LeyLine>? leyLines, List<Pyramid>? pyramids)
    {
        if (IsInMenuMode) return;

        // Atlantean structure proximity
        if (temples != null && temples.Count > 0) CheckTempleProximity(temples);
        if (leyLines != null && leyLines.Count > 0) CheckLeyLineProximity(leyLines);
        if (pyramids != null && pyramids.Count > 0) CheckPyramidProximity(pyramids);

        // Idle mode
        if (SimulationTime - _lastInputTime > GameConstants.IdleTimeThreshold && !IdleMode)
        {
            IdleMode = true;
            DebugLogger.Log("Ship", "Idle mode activated (120s no input)");
            Speak("Entering cosmic meditation mode.");
        }

        if (IdleMode)
        {
            for (int i = 0; i < N; i++)
                RDrive[i] += (FTarget[i] - RDrive[i]) * 0.01f;
        }

        // Landed mode
        if (LandedMode)
        {
            Array.Clear(Velocity);
            float shift = (Biome == PlanetBiome.Dissonant ? 10f : 1f) * dt;
            for (int i = 0; i < N; i++)
            {
                FTarget[i] += MathHelpers.RandomRange(-shift, shift);
                FTarget[i] = Math.Clamp(FTarget[i], GameConstants.FrequencyMin, GameConstants.FrequencyMax);
                float df = RDrive[i] - FTarget[i];
                ResonanceLevels[i] = ResonancePhysics.Resonance(df, ResonanceWidth);
            }
            return;
        }

        // Environmental influence on targets (uses spatial grid for O(small) instead of O(810))
        Array.Clear(_envInfluence);
        if (SpatialGrid != null)
            SpatialGrid.GetNearby(Position, GameConstants.InteractionDistance, _nearbyBuffer);
        else
        { _nearbyBuffer.Clear(); _nearbyBuffer.AddRange(celestialBodies); }
        foreach (var body in _nearbyBuffer)
        {
            if (LockedTarget != null)
            {
                // Skip influence from locked target
                bool isLocked = true;
                for (int d = 0; d < N; d++)
                    if (MathF.Abs(body.Position[d] - LockedTarget[d]) > 0.001f) { isLocked = false; break; }
                if (isLocked) continue;
            }

            bool anyClose = false;
            for (int d = 0; d < N; d++)
            {
                _dists[d] = MathF.Abs(Position[d] - body.Position[d]);
                if (_dists[d] < GameConstants.InteractionDistance) anyClose = true;
            }
            if (anyClose)
            {
                for (int d = 0; d < N; d++)
                {
                    if (_dists[d] < GameConstants.InteractionDistance)
                        _envInfluence[d] += (GameConstants.InteractionDistance - _dists[d]) / GameConstants.InteractionDistance * body.Frequency * MathF.Pow(PHI, d);
                }
            }
        }
        for (int i = 0; i < N; i++)
        {
            FTarget[i] = BaseFTarget[i] + _envInfluence[i];
            FTarget[i] = Math.Clamp(FTarget[i], GameConstants.FrequencyMin, GameConstants.FrequencyMax);
        }

        // Autopilot to locked target
        if (LockedTarget != null)
        {
            Vec5.SubtractInto(LockedTarget, Position, _dirVecBuffer);
            float norm = Vec5.Norm(_dirVecBuffer);
            if (norm < 1e-6f) norm = 1e-6f;

            float stopDist = LockedIsRift ? GameConstants.RiftAlignmentTolerance : 1f;
            if (norm < stopDist)
            {
                for (int i = 0; i < N; i++) RDrive[i] = FTarget[i];
                Array.Clear(Velocity);
                if (LockedIsRift && !_approachedRiftAnnounced)
                {
                    Speak("Approached rift - ready for entry.");
                    _approachedRiftAnnounced = true;
                }
                else if (!LockedIsRift)
                {
                    LockedTarget = null;
                    LockedIsRift = false;
                    StopLockSound();
                    Speak("Target reached.");
                }
            }
            else
            {
                float slowdownFactor = MathF.Min(1f, norm / GameConstants.SlowdownDist);
                for (int i = 0; i < N; i++)
                {
                    float dirI = _dirVecBuffer[i];
                    float desiredVelI = (dirI / norm) * MaxVelocity * slowdownFactor;
                    float targetRes = MathF.Abs(desiredVelI) > 0.01f ? MathF.Min(0.999f, MathF.Abs(desiredVelI) / MaxVelocity) : 0;
                    float targetDrive = ResonancePhysics.DriveForTargetResonance(
                        FTarget[i], targetRes, ResonanceWidth, MathF.Sign(desiredVelI));

                    if (norm < GameConstants.SlowdownDist / 2f)
                        RDrive[i] = targetDrive;
                    else
                    {
                        float autopilotRate = 0.1f;
                        if (TuaoiMode == TuaoiMode.Navigation)
                            autopilotRate *= _cachedTuaoiInfo.Rate;
                        RDrive[i] += (targetDrive - RDrive[i]) * autopilotRate;
                    }
                }

                // Update lock sound pan
                if (LockSound != null)
                {
                    var proj = ProjectRelative(LockedTarget);
                    float angle = MathF.Atan2(proj.Y, proj.X);
                    LockSound.Pan = MathF.Sin(angle);
                    LockSound.Volume = _audio.BeepVolume;
                }
            }
        }

        // Auto-rotate view toward locked target
        if (LockedTarget != null)
        {
            Vec5.SubtractInto(LockedTarget, Position, _dirVecBuffer);
            float norm = Vec5.Norm(_dirVecBuffer);
            if (norm > 1f)
            {
                float dx = _dirVecBuffer[0];
                float dw = _dirVecBuffer[3];
                float targetR;
                if (MathF.Abs(dx) + MathF.Abs(dw) > 1e-6f)
                {
                    targetR = MathF.Atan2(dw, dx);
                    float projX = dx * MathF.Cos(targetR) + dw * MathF.Sin(targetR);
                    if (projX < 0) targetR += MathF.PI;
                }
                else
                    targetR = ViewRotation;

                float deltaR = targetR - ViewRotation;
                if (deltaR > MathF.PI) deltaR -= MathF.Tau;
                else if (deltaR < -MathF.PI) deltaR += MathF.Tau;
                ViewRotation += deltaR * 0.5f;
            }
        }

        // Calculate resonance and velocity
        for (int i = 0; i < N; i++)
        {
            float df = RDrive[i] - FTarget[i];
            float effectiveWidth = ResonanceWidth;
            if (TuaoiMode == TuaoiMode.Transcendence && i >= 3)
                effectiveWidth *= GameConstants.TuaoiModes[TuaoiMode.Transcendence].Rate;
            ResonanceLevels[i] = ResonancePhysics.Resonance(df, effectiveWidth);

            if (ResonanceLevels[i] > GameConstants.PerfectResonanceThreshold &&
                _prevResonanceLevels[i] <= GameConstants.PerfectResonanceThreshold)
                GameEvents.RaisePlaySound(this, _audio.ClickWaveform, volume: _audio.EffectVolume);

            if (ResonanceLevels[i] > GameConstants.PowerBuildThreshold)
                ResonancePower[i] += dt;
            else
                ResonancePower[i] = 0;

            float boost = 1f + (ResonancePower[i] / GameConstants.PowerBuildTime) * PHI;
            Velocity[i] = MaxVelocity * ResonanceLevels[i] * MathF.Sign(df) * boost;
        }

        // Ley line speed boost
        if (OnLeyLine)
            Vec5.ScaleInPlace(Velocity, GameConstants.LeyLineSpeedMult);

        // Merkaba velocity boost
        if (MerkabaActive)
            Vec5.ScaleInPlace(Velocity, GameConstants.MerkabaVelocityBoost);

        // Power mode boost
        if (TuaoiMode == TuaoiMode.Power)
            Vec5.ScaleInPlace(Velocity, GameConstants.TuaoiModes[TuaoiMode.Power].Rate);

        // Pyramid healing
        if (NearPyramid != null)
        {
            bool freqMatch = false;
            for (int i = 0; i < N; i++)
                if (RDrive[i] >= GameConstants.PyramidResonanceRange.Min && RDrive[i] <= GameConstants.PyramidResonanceRange.Max)
                { freqMatch = true; break; }
            if (freqMatch)
                ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + GameConstants.PyramidHealingMult * 0.01f * dt);
        }

        // Harmonic detection
        if (SimulationTime - _lastHarmonicCheck > GameConstants.HarmonicDetectionInterval)
        {
            var harmonics = DetectHarmonicRelationships();
            ApplyHarmonicBonuses(harmonics);
            _lastHarmonicCheck = SimulationTime;
        }

        // Solfeggio detection
        if (SimulationTime - _lastSolfeggioCheck > 0.5f)
        {
            foreach (var (freq, info) in GameConstants.SolfeggioFrequencies)
            {
                for (int i = 0; i < N; i++)
                {
                    if (MathF.Abs(RDrive[i] - freq) < GameConstants.SolfeggioTolerance)
                    {
                        if (!ActiveSolfeggio.ContainsKey(freq))
                            Speak($"Solfeggio {info.Name} frequency detected. {Capitalize(info.Desc)}.");
                        ActiveSolfeggio[freq] = (info.Effect, SimulationTime + 2f);
                    }
                }
            }
            // Clean expired (no LINQ — reuse pre-allocated list)
            _expiredKeys.Clear();
            foreach (var kv in ActiveSolfeggio)
                if (kv.Value.Expiry <= SimulationTime)
                    _expiredKeys.Add(kv.Key);
            foreach (var k in _expiredKeys)
                ActiveSolfeggio.Remove(k);
            _lastSolfeggioCheck = SimulationTime;
        }

        // Merkaba activation
        bool allAbove = Vec5.All(ResonanceLevels, r => r > GameConstants.MerkabaActivationThreshold);
        if (allAbove && !MerkabaActive)
        {
            MerkabaActive = true;
            DebugLogger.Log("Ship", "Merkaba ACTIVATED - all dimensions > 0.9");
            GameEvents.RaiseMerkabaActivated(this);
            if (!_merkabaAnnounced)
            {
                Speak("Merkaba activated. Light vehicle field engaged. All realms in harmonic alignment.");
                _merkabaAnnounced = true;
            }
        }
        else if (!allAbove && MerkabaActive)
        {
            MerkabaActive = false;
            DebugLogger.Log("Ship", "Merkaba DEACTIVATED");
            GameEvents.RaiseMerkabaDeactivated(this);
            _merkabaAnnounced = false;
            Speak("Merkaba field collapsed. Realign frequencies.");
        }

        // Temple resonance (110 Hz)
        bool templeResActive = false;
        for (int i = 0; i < N; i++)
            if (RDrive[i] >= GameConstants.TempleResonanceRange.Min && RDrive[i] <= GameConstants.TempleResonanceRange.Max)
            { templeResActive = true; break; }
        if (templeResActive && !InTempleResonance)
        {
            InTempleResonance = true;
            if (!_templeAnnounced)
            {
                Speak("Temple resonance detected. Ancient healing frequency 110 hertz active.");
                _templeAnnounced = true;
            }
        }
        else if (!templeResActive && InTempleResonance)
        {
            InTempleResonance = false;
            _templeAnnounced = false;
        }

        // Tuaoi mode effects
        if (TuaoiMode == TuaoiMode.Healing)
            ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + _cachedTuaoiInfo.Rate * dt);
        else if (TuaoiMode == TuaoiMode.Regeneration && InTempleResonance)
            ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + GameConstants.TempleHealingRate * dt);

        // Extended Atlantean updates
        UpdateConsciousness(dt);
        DetectBrainwaveState();
        UpdateAstralMode(dt);
        UpdateIntentionNavigation(dt);

        if (PatternBonusTimer > 0) PatternBonusTimer -= dt;

        // Dissonance
        float avgRes = Vec5.Mean(ResonanceLevels);
        if (avgRes < GameConstants.DissonanceThreshold)
        {
            DissonanceTimer += dt;
            if (DissonanceTimer > GameConstants.DissonanceDuration)
            {
                for (int i = 0; i < N; i++)
                    Velocity[i] += MathHelpers.RandomRange(-0.5f, 0.5f);
                Speak("Dissonance detected-retune!");
                DissonanceTimer = 0f;
            }
        }
        else
            DissonanceTimer = 0f;

        // Verbose resonance alerts
        for (int i = 0; i < N; i++)
        {
            float change = MathF.Abs(ResonanceLevels[i] - _prevResonanceLevels[i]);
            if (VerboseMode > 0 && change > 0.1f)
                Speak($"Alert: Resonance in dim {i + 1} now {ResonanceLevels[i]:F2}.");
        }
        Array.Copy(ResonanceLevels, _prevResonanceLevels, N);

        // Easter egg
        bool allEasterEgg = true;
        for (int i = 0; i < N; i++)
            if (MathF.Abs(RDrive[i] - GameConstants.EasterEggFreq) >= GameConstants.EasterEggTolerance)
            { allEasterEgg = false; break; }
        if (allEasterEgg)
        {
            if (!EasterEggAnnounced) { Speak("You are the universe experiencing itself."); EasterEggAnnounced = true; }
        }
        else EasterEggAnnounced = false;

        // Random rift generation
        if (Random.Shared.NextSingle() < 0.001f && avgRes > 0.9f)
        {
            var riftPos = new float[N];
            for (int i = 0; i < N; i++)
                riftPos[i] = Position[i] + MathHelpers.RandomRange(-15f, 15f);
            riftPos[3] = riftPos[0] * PHI;
            riftPos[4] = riftPos[1] * PHI;
            RiftType[] riftTypes = { RiftType.Boost, RiftType.Crystal, RiftType.Hazard };
            RiftType riftType = riftTypes[Random.Shared.Next(riftTypes.Length)];
            var sound = new GameSoundEffect(_audio.RiftHumWaveform, loop: true, volume: 0f);
            _audio.AddSoundEffect(sound);
            Rifts.Add(new Rift { Position = riftPos, Timer = GameConstants.RiftFadeTime, RiftKind = riftType, Sound = sound, LastBeepTime = SimulationTime });

            var proj = ProjectRelative(riftPos);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            string dirStr = angle < 0 ? "left" : "right";
            Speak($"{riftType} Harmonic Chamber detected at {MathF.Abs(angle):F1} degrees {dirStr}.");
        }

        // Perfect fifth rift
        bool allPerfect = true;
        for (int i = 0; i < N; i++)
            if (MathF.Abs(RDrive[i] - FTarget[i]) >= GameConstants.PerfectFifthTolerance)
            { allPerfect = false; break; }
        if (allPerfect && Random.Shared.NextSingle() < GameConstants.PerfectFifthProb)
        {
            var riftPos = new float[N];
            for (int i = 0; i < N; i++)
                riftPos[i] = Position[i] + MathHelpers.RandomRange(-15f, 15f);
            riftPos[3] = riftPos[0] * PHI;
            riftPos[4] = riftPos[1] * PHI;
            var sound = new GameSoundEffect(_audio.RiftHumWaveform, loop: true, volume: 0f);
            _audio.AddSoundEffect(sound);
            Rifts.Add(new Rift { Position = riftPos, Timer = GameConstants.RiftFadeTime, RiftKind = RiftType.PerfectFifth, Sound = sound, LastBeepTime = SimulationTime });

            var proj = ProjectRelative(riftPos);
            float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
            string dirStr = angle < 0 ? "left" : "right";
            Speak($"Mythical Perfect Fifth Harmonic Chamber materialized at {MathF.Abs(angle):F1} degrees {dirStr}!");
        }

        // Update rifts
        for (int i = Rifts.Count - 1; i >= 0; i--)
        {
            var rift = Rifts[i];
            rift.Timer -= dt;
            if (rift.Timer <= 0)
            {
                if (rift == LockedRift)
                {
                    LockedRift = null;
                    LockedTarget = null;
                    LockedIsRift = false;
                    StopLockSound();
                    Speak("Locked rift faded into the void.");
                }
                else
                    Speak("Rift faded into the void.");
                if (rift.Sound != null) { rift.Sound.Loop = false; rift.Sound.Volume = 0; }
                Rifts.RemoveAt(i);
                continue;
            }

            if (avgRes > 0.9f) rift.Timer += dt * PHI;

            float dist = rift.DistanceTo(Position);
            if (rift.Sound != null)
            {
                var proj = ProjectRelative(rift.Position);
                float angle = MathF.Atan2(proj.Y, proj.X);
                rift.Sound.Pan = MathF.Sin(angle);
                rift.Sound.Volume = MathF.Max(0, _audio.EffectVolume * (1f - dist / GameConstants.RiftMaxDist)) * avgRes;
            }

            if (rift == LockedRift)
            {
                var proj = ProjectRelative(rift.Position);
                float angle = MathF.Atan2(proj.Y, proj.X);
                float pan = MathF.Sin(angle);
                float centeredFactor = 1f - MathF.Abs(pan);
                float interval = 2f - 1.8f * centeredFactor;
                if (SimulationTime - rift.LastBeepTime > interval)
                {
                    GameEvents.RaisePlaySound(this, _audio.RiftBeepWaveform, pan: pan, volume: _audio.BeepVolume);
                    rift.LastBeepTime = SimulationTime;
                }
            }

            if (dist < GameConstants.RiftAlignmentTolerance && avgRes <= GameConstants.RiftEntryResThreshold)
            {
                for (int d = 0; d < N; d++)
                    Velocity[d] += MathHelpers.RandomRange(-0.5f, 0.5f);
                Speak("Dissonance prevents rift entry.");
            }
        }

        // Update position with wrap
        for (int i = 0; i < N; i++)
        {
            Position[i] += Velocity[i] * dt;
            Position[i] = ((Position[i] + 100f) % 200f + 200f) % 200f - 100f;
        }

        // Rift charge sequence
        if (RiftChargeTimer > 0)
        {
            RiftChargeTimer -= dt;
            if (LockedRift != null && LockedTarget != null)
            {
                if (Vec5.Mean(ResonanceLevels) < GameConstants.RiftEntryResThreshold)
                {
                    RiftChargeTimer = 0;
                    Speak("Charge aborted-resonance too low. Retune.");
                }
                else if (RiftChargeTimer <= 0)
                {
                    EnterRift(LockedRift);
                }
            }
        }
        else
        {
            // Guidance while locked
            if (LockedIsRift && LockedTarget != null && SimulationTime - _lastGuidanceTime > 10f)
            {
                float dist = Vec5.Distance(Position, LockedTarget);
                float avgResP = Vec5.Mean(ResonanceLevels) * 100f;
                var proj = ProjectRelative(LockedTarget);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                float pan = MathF.Sin(angle * MathF.PI / 180f);
                float alignPct = (1f - MathF.Abs(pan)) * 100f;

                if (MathF.Abs(dist - _prevRiftDist) > 5f || MathF.Abs(alignPct - _prevRiftAlign) > 10f || MathF.Abs(avgResP - _prevRiftRes) > 10f)
                {
                    Speak($"Rift status: Distance {dist:F1}, alignment {alignPct:F0}%, resonance {avgResP:F0}%.");
                    if (alignPct < 50f)
                    {
                        Vec5.SubtractInto(LockedTarget, Position, _dirVecBuffer);
                        float targetR;
                        if (MathF.Abs(_dirVecBuffer[0]) + MathF.Abs(_dirVecBuffer[3]) > 1e-6f)
                            targetR = MathF.Atan2(_dirVecBuffer[3], _dirVecBuffer[0]);
                        else
                            targetR = ViewRotation;
                        float deltaR = targetR - ViewRotation;
                        Speak($"Rotate {(deltaR > 0 ? "right" : "left")} to center.");
                    }
                    _prevRiftDist = dist;
                    _prevRiftAlign = alignPct;
                    _prevRiftRes = avgResP;
                    _lastGuidanceTime = SimulationTime;
                }
            }
        }

        // Detect nearby celestial bodies (spatial grid query)
        float scanRange = GetEffectiveScanRange();
        NearestBody = null;
        float minDist = float.MaxValue;
        bool nearAny = false;
        if (SpatialGrid != null)
            SpatialGrid.GetNearby(Position, scanRange, _nearbyBuffer);
        else
        { _nearbyBuffer.Clear(); _nearbyBuffer.AddRange(celestialBodies); }
        foreach (var body in _nearbyBuffer)
        {
            float dist = body.DistanceTo(Position);
            if (dist < scanRange)
            {
                nearAny = true;
                if (dist < minDist) { minDist = dist; NearestBody = body; }
            }
        }
        if (nearAny && !NearObject)
        {
            NearObject = true;
            Speak("Approaching celestial object. Resonance influenced.");
        }
        else if (!nearAny && NearObject)
        {
            NearObject = false;
            Speak("Leaving object vicinity. Base targets restored.");
        }

        // Proximity ambient audio
        if (AmbientSoundsEnabled && NearestBody != null)
        {
            float dist = NearestBody.DistanceTo(Position);
            var proj = ProjectRelative(NearestBody.Position);
            float angle = MathF.Atan2(proj.Y, proj.X);
            float pan = MathF.Sin(angle);

            if (NearObject && SimulationTime - _lastBeepTime > 1f)
            {
                GameEvents.RaisePlaySound(this, _audio.BeepWaveform, pan: pan, volume: _audio.BeepVolume);
                _lastBeepTime = SimulationTime;
            }

            // Type-specific ambient
            HandleProximityAmbient(NearestBody, dist, pan);
        }

        // Stop ambient when leaving
        if (!NearObject || NearestBody == null || !AmbientSoundsEnabled)
            StopAllAmbientSounds();

        // Nebula dissonance
        if (NebulaDissonanceEnabled && NearestBody != null && NearestBody.BodyType == CelestialBodyType.Nebula)
        {
            float dist = NearestBody.DistanceTo(Position);
            if (dist < GameConstants.NebulaDissonanceRadius)
            {
                float dissonance = NearestBody.Dissonance;
                float strength = dissonance * (1f - dist / GameConstants.NebulaDissonanceRadius);

                float driftAmt = strength * 15f * dt;
                for (int i = 0; i < N; i++)
                {
                    float drift = (Random.Shared.NextSingle() - 0.5f) * driftAmt;
                    FTarget[i] = Math.Clamp(FTarget[i] + drift, GameConstants.FrequencyMin, GameConstants.FrequencyMax);
                }

                if (dissonance > 0.6f)
                {
                    float turbulence = strength * 0.5f;
                    for (int i = 0; i < N; i++)
                        Velocity[i] += (Random.Shared.NextSingle() - 0.5f) * turbulence;
                }

                if (!_nebulaDissonanceAnnounced)
                {
                    Speak("Warning: Entering nebula dissonance field. Frequencies unstable.");
                    _nebulaDissonanceAnnounced = true;
                }
            }
            else if (_nebulaDissonanceAnnounced)
            {
                Speak("Nebula dissonance field cleared. Frequencies stable.");
                _nebulaDissonanceAnnounced = false;
            }
        }
        else if (_nebulaDissonanceAnnounced)
        {
            Speak("Nebula dissonance field cleared. Frequencies stable.");
            _nebulaDissonanceAnnounced = false;
        }

        // Landmarks during rotation (spatial grid query)
        if (RotatingLeft || RotatingRight)
        {
            if (SpatialGrid != null)
                SpatialGrid.GetNearby(Position, GameConstants.ScannerRange, _nearbyBuffer);
            else
            { _nearbyBuffer.Clear(); _nearbyBuffer.AddRange(celestialBodies); }
            foreach (var body in _nearbyBuffer)
            {
                var proj = ProjectRelative(body.Position);
                float angle = MathF.Atan2(proj.Y, proj.X) * 180f / MathF.PI;
                if (MathF.Abs(angle) < GameConstants.ViewLandmarkThreshold &&
                    SimulationTime - _lastLandmarkSpeakTime > GameConstants.LandmarkSpeechCooldown)
                {
                    Speak($"Object in view at {angle:F1} degrees.");
                    _lastLandmarkSpeakTime = SimulationTime;
                }
            }
        }

        // Landing timer
        if (LandingTimer > 0)
        {
            LandingTimer -= dt;
            if (LandingTimer <= 0)
            {
                float landingThreshold = GameConstants.LandingThreshold;
                if (NearestBody != null && NearestBody.BodyType == CelestialBodyType.Planet)
                    landingThreshold *= NearestBody.Difficulty;

                if (Vec5.Mean(ResonanceLevels) > landingThreshold && NearestBody != null && NearestBody.BodyType == CelestialBodyType.Planet)
                {
                    LandedMode = true;
                    LandedPlanet = Vec5.Clone(NearestBody.Position);
                    LandedPlanetBody = NearestBody;
                    DebugLogger.Log("Ship", $"LANDED on planet: type={NearestBody.ExoplanetClass}, pos={Vec5.Format(NearestBody.Position)}");
                    GameEvents.RaiseLandingEvent(this, true, NearestBody);
                    GenerateCrystals();
                }
                else
                {
                    ResonanceIntegrity -= 0.1f;
                    if (NearestBody != null && NearestBody.BodyType != CelestialBodyType.Planet)
                        Speak("Cannot anchor on this celestial body.");
                    else
                    {
                        float difficulty = NearestBody?.Difficulty ?? 1f;
                        Speak(difficulty > 1f
                            ? "Anchoring failed. This world requires exceptionally high harmonic alignment. Integrity reduced."
                            : "Anchoring failed due to dissonance. Integrity reduced.");
                    }
                    if (ResonanceIntegrity < 0.5f)
                        Speak("Warning: Low integrity-repair needed.");
                }
            }
        }

        // Autosave
        if (AutosaveEnabled && SimulationTime - _lastAutosaveTime > GameConstants.AutosaveInterval)
        {
            SaveGame();
            _lastAutosaveTime = SimulationTime;
        }
    }
}
