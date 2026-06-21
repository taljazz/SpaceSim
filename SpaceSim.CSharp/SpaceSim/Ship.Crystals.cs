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
    //  CRYSTAL GENERATION (on landed planet)
    // =========================================================================

    public void GenerateCrystals()
    {
        CrystalPositions.Clear();
        CrystalFreqs.Clear();
        LockedCrystals.Clear();
        Biome = Random.Shared.NextDouble() < 0.5 ? PlanetBiome.Harmonic : PlanetBiome.Dissonant;
        PatternProgress.Clear();

        float crystalMult = 1f;
        if (LandedPlanetBody != null)
            crystalMult = LandedPlanetBody.CrystalMult;

        int baseCount = Random.Shared.Next(1 + CrystalBonus, 9 + CrystalBonus);
        CrystalCount = Math.Max(1, (int)(baseCount * crystalMult));

        // Detect sacred geometry pattern
        CurrentPattern = null;
        foreach (var (patName, patInfo) in GameConstants.SacredPatterns)
        {
            if (patInfo.Points == CrystalCount)
            {
                CurrentPattern = patName;
                break;
            }
        }

        var exoType = LandedPlanetBody?.ExoplanetClass ?? ExoplanetType.SuperEarth;
        string exoDesc = GameConstants.ExoplanetTypes[exoType].Desc;

        string patternMsg = "";
        if (CurrentPattern != null)
            patternMsg = $" Sacred {FormatName(CurrentPattern)} pattern detected!";
        Speak($"Anchored on {Biome} biome planet. {Capitalize(exoDesc)}. {CrystalCount} Atlantean crystals detected.{patternMsg}");
        DebugLogger.Log("Ship", $"GenerateCrystals: {CrystalCount} crystals, biome={Biome}, pattern={CurrentPattern ?? "none"}, mult={crystalMult:F1}");

        float scaleFactor = GameConstants.ScaleFactor;

        for (int i = 0; i < CrystalCount; i++)
        {
            float px, py;

            if (CurrentPattern == "seed_of_life" && CrystalCount == 7)
            {
                if (i == 0) { px = 0; py = 0; }
                else
                {
                    float angle = (i - 1) * (MathF.Tau / 6f);
                    float r = scaleFactor / 10f;
                    px = r * MathF.Cos(angle);
                    py = r * MathF.Sin(angle);
                }
            }
            else if (CurrentPattern == "merkaba" && CrystalCount == 8)
            {
                if (i < 4)
                {
                    float angle = i * (MathF.Tau / 4f) + MathF.PI / 4f;
                    float r = scaleFactor / 10f;
                    px = r * MathF.Cos(angle);
                    py = r * MathF.Sin(angle);
                }
                else
                {
                    float angle = (i - 4) * (MathF.Tau / 4f);
                    float r = scaleFactor / 10f * PHI;
                    px = r * MathF.Cos(angle);
                    py = r * MathF.Sin(angle);
                }
            }
            else if (CurrentPattern == "golden_spiral" && CrystalCount == 5)
            {
                float theta = i * MathF.Tau * PHI;
                int fibIdx = i % GameConstants.FibSeq.Length;
                float r = GameConstants.FibSeq[fibIdx] * (scaleFactor / 10f);
                px = r * MathF.Cos(theta);
                py = r * MathF.Sin(theta);
            }
            else
            {
                float theta = i * MathF.Tau * PHI;
                int fibIdx = i % GameConstants.FibSeq.Length;
                float r = GameConstants.FibSeq[fibIdx] * (scaleFactor / 10f);
                px = r * MathF.Cos(theta);
                py = r * MathF.Sin(theta);
            }

            CrystalPositions.Add(new[] { px, py });

            // Assign Atlantean crystal type with chance
            if (Random.Shared.NextSingle() < GameConstants.AtlanteanCrystalChance)
            {
                var types = GameConstants.AtlanteanCrystalTypes.Keys.ToArray();
                string cType = types[Random.Shared.Next(types.Length)];
                var cInfo = GameConstants.AtlanteanCrystalTypes[cType];
                float baseFreq = MathHelpers.RandomRange(cInfo.FreqMin, cInfo.FreqMax);
                var freqs = new float[N];
                for (int d = 0; d < N; d++)
                    freqs[d] = baseFreq + MathHelpers.RandomRange(-20f, 20f);
                CrystalFreqs.Add(new CrystalData { Freqs = freqs, AtlanteanType = cType, Special = true });
            }
            else
            {
                var freqs = new float[N];
                for (int d = 0; d < N; d++)
                    freqs[d] = MathHelpers.RandomRange(GameConstants.FrequencyMin, GameConstants.FrequencyMax);
                CrystalFreqs.Add(new CrystalData { Freqs = freqs, AtlanteanType = null, Special = false });
            }
        }

        string freqStr = string.Join(", ", CrystalFreqs.Select(f => $"{f.Freqs[0]:F2}"));
        Speak($"Crystals detected at frequencies: {freqStr} Hz in primary dim.");
        _approachingLockAnnounced = false;

        // Play biome sound
        StopBiomeSound();
        float[] waveform = Biome == PlanetBiome.Harmonic
            ? _audio.GoldenChordWaveform
            : _audio.SupernovaChaos; // dissonant waveform
        _biomeSound = new GameSoundEffect(waveform, loop: true, volume: _audio.EffectVolume * 0.5f);
        _audio.AddSoundEffect(_biomeSound);
    }

    private void StopBiomeSound()
    {
        // We can't remove from the audio system's internal list directly,
        // but we can mark it finished by setting volume to 0 and non-looping
        if (_biomeSound != null)
        {
            _biomeSound.Loop = false;
            _biomeSound.Volume = 0;
            _biomeSound = null;
        }
    }

    // =========================================================================
    //  SCAN / COLLECT CRYSTAL
    // =========================================================================

    public void ScanNearestCrystal()
    {
        if (CrystalPositions.Count == 0) return;

        var dists = new float[CrystalPositions.Count];
        for (int i = 0; i < CrystalPositions.Count; i++)
            dists[i] = LockedCrystals.Contains(i) ? float.MaxValue : Dist2D(CursorPos, CrystalPositions[i]);

        int nearest = MathHelpers.ArgMin(dists);
        if (dists[nearest] >= float.MaxValue)
        {
            Speak("No more crystals to scan on this planet.");
            return;
        }

        var crystal = CrystalFreqs[nearest];
        float[] cFreqs = crystal.Freqs;

        // Auto-snap if close enough
        float[] tempRes = new float[N];
        for (int i = 0; i < N; i++)
        {
            float df = RDrive[i] - cFreqs[i];
            tempRes[i] = ResonancePhysics.Resonance(df, ResonanceWidth);
        }
        if (Vec5.Mean(tempRes) > GameConstants.AutoSnapThreshold)
        {
            for (int i = 0; i < N; i++)
                RDrive[i] = cFreqs[i];
            GameEvents.RaisePlaySound(this, _audio.ClickWaveform, volume: _audio.BeepVolume);
        }

        float freq = cFreqs[SelectedDim];
        float dx = CrystalPositions[nearest][0] - CursorPos[0];
        float dy = CrystalPositions[nearest][1] - CursorPos[1];
        string dir = "";
        if (dy > 0) dir += "north ";
        else if (dy < 0) dir += "south ";
        if (dx > 0) dir += "east";
        else if (dx < 0) dir += "west";

        string specialMsg = "";
        if (crystal.Special && crystal.AtlanteanType != null)
            specialMsg = $" Special {FormatName(crystal.AtlanteanType)} crystal!";
        Speak($"Nearest crystal {dists[nearest]:F1} units {dir}. Target freq in dim {SelectedDim + 1}: {freq:F2} Hz.{specialMsg}");

        float angle = MathF.Atan2(dy, dx);
        float pan = MathF.Cos(angle);
        GameEvents.RaisePlaySound(this, _audio.BeepWaveform, pan: pan, volume: _audio.BeepVolume);
    }

    public void CollectCrystal()
    {
        if (CrystalPositions.Count == 0)
        {
            Speak("No crystals on this planet.");
            return;
        }

        var dists = new float[CrystalPositions.Count];
        for (int i = 0; i < CrystalPositions.Count; i++)
            dists[i] = LockedCrystals.Contains(i) ? float.MaxValue : Dist2D(CursorPos, CrystalPositions[i]);

        int nearest = MathHelpers.ArgMin(dists);
        if (dists[nearest] > 1f || dists[nearest] >= float.MaxValue)
        {
            Speak("No collectable crystal nearby.");
            return;
        }

        var crystal = CrystalFreqs[nearest];
        float[] cFreqs = crystal.Freqs;

        for (int i = 0; i < N; i++)
        {
            float df = RDrive[i] - cFreqs[i];
            ResonanceLevels[i] = ResonancePhysics.Resonance(df, ResonanceWidth);
        }

        if (Vec5.Mean(ResonanceLevels) > GameConstants.CrystalCollectionThreshold)
        {
            LockedCrystals.Add(nearest);
            PatternProgress.Add(nearest);

            int crystalValue = 1;
            if (crystal.Special && crystal.AtlanteanType != null &&
                GameConstants.AtlanteanCrystalTypes.ContainsKey(crystal.AtlanteanType))
            {
                var cInfo = GameConstants.AtlanteanCrystalTypes[crystal.AtlanteanType];
                crystalValue = (int)cInfo.Mult;
                Speak($"Ancient {FormatName(crystal.AtlanteanType)} crystal collected! {cInfo.Desc}. Value: {crystalValue} crystals.");
                ApplyAtlanteanCrystalEffect(crystal.AtlanteanType);
            }
            else
            {
                float avgFreq = cFreqs.Average();
                var (typeName, typeInfo) = GetCrystalType(avgFreq);
                Speak($"Atlantean {Capitalize(typeName)} crystal collected. {Capitalize(typeInfo.Chakra)} chakra resonance. Harmony increases.");
            }

            CrystalsCollected += crystalValue;
            GameEvents.RaisePlaySound(this, _audio.ClickWaveform, volume: _audio.BeepVolume);

            // Raise crystal collected event
            {
                float avgFreq = cFreqs.Average();
                var (cTypeName, cTypeInfo) = GetCrystalType(avgFreq);
                GameEvents.RaiseCrystalCollected(this, crystal.Special ? (crystal.AtlanteanType ?? cTypeName) : cTypeName, cTypeInfo.Chakra, CrystalsCollected);
            }

            if (Random.Shared.NextSingle() < 0.2f)
                Speak("Ancient echo: The spiral binds all realms in golden eternity.");

            // Sacred geometry pattern completion
            if (LockedCrystals.Count == CrystalCount)
            {
                if (CurrentPattern != null && GameConstants.SacredPatterns.TryGetValue(CurrentPattern, out var pInfo))
                {
                    PatternBonusTimer = 30f;
                    int bonusCrystals = (int)(CrystalCount * (pInfo.Mult - 1f));
                    if (bonusCrystals > 0)
                        CrystalsCollected += bonusCrystals;
                    Speak($"All crystals collected! Sacred {FormatName(CurrentPattern)} pattern completed. {FormatName(pInfo.Bonus)} bonus activated. {bonusCrystals} bonus crystals. Press U for attunement.");
                }
                else
                {
                    Speak("All crystals collected. Press U for attunement menu.");
                }
            }

            if (CrystalsCollected >= GameConstants.AscensionCrystalThreshold)
                Ascend();
            _approachingLockAnnounced = false;
        }
        else
        {
            Speak("Resonance too low to collect. Tune to crystal frequencies.");
        }
    }

    public void ApplyAtlanteanCrystalEffect(string crystalType)
    {
        if (!GameConstants.AtlanteanCrystalTypes.TryGetValue(crystalType, out var info)) return;
        string effect = info.Effect;

        switch (effect)
        {
            case "velocity_burst":
                MaxVelocity *= 1.5f;
                Speak("Fire crystal energy surges through your light vehicle!");
                break;
            case "shield_boost":
                ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + 0.2f);
                Speak("Aquamarine protective field strengthens your hull.");
                break;
            case "communication":
                Speak("Larimar stone awakens ancient wisdom within you.");
                break;
            case "transformation":
                ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + 0.1f);
                Speak("Moldavite accelerates your spiritual evolution!");
                break;
            case "memory_unlock":
                Speak("Lemurian seed crystal shares memories of forgotten ages.");
                break;
            case "purification":
                DissonanceTimer = 0f;
                Speak("Black tourmaline cleanses all dissonance from your field.");
                break;
            case "angelic_connection":
                Speak("Celestite opens channels to higher dimensional beings.");
                break;
        }
    }
}
