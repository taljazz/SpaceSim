using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpaceSim;

/// <summary>
/// Raw P/Invoke bindings to Tolk.dll, the screen-reader abstraction library. Tolk talks to whatever
/// reader is running (NVDA, JAWS, SAPI fallback). Use <see cref="TolkSpeechService"/> rather than
/// calling these directly.
/// </summary>
internal static class TolkNative
{
    private const string DllName = "Tolk.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Tolk_Load();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Tolk_Unload();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Tolk_TrySAPI(bool useSAPI);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern bool Tolk_Output(string str, bool interrupt);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool Tolk_Silence();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool Tolk_IsLoaded();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Tolk_DetectScreenReader();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool Tolk_HasSpeech();
}

/// <summary>
/// Friendly, <b>non-blocking</b> wrapper over <see cref="TolkNative"/>.
///
/// <para>
/// The screen-reader P/Invoke (<c>Tolk_Output</c>) is synchronous and can take many milliseconds —
/// far too long to sit in a 60 Hz realtime loop, especially in an audio-first game that announces
/// constantly. So ALL Tolk calls (load, output, silence, unload) run on a dedicated background
/// thread, and <see cref="Speak"/> merely drops a message onto a bounded, lock-free queue and
/// returns immediately. The game thread never blocks on speech.
/// </para>
///
/// <para>
/// The queue is bounded: if a slow reader can't keep up, the newest messages are dropped rather than
/// building an ever-growing backlog of stale announcements. If no reader is available, speech falls
/// back to the console so the game stays usable everywhere.
/// </para>
/// </summary>
public sealed class TolkSpeechService : IDisposable
{
    #region Command queue plumbing

    private enum CommandKind { Speak, Silence }
    private readonly record struct SpeechCommand(CommandKind Kind, string Text, bool Interrupt);

    // Bounded so a slow screen reader can never back up the queue or block the game thread.
    private readonly BlockingCollection<SpeechCommand> _queue = new(boundedCapacity: 64);
    private readonly Thread _worker;
    private readonly ManualResetEventSlim _ready = new(false);

    // Written by the worker thread (after Tolk loads), read by the game thread — hence volatile.
    private volatile bool _isActive;
    private bool _disposed;

    #endregion

    #region Construction

    /// <summary>
    /// Starts the speech worker, which loads Tolk (with SAPI fallback) and then services the queue.
    /// Briefly waits for the load so <see cref="IsScreenReaderActive"/> is meaningful right away.
    /// </summary>
    public TolkSpeechService()
    {
        _worker = new Thread(SpeechLoop) { IsBackground = true, Name = "TolkSpeech" };
        _worker.Start();
        _ready.Wait(2000); // one-time startup wait (not in the realtime loop); Tolk loads in well under this
    }

    /// <summary>True if a real screen reader is driving output (false means console fallback).</summary>
    public bool IsScreenReaderActive => _isActive;

    #endregion

    #region Public API (non-blocking — game thread safe)

    /// <summary>
    /// Queue <paramref name="text"/> to be spoken on the background thread. Returns immediately;
    /// <paramref name="interrupt"/> cuts off any current speech. Dropped silently if the queue is full.
    /// </summary>
    public void Speak(string text, bool interrupt = false)
    {
        if (_queue.IsAddingCompleted) return;

        // An interrupting message supersedes anything still waiting, so drop the stale backlog first.
        // This keeps rapid menu navigation snappy: the worker speaks the latest item right away
        // instead of grinding through a queue of already-stale announcements.
        if (interrupt)
            while (_queue.TryTake(out _)) { }

        _queue.TryAdd(new SpeechCommand(CommandKind.Speak, text, interrupt));
    }

    /// <summary>Queue a request to stop whatever is currently being read out.</summary>
    public void Silence()
    {
        if (!_queue.IsAddingCompleted)
            _queue.TryAdd(new SpeechCommand(CommandKind.Silence, string.Empty, false));
    }

    #endregion

    #region Worker thread

    /// <summary>
    /// The speech thread: loads Tolk, then drains the queue, performing the (potentially slow)
    /// synchronous Tolk calls here so they never touch the game loop. Keeping load/output/unload all
    /// on this one thread also sidesteps any cross-thread/COM concerns in the underlying readers.
    /// </summary>
    private void SpeechLoop()
    {
        try
        {
            TolkNative.Tolk_TrySAPI(true);
            TolkNative.Tolk_Load();
            _isActive = TolkNative.Tolk_IsLoaded() && TolkNative.Tolk_HasSpeech();
        }
        catch
        {
            _isActive = false;
        }
        finally
        {
            _ready.Set(); // unblock the constructor regardless of how loading went
        }

        foreach (var cmd in _queue.GetConsumingEnumerable())
        {
            try
            {
                if (!_isActive)
                {
                    if (cmd.Kind == CommandKind.Speak)
                        Console.WriteLine(cmd.Text);
                    continue;
                }

                if (cmd.Kind == CommandKind.Speak)
                    TolkNative.Tolk_Output(cmd.Text, cmd.Interrupt);
                else
                    TolkNative.Tolk_Silence();
            }
            catch
            {
                // A single failed utterance must never kill the worker.
            }
        }

        if (_isActive)
        {
            try { TolkNative.Tolk_Unload(); } catch { /* ignore unload errors */ }
        }
    }

    #endregion

    #region Disposal

    /// <summary>Stops accepting messages, lets the worker drain and unload Tolk, then cleans up.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _queue.CompleteAdding();   // ends the worker's consuming loop
        _worker.Join(1000);        // give it a moment to finish the current utterance + unload
        _ready.Dispose();
        _queue.Dispose();
    }

    #endregion
}
