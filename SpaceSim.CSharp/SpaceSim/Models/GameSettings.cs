namespace SpaceSim.Models;

/// <summary>
/// Player <b>preferences</b> that persist across sessions in <c>settings.json</c> — kept separate
/// from the run-progress savegame. Loaded once at startup and auto-saved (debounced) whenever a
/// preference changes, so volumes, accessibility options, and toggles survive a relaunch.
///
/// <para>Defaults here mirror the code defaults on <c>AudioSystem</c>, <c>Ship</c>, and the renderer,
/// so a missing or first-run settings file behaves exactly like the game always has.</para>
/// </summary>
public class GameSettings
{
    #region Audio volumes (0..1)

    public float MasterVolume { get; set; } = 0.2f;
    public float BeepVolume { get; set; } = 0.3f;
    public float EffectVolume { get; set; } = 0.2f;
    public float DriveVolume { get; set; } = 0.05f;

    #endregion

    #region Accessibility / HUD

    /// <summary>Speech verbosity: 0 = low, 1 = medium, 2 = high.</summary>
    public int VerboseMode { get; set; } = 1;
    public int HudTextSize { get; set; } = GameConstants.HudTextSizeBase;
    public bool HighContrast { get; set; }

    #endregion

    #region Toggles

    public bool AutosaveEnabled { get; set; } = true;
    public bool AmbientSoundsEnabled { get; set; } = true;
    public bool NebulaDissonanceEnabled { get; set; } = true;

    #endregion

    #region Rendering

    /// <summary>True = 3D renderer, false = 2D fallback (toggled with F10).</summary>
    public bool Use3DRenderer { get; set; } = true;

    #endregion

    #region Onboarding

    /// <summary>True once the first-run spoken orientation has played, so it isn't repeated every launch.</summary>
    public bool TutorialSeen { get; set; }

    #endregion
}
