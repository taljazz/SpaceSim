using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;

namespace SpaceSim;

public partial class Ship
{
    #region Save / load

    // Shared by the async write path: a single options instance and a lock so two overlapping
    // saves (autosave + a manual save) can never interleave and corrupt the file.
    private const string SaveFile = "savegame.json";
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
    private static readonly object _saveFileLock = new();

    /// <summary>
    /// Persist the player's progress (position, drive state, crystals, upgrades, consciousness,
    /// temple keys, portal anchors, and accessibility settings) to <c>savegame.json</c>.
    ///
    /// <para>
    /// The snapshot — with all mutable arrays/collections cloned — is built synchronously on the
    /// game thread (fast), but the JSON serialization and disk write happen on a background task so
    /// the 60 Hz realtime loop never stalls on I/O. Because the snapshot is a frozen copy, the
    /// background task touches no live game state.
    /// </para>
    /// </summary>
    public void SaveGame()
    {
        SaveGameState state;
        try
        {
            state = BuildSaveState();
        }
        catch (Exception ex)
        {
            Speak($"Save failed: {ex.Message}");
            DebugLogger.LogError("Save", "SaveGame snapshot failed", ex);
            return;
        }

        // Speak now (on the game thread) — Speak just enqueues, so it stays non-blocking.
        Speak("Game saved.");

        // Off-thread write. WriteSaveFile is static and takes ONLY the frozen snapshot, so it is
        // structurally impossible for the background task to touch live game state (e.g. Speak's
        // cooldown dictionary or the live arrays) — no data race can be introduced here later.
        Task.Run(() => WriteSaveFile(state));
    }

    /// <summary>
    /// Serialize a frozen save snapshot and write it to disk. Runs on a background task (never the
    /// game thread). The write is atomic — temp file then swap — so a crash mid-write can't leave a
    /// truncated/corrupt <c>savegame.json</c>, and the lock serializes any overlapping saves.
    /// </summary>
    private static void WriteSaveFile(SaveGameState state)
    {
        try
        {
            string json = JsonSerializer.Serialize(state, SaveOptions);
            lock (_saveFileLock)
            {
                string tmp = SaveFile + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, SaveFile, overwrite: true);
            }
            DebugLogger.Log("Save", $"Game saved to {SaveFile} ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Save", "Async save write failed", ex);
        }
    }

    /// <summary>Build a frozen snapshot of everything worth persisting; arrays/dicts/lists are cloned.</summary>
    private SaveGameState BuildSaveState() => new SaveGameState
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

            // Start fresh: stop and clear any rifts, lock, and world sounds from the previous session
            // so they don't carry stale audio into the loaded universe (mirrors Ascend).
            SilenceAllWorldSounds();
            Rifts.Clear();
            LockedTarget = null;
            LockedRift = null;
            LockedIsRift = false;

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
