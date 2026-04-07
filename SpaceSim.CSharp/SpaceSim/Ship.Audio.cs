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
    //  PROXIMITY AMBIENT AUDIO HELPERS
    // =========================================================================

    private void HandleProximityAmbient(CelestialBody body, float dist, float pan)
    {
        if (body.Type == "star" && dist < GameConstants.StarHarmonyRadius)
        {
            string sType = body.StellarType ?? "main_sequence";
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.StarHarmonyRadius) * 0.3f;
            float[]? waveform = sType switch
            {
                "red_giant" => _audio.RedGiantPulse,
                "white_dwarf" => _audio.WhiteDwarfWhine,
                "brown_dwarf" => _audio.BrownDwarfRumble,
                _ => null
            };
            UpdateAmbientSound(ref _starSound, waveform, pan, volume);
        }
        else if (body.Type == "nebula" && dist < GameConstants.NebulaDissonanceRadius)
        {
            string nType = body.NebulaType ?? "emission";
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.NebulaDissonanceRadius) * 0.4f;
            float[]? waveform = nType switch
            {
                "emission" => _audio.EmissionDrone,
                "reflection" => _audio.ReflectionShimmer,
                "planetary" => _audio.PlanetaryLayers,
                "supernova_remnant" => _audio.SupernovaChaos,
                _ => null
            };
            UpdateAmbientSound(ref _nebulaSound, waveform, pan, volume);
        }
        else if (body.Type == "planet" && dist < GameConstants.InteractionDistance)
        {
            string eType = body.ExoplanetType ?? "super_earth";
            float volume = _audio.EffectVolume * (1f - dist / GameConstants.InteractionDistance) * 0.3f;
            float[]? waveform = eType switch
            {
                "hot_jupiter" => _audio.HotJupiterRoar,
                "super_earth" => _audio.SuperEarthTone,
                "ocean_world" => _audio.OceanWorldFlow,
                "rogue_planet" => _audio.RogueOminous,
                "ice_giant" => _audio.IceChime,
                _ => null
            };
            UpdateAmbientSound(ref _planetSound, waveform, pan, volume);
        }
    }

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

    private void StopAllAmbientSounds()
    {
        StopAmbient(ref _starSound);
        StopAmbient(ref _nebulaSound);
        StopAmbient(ref _planetSound);
    }

    private static void StopAmbient(ref GameSoundEffect? sfx)
    {
        if (sfx == null) return;
        sfx.Loop = false;
        sfx.Volume = 0;
        sfx = null;
    }
}
