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
    #region Proximity ambient audio helpers

    /// <summary>
    /// Drive the looping ambient soundscape for a nearby celestial body. Picks a type-specific
    /// waveform (red giant pulse, nebula drone, ocean-world flow, etc.) and fades its volume with
    /// distance, so flying past objects produces a living, positional soundscape.
    /// </summary>
    /// <param name="body">The celestial body being approached.</param>
    /// <param name="dist">Distance from the ship to the body, in world units.</param>
    /// <param name="pan">Stereo pan toward the body (-1 left .. +1 right).</param>
    private void HandleProximityAmbient(CelestialBody body, float dist, float pan)
    {
        if (body.BodyType == CelestialBodyType.Star && dist < GameConstants.StarHarmonyRadius)
        {
            var sType = body.StellarClass ?? StellarType.MainSequence;
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.StarHarmonyRadius) * 0.3f;
            float[]? waveform = sType switch
            {
                StellarType.RedGiant => _audio.RedGiantPulse,
                StellarType.WhiteDwarf => _audio.WhiteDwarfWhine,
                StellarType.BrownDwarf => _audio.BrownDwarfRumble,
                _ => null
            };
            UpdateAmbientSound(ref _starSound, waveform, pan, volume);
        }
        else if (body.BodyType == CelestialBodyType.Nebula && dist < GameConstants.NebulaDissonanceRadius)
        {
            var nType = body.NebulaClass ?? NebulaType.Emission;
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.NebulaDissonanceRadius) * 0.4f;
            float[]? waveform = nType switch
            {
                NebulaType.Emission => _audio.EmissionDrone,
                NebulaType.Reflection => _audio.ReflectionShimmer,
                NebulaType.Planetary => _audio.PlanetaryLayers,
                NebulaType.SupernovaRemnant => _audio.SupernovaChaos,
                _ => null
            };
            UpdateAmbientSound(ref _nebulaSound, waveform, pan, volume);
        }
        else if (body.BodyType == CelestialBodyType.Planet && dist < GameConstants.InteractionDistance)
        {
            var eType = body.ExoplanetClass ?? ExoplanetType.SuperEarth;
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.InteractionDistance) * 0.3f;
            float[]? waveform = eType switch
            {
                ExoplanetType.HotJupiter => _audio.HotJupiterRoar,
                ExoplanetType.SuperEarth => _audio.SuperEarthTone,
                ExoplanetType.OceanWorld => _audio.OceanWorldFlow,
                ExoplanetType.RoguePlanet => _audio.RogueOminous,
                ExoplanetType.IceGiant => _audio.IceChime,
                _ => null
            };
            UpdateAmbientSound(ref _planetSound, waveform, pan, volume);
        }
    }

    /// <summary>
    /// Reconcile a single looping ambient slot with the desired waveform. If the slot already plays
    /// that waveform, just refresh its pan/volume; if it plays a different one, stop the old loop and
    /// start the new one. A null waveform leaves the slot untouched.
    /// </summary>
    private void UpdateAmbientSound(ref GameSoundEffect? current, float[]? waveform, float pan, float volume)
    {
        if (waveform == null) return;

        if (current != null)
        {
            // If same waveform, just update properties
            if (ReferenceEquals(current.Waveform, waveform))
            {
                current.Pan = pan;
                current.Volume = volume;
                return;
            }
            // Different type - stop old
            current.Loop = false;
            current.Volume = 0;
            current = null;
        }

        current = new GameSoundEffect(waveform, pan: pan, loop: true, volume: volume);
        _audio.AddSoundEffect(current);
    }

    /// <summary>Stop every looping proximity ambient (star, nebula, planet) — e.g. when none are in range.</summary>
    private void StopAllAmbientSounds()
    {
        StopAmbient(ref _starSound);
        StopAmbient(ref _nebulaSound);
        StopAmbient(ref _planetSound);
    }

    /// <summary>Stop one ambient slot by ending its loop and silencing it, then clear the reference.</summary>
    private static void StopAmbient(ref GameSoundEffect? sfx)
    {
        if (sfx == null) return;
        sfx.Loop = false;
        sfx.Volume = 0;
        sfx = null;
    }

    #endregion
}
