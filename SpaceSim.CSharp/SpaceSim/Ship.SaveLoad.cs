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
            SpeakSystem($"Save failed: {ex.Message}");
            DebugLogger.LogError("Save", "SaveGame snapshot failed", ex);
            return;
        }

        // Speak now (on the game thread) — Speak just enqueues, so it stays non-blocking.
        SpeakSystem("Game saved.");

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
            // The optimistic "Game saved." was already spoken; a failed disk write would otherwise be silent.
            // Raise speech directly (thread-safe Tolk queue) rather than via Ship.Speak, since this runs on a
            // background thread and must not touch the game-thread speech cooldown/buffers.
            GameEvents.RaiseSpeak(null, "Warning: saving to disk failed.");
        }
    }

    /// <summary>Build a frozen snapshot of everything worth persisting; arrays/dicts/lists are cloned.</summary>
    private SaveGameState BuildSaveState() => new SaveGameState
    {
        // If projecting astrally, persist the BODY's position (not the roaming form), so a mid-astral autosave
        // doesn't silently relocate the player to wherever the projection happened to be on next load.
        Position = (AstralMode && AstralBodyPos != null) ? Vec5.Clone(AstralBodyPos) : Vec5.Clone(Position),
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
        SaveGameState? state;
        try
        {
            if (!File.Exists(SaveFile))
            {
                SpeakSystem("No save file found.");
                return;
            }
            string json = File.ReadAllText(SaveFile);
            state = JsonSerializer.Deserialize<SaveGameState>(json);
        }
        catch (FileNotFoundException)
        {
            SpeakSystem("No save file found.");
            return;
        }
        catch (Exception ex)
        {
            // Unreadable / unparseable / unknown-enum file is CORRUPTION, not a missing save. Say so —
            // speech is the player's only feedback channel, and "no save file" here would be a lie.
            SpeakSystem("Save file corrupted.");
            DebugLogger.LogError("Save", "LoadGame read/parse failed", ex);
            return;
        }

        // Validate the WHOLE snapshot before touching the live ship, so a corrupt save can never leave a
        // half-loaded craft or feed a NaN/out-of-range frequency into the real-time synth.
        if (state == null || !IsValidSaveState(state))
        {
            SpeakSystem("Save file corrupted.");
            DebugLogger.Log("Save", "LoadGame: save data failed validation");
            return;
        }

        // Validated — commit onto the live ship. Array.Copy fills the existing N-length buffers in place.
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
        // Keep only well-formed presets (length N) so a later Shift+1-9 recall can't crash on a malformed slot.
        FrequencyPresets = state.FrequencyPresets?
            .Where(kv => kv.Value != null && kv.Value.Length == N)
            .ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone()) ?? new();
        SetTuaoiMode(state.TuaoiMode);
        // Derive the cycle index from the (enum-validated) mode rather than trusting the saved index, so a
        // corrupt/out-of-range index can't crash the next G press (TuaoiModeOrder[index]).
        int tmIdx = Array.IndexOf(GameConstants.TuaoiModeOrder, state.TuaoiMode);
        TuaoiModeIndex = tmIdx >= 0 ? tmIdx : 0;
        ConsciousnessValue = state.ConsciousnessValue;
        ConsciousnessStage = state.ConsciousnessStage;
        TempleKeys = new HashSet<int>(state.TempleKeys);
        VisitedAmenti = state.VisitedAmenti;
        AmentiBlessingActive = state.AmentiBlessingActive;
        PortalAnchors = state.PortalAnchors
            .Where(a => a?.Position != null && a.Position.Length == N)
            .Select(a => new PortalAnchor { Position = Vec5.Clone(a.Position), Name = a.Name ?? "Anchor" })
            .ToList();
        VerboseMode = state.VerboseMode;
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
        SpeakSystem("Game loaded.");
        DebugLogger.Log("Save", $"Game loaded: pos={Vec5.Format(Position)}, crystals={CrystalsCollected}, consciousness={ConsciousnessStage}");
    }

    /// <summary>
    /// Validate a deserialized save BEFORE any of it is copied onto the live ship: arrays must be the right
    /// length and finite, and the drive frequencies must be in range. Guarantees a corrupt or hand-edited
    /// save is rejected cleanly instead of half-loading or poisoning the audio synthesis with a NaN.
    /// </summary>
    private static bool IsValidSaveState(SaveGameState s)
    {
        if (!IsFiniteVec(s.Position) || !IsFiniteVec(s.Velocity) || !IsFiniteVec(s.RDrive) || !IsFiniteVec(s.BaseFTarget))
            return false;
        if (s.TempleKeys == null || s.PortalAnchors == null) return false;
        for (int i = 0; i < N; i++)
        {
            if (s.RDrive[i] < GameConstants.FrequencyMin || s.RDrive[i] > GameConstants.FrequencyMax) return false;
            if (s.BaseFTarget[i] < GameConstants.FrequencyMin || s.BaseFTarget[i] > GameConstants.FrequencyMax) return false;
        }
        return float.IsFinite(s.ResonanceIntegrity) && float.IsFinite(s.ResonanceWidth)
            && float.IsFinite(s.MaxVelocity) && float.IsFinite(s.ConsciousnessValue);
    }

    /// <summary>True if <paramref name="a"/> is non-null, exactly N elements, and every element is finite.</summary>
    private static bool IsFiniteVec(float[] a)
    {
        if (a == null || a.Length != N) return false;
        foreach (float v in a) if (!float.IsFinite(v)) return false;
        return true;
    }

    #endregion
}
