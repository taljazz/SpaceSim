using System;
using System.Runtime.InteropServices;

namespace SpaceSim;

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

public sealed class TolkSpeechService : IDisposable
{
    private readonly bool _isActive;
    private bool _disposed;

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

    public bool IsScreenReaderActive => _isActive;

    public void Speak(string text, bool interrupt = false)
    {
        if (_isActive)
            TolkNative.Tolk_Output(text, interrupt);
        else
            Console.WriteLine(text);
    }

    public void Silence()
    {
        if (_isActive)
            TolkNative.Tolk_Silence();
    }

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
