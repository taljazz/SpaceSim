using System;

namespace SpaceSim;

internal static class Program
{
    // STA is required so the WinForms help reader (TopicReaderForm.ShowDialog) behaves correctly;
    // MonoGame runs fine on an STA thread. The using-var disposes the game host on exit.
    [STAThread]
    private static void Main()
    {
        using var game = new SpaceSimGame();
        game.Run();
    }
}
