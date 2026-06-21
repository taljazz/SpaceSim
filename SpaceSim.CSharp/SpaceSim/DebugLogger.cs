using System;
using System.Diagnostics;
using System.IO;

namespace SpaceSim;

/// <summary>
/// Centralized debug logger that writes timestamped messages to a log file.
/// Thread-safe for use from audio and game threads.
/// </summary>
public static class DebugLogger
{
    /// <summary>Master switch — set false (or auto-disabled on file error) to silence all logging.</summary>
    public static bool IsEnabled = true;

    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private static float _lastFlushTime;

    #region Lifecycle

    /// <summary>
    /// Opens the log file. Call once at startup.
    /// </summary>
    public static void Initialize(string path = "spacesim_debug.log")
    {
        try
        {
            _writer = new StreamWriter(path, append: false) { AutoFlush = false };
            _writer.WriteLine($"=== SpaceSim Debug Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer.Flush();
        }
        catch
        {
            IsEnabled = false;
        }
    }

    #endregion

    #region Writing

    /// <summary>
    /// Log a message with category and timestamp.
    /// </summary>
    public static void Log(string category, string message)
    {
        if (!IsEnabled || _writer == null) return;
        float elapsed = (float)_stopwatch.Elapsed.TotalSeconds;
        string line = $"[{elapsed:F3}] [{category}] {message}";
        lock (_lock)
        {
            try
            {
                _writer.WriteLine(line);
                // Flush periodically (every 2 seconds) to avoid constant disk I/O
                if (elapsed - _lastFlushTime > 2f)
                {
                    _writer.Flush();
                    _lastFlushTime = elapsed;
                }
            }
            catch { /* Don't crash the game over logging */ }
        }
    }

    /// <summary>
    /// Log an exception with full stack trace.
    /// </summary>
    public static void LogError(string category, string message, Exception ex)
    {
        Log(category, $"ERROR: {message} - {ex.GetType().Name}: {ex.Message}");
        Log(category, $"  StackTrace: {ex.StackTrace}");
    }

    #endregion

    #region Flushing & shutdown

    /// <summary>
    /// Force flush all buffered log data to disk.
    /// </summary>
    public static void Flush()
    {
        if (_writer == null) return;
        lock (_lock)
        {
            try { _writer.Flush(); }
            catch { }
        }
    }

    /// <summary>
    /// Close the log file. Call at shutdown.
    /// </summary>
    public static void Shutdown()
    {
        if (_writer == null) return;
        lock (_lock)
        {
            try
            {
                _writer.WriteLine($"=== Shutdown at {_stopwatch.Elapsed.TotalSeconds:F3}s ===");
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
            catch { }
        }
    }

    #endregion
}
