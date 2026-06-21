using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// Loads and persists <see cref="GameSettings"/> to <c>settings.json</c>. Reads are synchronous
/// (one-time, at startup); writes serialize on the caller's thread (tiny — a handful of primitives)
/// then do the disk I/O on a background task with an atomic temp-file swap, so the realtime loop is
/// never stalled and a crash mid-write can't corrupt the file.
/// </summary>
public static class SettingsStore
{
    #region Constants

    private const string SettingsFile = "settings.json";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly object _fileLock = new();

    // Monotonic ordering for writes: each enqueued write gets an increasing sequence number, and a
    // write only lands if it is newer than the last one that landed. This keeps a stale in-flight
    // async save from overwriting a newer one (e.g. the synchronous save-on-exit), regardless of the
    // order the background tasks happen to run in.
    private static long _writeSeq;
    private static long _lastWrittenSeq;

    #endregion

    #region Load

    /// <summary>Load preferences from disk, or return defaults if the file is missing/unreadable.</summary>
    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<GameSettings>(json);
                if (settings != null)
                {
                    DebugLogger.Log("Settings", $"Loaded preferences from {SettingsFile}.");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Settings", "Failed to load settings; using defaults", ex);
        }
        return new GameSettings();
    }

    #endregion

    #region Save

    /// <summary>
    /// Persist preferences without blocking the caller. The snapshot is serialized to a string on
    /// the calling thread (cheap), then the immutable string is written on a background task.
    /// </summary>
    public static void Save(GameSettings settings)
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(settings, Options);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Settings", "Failed to serialize settings", ex);
            return;
        }

        long seq = Interlocked.Increment(ref _writeSeq);
        Task.Run(() => WriteAtomic(json, seq));
    }

    /// <summary>
    /// Persist preferences synchronously — used on shutdown so the final state is on disk before the
    /// process exits.
    /// </summary>
    public static void SaveBlocking(GameSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, Options);
            long seq = Interlocked.Increment(ref _writeSeq);
            WriteAtomic(json, seq);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Settings", "Failed to save settings on shutdown", ex);
        }
    }

    /// <summary>
    /// Write the JSON to a temp file then atomically swap it in, serialized by a lock. Skips the
    /// write if a newer one (higher <paramref name="seq"/>) has already landed.
    /// </summary>
    private static void WriteAtomic(string json, long seq)
    {
        try
        {
            lock (_fileLock)
            {
                if (seq < _lastWrittenSeq) return; // a newer save already landed — don't regress it
                string tmp = SettingsFile + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, SettingsFile, overwrite: true);
                _lastWrittenSeq = seq;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Settings", "Failed to write settings file", ex);
        }
    }

    #endregion
}
