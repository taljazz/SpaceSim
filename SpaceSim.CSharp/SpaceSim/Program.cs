using System;
using System.Runtime.InteropServices;

namespace SpaceSim;

internal static class Program
{
    // Add the app's base directory to the native DLL search path. In a single-file self-extract build the
    // bundled native DLLs — Tolk.dll AND the screen-reader client DLLs it loads internally by name
    // (nvdaControllerClient64.dll, etc.) — live in the extraction directory, not beside the .exe. Tolk uses
    // the Win32 default search for its clients, which does NOT include that directory, so without this the
    // screen reader silently fails to load. Harmless in a normal (non-bundled) run.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    // STA is required so the WinForms help reader (TopicReaderForm.ShowDialog) behaves correctly;
    // MonoGame runs fine on an STA thread. The using-var disposes the game host on exit.
    [STAThread]
    private static void Main()
    {
        try { SetDllDirectory(AppContext.BaseDirectory); } catch { /* non-fatal: native search stays default */ }

        using var game = new SpaceSimGame();
        game.Run();
    }
}
