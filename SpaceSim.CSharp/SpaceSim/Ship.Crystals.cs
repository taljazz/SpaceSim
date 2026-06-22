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
    #region Crystal generation (on landed planet)

    /// <summary>
    /// Seed a freshly-anchored planet with collectable crystals. Picks a biome, rolls a crystal
    /// count (boosted by upgrades and the planet's exoplanet multiplier), arranges them in a sacred
    /// geometry layout when the count matches a known pattern, and starts the looping biome ambience.
    /// </summary>
    public void GenerateCrystals()
    {
        CrystalPositions.Clear();
        CrystalFreqs.Clear();
        LockedCrystals.Clear();
        Biome = Random.Shared.NextDouble() < 0.5 ? PlanetBiome.Harmonic : PlanetBiome.Dissonant;
        PatternProgress.Clear();

        // Rarer planet types skew their crystal yield via CrystalMult (e.g. rogue planets give more).
        float crystalMult = 1f;
        if (LandedPlanetBody != null)
            crystalMult = LandedPlanetBody.CrystalMult;

        // Roll a base count (Crystal Growth upgrades raise both bounds), then scale by the planet's multiplier.
        int baseCount = Random.Shared.Next(1 + CrystalBonus, 9 + CrystalBonus);
        CrystalCount = Math.Max(1, (int)(baseCount * crystalMult));

        // Detect sacred geometry pattern: if the count exactly matches a pattern's point total, use it.
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
            patternMsg = $" Sacred {GameUtils.SpacePascalCase(CurrentPattern.Value.ToString())} pattern detected!";
        Speak($"Anchored on {Biome} biome planet. {Capitalize(exoDesc)}. {CrystalCount} Atlantean crystals detected.{patternMsg}");
        DebugLogger.Log("Ship", $"GenerateCrystals: {CrystalCount} crystals, biome={Biome}, pattern={CurrentPattern?.ToString() ?? "none"}, mult={crystalMult:F1}");

        float scaleFactor = GameConstants.ScaleFactor;

        // Pick the crystal layout once (polymorphic — see CrystalPattern), then place each crystal.
        CrystalPattern layout = CrystalPatterns.Select(CurrentPattern, CrystalCount);

        for (int i = 0; i < CrystalCount; i++)
        {
            var (px, py) = layout.PositionFor(i, CrystalCount, scaleFactor);

            CrystalPositions.Add(new[] { px, py });

            // Small chance each crystal is a special Atlantean type (its own frequency band + effect);
            // otherwise it gets ordinary frequencies spread across the full range.
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
        Speak($"Crystals detected at frequencies: {freqStr} Hz in the primary Realm.");
        _approachingLockAnnounced = false;

        // Start the looping biome ambience: a golden chord for harmonic worlds, chaos for dissonant ones.
        StopBiomeSound();
        float[] waveform = Biome == PlanetBiome.Harmonic
            ? _audio.GoldenChordWaveform
            : _audio.SupernovaChaos; // dissonant waveform
        _biomeSound = new GameSoundEffect(waveform, loop: true, volume: _audio.EffectVolume * 0.5f);
        _audio.AddSoundEffect(_biomeSound);
    }

    /// <summary>Silence the looping biome ambience started by <see cref="GenerateCrystals"/> (e.g. on takeoff).</summary>
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

    #endregion

    #region Scan / collect crystal

    /// <summary>
    /// Announce the nearest un-collected crystal: its distance, compass direction, and target
    /// frequency for the selected dimension. Auto-snaps the drive onto the crystal's frequencies when
    /// the player is already close enough, and pans a beep toward it as an audio cue.
    /// </summary>
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

        // Auto-snap: if the drive is already resonating closely across all dims, lock it exactly
        // onto the crystal's frequencies so the player doesn't have to fine-tune the last sliver.
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
            specialMsg = $" Rare {FormatName(crystal.AtlanteanType)} crystal!";
        Speak($"Nearest crystal {dists[nearest]:F1} units {dir}. Target frequency in Realm {SelectedDim + 1}: {freq:F2} Hz.{specialMsg}");

        float angle = MathF.Atan2(dy, dx);
        float pan = MathF.Cos(angle);
        GameEvents.RaisePlaySound(this, _audio.BeepWaveform, pan: pan, volume: _audio.BeepVolume);
    }

    /// <summary>
    /// Attempt to collect the crystal under the cursor. Succeeds only when the drive is in tune
    /// (mean resonance above the collection threshold) and the crystal is within one grid unit.
    /// Tallies its value, applies any special effect, and — once the last crystal is gathered —
    /// awards the sacred-pattern completion bonus and checks for ascension.
    /// </summary>
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

            // Build the whole collection announcement as ONE utterance — with interrupt-by-default,
            // separate Speak calls here would each cut off the previous, so the player would only hear
            // the last line. We assemble base + crystal effect + ancient echo + pattern completion.
            int crystalValue = 1;
            string msg;
            if (crystal.Special && crystal.AtlanteanType != null &&
                GameConstants.AtlanteanCrystalTypes.ContainsKey(crystal.AtlanteanType))
            {
                var cInfo = GameConstants.AtlanteanCrystalTypes[crystal.AtlanteanType];
                crystalValue = (int)cInfo.Mult;
                msg = $"Rare {FormatName(crystal.AtlanteanType)} crystal collected! {cInfo.Desc}. Value: {crystalValue} crystals.";
                string effect = ApplyAtlanteanCrystalEffect(crystal.AtlanteanType);
                if (effect.Length > 0) msg += " " + effect;
            }
            else
            {
                var (typeName, typeInfo) = GetCrystalType(cFreqs.Average());
                msg = $"{Capitalize(typeName)} crystal collected. {Capitalize(typeInfo.Chakra)} chakra resonance. Harmony increases.";
                // "Harmony increases" made literal: each chakra crystal restores a little integrity, scaled by its spectrum multiplier.
                ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + GameConstants.ChakraHealAmount * typeInfo.Mult);
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
                msg += " Ancient echo: the spiral binds all realms in golden eternity.";

            // Sacred geometry pattern completion: gathering every crystal of a recognised layout
            // grants a timed bonus plus extra crystals scaled by the pattern's multiplier.
            if (LockedCrystals.Count == CrystalCount)
            {
                if (CurrentPattern != null && GameConstants.SacredPatterns.TryGetValue(CurrentPattern.Value, out var pInfo))
                {
                    PatternBonusTimer = 30f;
                    int bonusCrystals = (int)(CrystalCount * (pInfo.Mult - 1f));
                    if (bonusCrystals > 0)
                        CrystalsCollected += bonusCrystals;
                    msg += $" All this planet's crystals are gathered. Sacred {GameUtils.SpacePascalCase(CurrentPattern.Value.ToString())} pattern completed, {FormatName(pInfo.Bonus)} bonus, {bonusCrystals} bonus crystals. Press U for attunement.";
                }
                else
                {
                    msg += " All this planet's crystals are gathered. Press U for the attunement menu.";
                }
            }

            Speak(msg);

            if (CrystalsCollected >= GameConstants.AscensionCrystalThreshold)
                Ascend();
            _approachingLockAnnounced = false;
        }
        else
        {
            Speak("Resonance too low to collect. Tune to crystal frequencies.");
        }
    }

    #endregion

    #region Atlantean crystal effects

    /// <summary>
    /// Apply the one-off effect of a collected special Atlantean crystal (velocity burst, shield
    /// boost, consciousness gain, dissonance purge, etc.), announcing the corresponding lore line.
    /// </summary>
    public string ApplyAtlanteanCrystalEffect(string crystalType)
    {
        if (!GameConstants.AtlanteanCrystalTypes.TryGetValue(crystalType, out var info)) return "";

        switch (info.Effect)
        {
            case CrystalEffect.VelocityBurst:
                MaxVelocity *= 1.5f;
                return "Fire crystal energy surges through your light vehicle!";
            case CrystalEffect.ShieldBoost:
                ResonanceIntegrity = MathF.Min(1f, ResonanceIntegrity + 0.2f);
                return "Aquamarine protective field strengthens your hull.";
            case CrystalEffect.Communication:
                return "Larimar stone awakens ancient wisdom within you.";
            case CrystalEffect.Transformation:
                ConsciousnessValue = MathF.Min(1f, ConsciousnessValue + 0.1f);
                return "Moldavite accelerates your spiritual evolution!";
            case CrystalEffect.MemoryUnlock:
                return "Lemurian seed crystal shares memories of forgotten ages.";
            case CrystalEffect.Purification:
                DissonanceTimer = 0f;
                return "Black tourmaline cleanses all dissonance from your field.";
            case CrystalEffect.AngelicConnection:
                return "Celestite opens channels to higher dimensional beings.";
            default:
                return "";
        }
    }

    #endregion
}
