using System;
using System.Runtime.InteropServices;

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
/// Friendly wrapper over <see cref="TolkNative"/>. Loads Tolk on construction and, if no screen
/// reader is available, gracefully falls back to writing speech to the console so the game stays
/// usable everywhere.
/// </summary>
public sealed class TolkSpeechService : IDisposable
{
    private readonly bool _isActive;
    private bool _disposed;

    /// <summary>Loads Tolk (with SAPI fallback) and records whether speech output is available.</summary>
    public TolkSpeechService()
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
    }

    /// <summary>True if a real screen reader is driving output (false means console fallback).</summary>
    public bool IsScreenReaderActive => _isActive;

    /// <summary>Speak <paramref name="text"/>; <paramref name="interrupt"/> cuts off any current speech.</summary>
    public void Speak(string text, bool interrupt = false)
    {
        if (_isActive)
            TolkNative.Tolk_Output(text, interrupt);
        else
            Console.WriteLine(text);
    }

    /// <summary>Stop any speech currently being read out.</summary>
    public void Silence()
    {
        if (_isActive)
            TolkNative.Tolk_Silence();
    }

    /// <summary>Unloads Tolk on shutdown (no-op if it was never active).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isActive)
        {
            try { TolkNative.Tolk_Unload(); }
            catch { /* Ignore errors during unload */ }
        }
    }
}
