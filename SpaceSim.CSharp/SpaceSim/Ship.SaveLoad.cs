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
    #region Save / load

    /// <summary>
    /// Serialize the player's progress (position, drive state, crystals, upgrades, consciousness,
    /// temple keys, portal anchors, and accessibility settings) to <c>savegame.json</c>. Mutable
    /// arrays and collections are cloned so the snapshot can't be altered by later play.
    /// </summary>
    public void SaveGame()
    {
        try
        {
            // Snapshot everything worth persisting; clone arrays/dicts so the save is a frozen copy.
            var state = new SaveGameState
            {
                Position = Vec5.Clone(Position),
                Velocity = Vec5.Clone(Velocity),
                RDrive = (float[])RDrive.Clone(),
                BaseFTarget = (float[])BaseFTarget.Clone(),
                ResonanceIntegrity = ResonanceIntegrity,
                CrystalsCollected = CrystalsCollected,
                ResonanceWidth = ResonanceWidth,
                MaxVelocity = MaxVelocity,
                CrystalBonus = CrystalBonus,
                GoldenHarmonyActive = GoldenHarmonyActive,
                FrequencyPresets = FrequencyPresets.ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone()),
                TuaoiMode = TuaoiMode,
                TuaoiModeIndex = TuaoiModeIndex,
                ConsciousnessValue = ConsciousnessValue,
                ConsciousnessStage = ConsciousnessStage,
                TempleKeys = TempleKeys.ToList(),
                VisitedAmenti = VisitedAmenti,
                AmentiBlessingActive = AmentiBlessingActive,
                PortalAnchors = PortalAnchors.Select(a => new PortalAnchorState
                {
                    Position = Vec5.Clone(a.Position),
                    Name = a.Name
                }).ToList(),
                VerboseMode = VerboseMode,
                HudTextSize = HudTextSize,
                HighContrast = HighContrast,
                AutosaveEnabled = AutosaveEnabled,
                AmbientSoundsEnabled = AmbientSoundsEnabled,
                NebulaDissonanceEnabled = NebulaDissonanceEnabled,
            };

            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("savegame.json", json);
            Speak("Game saved.");
            DebugLogger.Log("Save", $"Game saved to savegame.json ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            Speak($"Save failed: {ex.Message}");
            DebugLogger.LogError("Save", "SaveGame failed", ex);
        }
    }

    /// <summary>
    /// Restore progress from <c>savegame.json</c> back onto this ship, then flag the universe for
    /// regeneration so the world matches the loaded position. Missing or corrupt saves are reported
    /// to the player rather than throwing.
    /// </summary>
    public void LoadGame()
    {
        try
        {
            if (!File.Exists("savegame.json"))
            {
                Speak("No save file found.");
                return;
            }
            string json = File.ReadAllText("savegame.json");
            var state = JsonSerializer.Deserialize<SaveGameState>(json);
            if (state == null) { Speak("Save file corrupted."); return; }

            // Copy saved values back onto the live ship; Array.Copy fills the existing N-length buffers.
            Array.Copy(state.Position, Position, N);
            Array.Copy(state.Velocity, Velocity, N);
            Array.Copy(state.RDrive, RDrive, N);
            Array.Copy(state.BaseFTarget, BaseFTarget, N);
            ResonanceIntegrity = state.ResonanceIntegrity;
            CrystalsCollected = state.CrystalsCollected;
            ResonanceWidth = state.ResonanceWidth;
            MaxVelocity = state.MaxVelocity;
            CrystalBonus = state.CrystalBonus;
            GoldenHarmonyActive = state.GoldenHarmonyActive;
            FrequencyPresets = state.FrequencyPresets ?? new();
            SetTuaoiMode(state.TuaoiMode);
            TuaoiModeIndex = state.TuaoiModeIndex;
            ConsciousnessValue = state.ConsciousnessValue;
            ConsciousnessStage = state.ConsciousnessStage;
            TempleKeys = new HashSet<int>(state.TempleKeys);
            VisitedAmenti = state.VisitedAmenti;
            AmentiBlessingActive = state.AmentiBlessingActive;
            PortalAnchors = state.PortalAnchors.Select(a => new PortalAnchor
            {
                Position = Vec5.Clone(a.Position),
                Name = a.Name
            }).ToList();
            VerboseMode = state.VerboseMode;
            HudTextSize = state.HudTextSize;
            HighContrast = state.HighContrast;
            AutosaveEnabled = state.AutosaveEnabled;
            AmbientSoundsEnabled = state.AmbientSoundsEnabled;
            NebulaDissonanceEnabled = state.NebulaDissonanceEnabled;

            // Rebuild celestial bodies around the restored position on the next update.
            NeedsUniverseRegeneration = true;
            Speak("Game loaded.");
            DebugLogger.Log("Save", $"Game loaded: pos={Vec5.Format(Position)}, crystals={CrystalsCollected}, consciousness={ConsciousnessStage}");
        }
        catch (Exception ex)
        {
            Speak("No save file found.");
            DebugLogger.LogError("Save", "LoadGame failed", ex);
        }
    }

    #endregion
}
