using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Menus;
using SpaceSim.Models;

namespace SpaceSim;

/// <summary>
/// Main game class for the SpaceSim 5D resonance-based spaceship simulator.
/// Manages initialization, input, update loop, and rendering delegation.
///
/// <para>
/// This is the MonoGame host: it owns the graphics device, the core systems (<see cref="Ship"/>,
/// <see cref="AudioSystem"/>, speech), the camera, and both renderers. The per-frame logic lives
/// in the partial half (<c>SpaceSimGame.Update.cs</c>); this file handles the lifecycle —
/// construction, <see cref="Initialize"/>, <see cref="LoadContent"/>, the <see cref="GameEvents"/>
/// wiring, and teardown.
/// </para>
/// </summary>
public partial class SpaceSimGame : Game
{
    #region Fields

    // --- Graphics ---
    // The window/graphics device is kept alive (MonoGame requires it) but nothing is drawn — the game
    // is entirely audio- and screen-reader-driven. See Draw(), which only clears the window.
    private readonly GraphicsDeviceManager _graphics;

    // --- Core systems ---
    private Ship _ship = null!;
    private AudioSystem _audio = null!;
    private OpenAlAudio _openAl = null!;   // OpenAL Soft spatial engine for positioned world sounds (Round 3)
    private TolkSpeechService _tolk = null!;

    // --- World data ---
    private List<CelestialBody> _stars = new();
    private List<CelestialBody> _planets = new();
    private List<CelestialBody> _nebulae = new();
    private List<CelestialBody> _celestialBodies = new();
    private List<Temple> _temples = new();
    private List<LeyLine> _leyLines = new();
    private List<Pyramid> _pyramids = new();
    private readonly SpatialGrid _spatialGrid = new();

    // --- Input state (keyboard only) ---
    private KeyboardState _prevKeyState;

    // --- Audio click timer ---
    private float _nextClickTime;

    // --- Settings persistence (preferences, separate from the run savegame) ---
    private GameSettings? _settings;
    private bool _settingsDirty;
    private float _settingsDirtyTime;
    private const float SettingsSaveDebounce = 1.5f; // persist 1.5s after the last preference change

    // --- Top-level screen state (main menu / sim / sound dictionary / help) ---
    private GameScreen _screen = GameScreen.MainMenu;
    private MainMenuScreen _mainMenu = null!;
    private LearnSoundsScreen _learnSounds = null!;
    private HelpScreen _help = null!;
    private GameScreen _returnScreen = GameScreen.MainMenu;   // where Help returns to when closed
    private Tutorial? _tutorial;                              // the interactive tutorial, when active (null otherwise)

    // First-launch only: defer the spoken main-menu intro briefly so the screen reader's window-focus
    // announcement finishes first instead of cutting off our "use up/down, Enter to select" instructions.
    private bool _menuIntroPending = true;
    private float _menuIntroTimer;
    private const float MenuIntroDelay = 0.064f;

    #endregion

    #region Construction and lifecycle

    /// <summary>Configures the graphics device manager and debug logging. Heavy setup is deferred to <see cref="Initialize"/>.</summary>
    public SpaceSimGame()
    {
        DebugLogger.Initialize();
        DebugLogger.Log("Init", "SpaceSimGame constructor started");

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = GameConstants.ScreenWidth,
            PreferredBackBufferHeight = GameConstants.ScreenHeight,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        DebugLogger.Log("Init", $"Graphics configured: {GameConstants.ScreenWidth}x{GameConstants.ScreenHeight}");
    }

    protected override void Initialize()
    {
        DebugLogger.Log("Init", "Initialize() started");

        // Initialize speech service
        try
        {
            _tolk = new TolkSpeechService();
            DebugLogger.Log("Init", $"TolkSpeechService created, active: {_tolk.IsScreenReaderActive}, reader: {_tolk.DetectedReader}, baseDir: {AppContext.BaseDirectory}");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Init", "TolkSpeechService failed", ex);
            _tolk = new TolkSpeechService(); // fallback - will use Console
        }

        // Initialize audio system
        try
        {
            _audio = new AudioSystem(masterVolume: 0.2f, beepVolume: 0.3f,
                                     effectVolume: 0.2f, driveVolume: 0.05f);
            DebugLogger.Log("Audio", "AudioSystem created successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "AudioSystem creation failed", ex);
            throw;
        }

        // Initialize the OpenAL spatial-audio engine (Round 3 — positioned world sounds + HRTF).
        // Self-guarding: if OpenAL/HRTF can't initialize, IsAvailable stays false and world sounds
        // fall back to NAudio panning.
        _openAl = new OpenAlAudio();

        // Generate the complete universe
        DebugLogger.Log("Init", "Generating universe...");
        GenerateUniverse();

        // Create the ship
        _ship = new Ship(_audio, _openAl, _tolk);
        _ship.Stars = _stars;
        _ship.Planets = _planets;
        _ship.Nebulae = _nebulae;
        _ship.Temples = _temples;
        _ship.Pyramids = _pyramids;
        DebugLogger.Log("Init", "Ship created and world data assigned");

        // Start audio
        _audio.SetShip(_ship);
        try
        {
            _audio.Start();
            DebugLogger.Log("Audio", "AudioSystem started successfully");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "AudioSystem.Start() failed", ex);
        }

        // Subscribe to game events
        GameEvents.OnSpeak += HandleSpeak;
        GameEvents.OnUniverseRegenNeeded += HandleUniverseRegenNeeded;
        GameEvents.OnModeChanged += HandleModeChanged;
        GameEvents.OnAscension += HandleAscension;
        DebugLogger.Log("Init", "Subscribed to GameEvents");

        // Load saved preferences (volumes, accessibility, toggles) and apply them so the player's
        // choices carry over from the last session.
        _settings = SettingsStore.Load();
        ApplySettings(_settings);
        DebugLogger.Log("Init", "Preferences loaded and applied");

        // Build the top-level menus and open on the main menu (the game starts here, not in the sim).
        // The engine synth stays silent until the player chooses "Start Sim". The spoken intro is
        // deferred briefly into the game loop (see UpdateMenuIntro) so the screen reader's
        // window-focus announcement doesn't talk over our menu instructions.
        Action<string> menuSpeak = msg => _tolk.Speak(msg, interrupt: true);
        _mainMenu = new MainMenuScreen(_audio, menuSpeak);
        _learnSounds = new LearnSoundsScreen(_audio, menuSpeak);
        _help = new HelpScreen(_audio, menuSpeak);
        _screen = GameScreen.MainMenu;
        DebugLogger.Log("Init", "Main menu ready");

        // Capture initial input state
        _prevKeyState = Keyboard.GetState();

        DebugLogger.Log("Init", "Initialize() completed");
        base.Initialize();
    }

    // LoadContent intentionally omitted: there are no sprites, fonts, or renderers to load now that
    // visuals have been removed. MonoGame still creates the window from the GraphicsDeviceManager.

    #endregion

    #region GameEvent handlers

    private void HandleSpeak(object? sender, SpeakEventArgs e)
    {
        _tolk.Speak(e.Message, e.Interrupt);
    }

    private void HandleUniverseRegenNeeded(object? sender, EventArgs e)
    {
        DebugLogger.Log("Event", "Universe regeneration requested via event");
        // The flag is still set on Ship, so the existing Update() logic handles it
    }

    private void HandleModeChanged(object? sender, ModeChangeEventArgs e)
    {
        DebugLogger.Log("Event", $"Mode changed: {e.ModeName} = {e.Value}");
    }

    private void HandleAscension(object? sender, EventArgs e)
    {
        DebugLogger.Log("Event", "Ascension event received");
    }

    #endregion

    #region Settings persistence

    /// <summary>Apply loaded preferences to the live systems (audio volumes, ship options, renderer).</summary>
    private void ApplySettings(GameSettings s)
    {
        _audio.MasterVolume = s.MasterVolume;
        _audio.BeepVolume = s.BeepVolume;
        _audio.EffectVolume = s.EffectVolume;
        _audio.DriveVolume = s.DriveVolume;

        _ship.VerboseMode = s.VerboseMode;
        _ship.ByEarMode = s.ByEarMode;
        _ship.AutosaveEnabled = s.AutosaveEnabled;
        _ship.AmbientSoundsEnabled = s.AmbientSoundsEnabled;
        _ship.NebulaDissonanceEnabled = s.NebulaDissonanceEnabled;

        // Restore the chosen output device (matched by name; silently stays on default if it's gone).
        if (!string.IsNullOrEmpty(s.OutputDeviceName))
        {
            int dev = AudioSystem.FindDeviceByName(s.OutputDeviceName);
            if (dev >= 0)
            {
                _audio.SetOutputDevice(dev);
                _openAl.TryReopen(s.OutputDeviceName);
                _openAl.SetMasterGain(_audio.MasterVolume);
            }
        }
    }

    /// <summary>
    /// Copy the current live preferences into <see cref="_settings"/>; returns true if anything
    /// changed since the last capture. Cheap (a handful of comparisons) and allocation-free.
    /// </summary>
    private bool CaptureSettings()
    {
        var s = _settings;
        if (s == null) return false;

        bool changed = false;
        if (s.MasterVolume != _audio.MasterVolume) { s.MasterVolume = _audio.MasterVolume; changed = true; }
        if (s.BeepVolume != _audio.BeepVolume) { s.BeepVolume = _audio.BeepVolume; changed = true; }
        if (s.EffectVolume != _audio.EffectVolume) { s.EffectVolume = _audio.EffectVolume; changed = true; }
        if (s.DriveVolume != _audio.DriveVolume) { s.DriveVolume = _audio.DriveVolume; changed = true; }

        if (s.VerboseMode != _ship.VerboseMode) { s.VerboseMode = _ship.VerboseMode; changed = true; }
        if (s.ByEarMode != _ship.ByEarMode) { s.ByEarMode = _ship.ByEarMode; changed = true; }
        if (s.AutosaveEnabled != _ship.AutosaveEnabled) { s.AutosaveEnabled = _ship.AutosaveEnabled; changed = true; }
        if (s.AmbientSoundsEnabled != _ship.AmbientSoundsEnabled) { s.AmbientSoundsEnabled = _ship.AmbientSoundsEnabled; changed = true; }
        if (s.NebulaDissonanceEnabled != _ship.NebulaDissonanceEnabled) { s.NebulaDissonanceEnabled = _ship.NebulaDissonanceEnabled; changed = true; }

        return changed;
    }

    /// <summary>
    /// Once per frame: notice any preference change and persist it — debounced so holding a volume
    /// key doesn't thrash the disk, and async so the save never stalls the loop.
    /// </summary>
    private void UpdateSettingsPersistence()
    {
        if (_settings == null) return;

        if (CaptureSettings())
        {
            _settingsDirty = true;
            _settingsDirtyTime = _ship.SimulationTime;
        }

        if (_settingsDirty && _ship.SimulationTime - _settingsDirtyTime > SettingsSaveDebounce)
        {
            SettingsStore.Save(_settings);
            _settingsDirty = false;
            DebugLogger.Log("Settings", "Preferences saved.");
        }
    }

    #endregion

    #region Audio device chooser (F3)

    /// <summary>
    /// F3: open a modal chooser of the system's output devices and route the sim's audio to the picked
    /// one. The NAudio mix (drive, cue, beeps, menus) moves for certain; the OpenAL spatial layer follows
    /// best-effort by name. The choice is persisted by name. Runs on the game thread (STA), so the modal
    /// dialog is safe and the audio threads keep playing while it is open.
    /// </summary>
    private void OpenAudioDeviceDialog()
    {
        try
        {
            var devices = AudioSystem.GetOutputDevices();
            using var form = new AudioDeviceForm(devices, _audio.CurrentDeviceNumber);
            if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            // Re-selecting the already-active device would needlessly rebuild the OpenAL context and briefly
            // silence the world loops, so treat it as a no-op.
            if (form.SelectedDeviceNumber == _audio.CurrentDeviceNumber)
            {
                _tolk.Speak($"Already using {form.SelectedDeviceName}.", interrupt: true);
                return;
            }

            _audio.SetOutputDevice(form.SelectedDeviceNumber);

            // Move the spatial engine to a name-matched device too; silence live world loops first so no
            // stale OpenAL sources survive the context swap (the ship recreates them next frame).
            _ship.SilenceAllWorldSounds();
            _openAl.TryReopen(form.SelectedDeviceNumber < 0 ? null : form.SelectedDeviceName);
            _openAl.SetMasterGain(_audio.MasterVolume);   // restore listener gain now (reopen resets it to 1)

            if (_settings != null)
            {
                _settings.OutputDeviceName = form.SelectedDeviceNumber < 0 ? "" : form.SelectedDeviceName;
                // Persist immediately: a discrete choice needs no debounce, and the debounce clock
                // (SimulationTime) is frozen off the Playing screen, so it might otherwise never save.
                SettingsStore.Save(_settings);
                _settingsDirty = false;
            }

            _tolk.Speak($"Audio output set to {form.SelectedDeviceName}.", interrupt: true);
            DebugLogger.Log("Audio", $"Output device chosen: {form.SelectedDeviceName} (#{form.SelectedDeviceNumber}).");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Audio", "Audio device dialog failed", ex);
        }
    }

    #endregion

    #region Screen transitions

    /// <summary>Apply a screen change requested by a menu (or by pressing Escape in the sim).</summary>
    private void ApplyTransition(ScreenTransition t)
    {
        switch (t)
        {
            case ScreenTransition.StartSim:
                _screen = GameScreen.Playing;
                _tutorial = null;
                _audio.EngineEnabled = true;          // resume the live resonance-drive synthesis
                SpeakStartOrientation();
                DebugLogger.Log("Event", "Screen -> Playing");
                break;

            case ScreenTransition.StartTutorial:
                _screen = GameScreen.Playing;
                _audio.EngineEnabled = true;
                _ship.PrepareForTutorial();           // clean, known state regardless of any prior play session
                _tutorial = new Tutorial();
                _tutorial.Begin(_ship);               // then detune the higher realms so the tuning steps are hands-on
                DebugLogger.Log("Event", "Screen -> Playing (tutorial)");
                break;

            case ScreenTransition.OpenLearnSounds:
                _screen = GameScreen.LearnSounds;
                _learnSounds.OnEnter();
                DebugLogger.Log("Event", "Screen -> LearnSounds");
                break;

            case ScreenTransition.OpenHelp:
                // Pause the sim's audio while reading help (resumes on close); from a menu there's nothing to pause.
                if (_screen == GameScreen.Playing)
                {
                    _audio.EngineEnabled = false;
                    _ship.SilenceAllWorldSounds();
                    _audio.ClearAllEffects();
                }
                else if (_screen == GameScreen.LearnSounds)
                {
                    _learnSounds.OnExit();   // stop any looping sound demo so it doesn't play under Help
                }
                _returnScreen = _screen;
                _screen = GameScreen.Help;
                _help.OnEnter();
                DebugLogger.Log("Event", "Screen -> Help");
                break;

            case ScreenTransition.CloseHelp:
                _screen = _returnScreen;
                if (_returnScreen == GameScreen.Playing) _audio.EngineEnabled = true; // resume the sim
                else if (_returnScreen == GameScreen.MainMenu) _mainMenu.OnEnter();
                else if (_returnScreen == GameScreen.LearnSounds) _learnSounds.OnEnter();
                DebugLogger.Log("Event", $"Screen -> {_returnScreen} (closed Help)");
                break;

            case ScreenTransition.BackToMainMenu:
                LeaveCurrentScreen();
                _screen = GameScreen.MainMenu;
                _mainMenu.OnEnter();
                DebugLogger.Log("Event", "Screen -> MainMenu");
                break;

            case ScreenTransition.Quit:
                DebugLogger.Log("Event", "Quit requested from main menu");
                Exit();
                break;

            case ScreenTransition.None:
            default:
                break;
        }
    }

    /// <summary>
    /// On the first ever Start Sim, speak a short spoken orientation (the core loop + the F1 help
    /// pointer) and remember it so it isn't repeated; on later starts just confirm and point to F1.
    /// </summary>
    private void SpeakStartOrientation()
    {
        if (_settings is { TutorialSeen: false })
        {
            _tolk.Speak(
                "Welcome to Space Sim. You fly by tuning your five realms into resonance: press 1 to 5 to " +
                "choose a realm, then up and down to tune it until the pulsing tone steadies. Press M to scan " +
                "for a destination and Enter to lock on. Press F1 at any time for the full controls and your goal.",
                interrupt: true);
            _settings.TutorialSeen = true;
            _settingsDirty = true;                      // persist that the player has now seen the intro
            _settingsDirtyTime = _ship.SimulationTime;
        }
        else
        {
            _tolk.Speak("Simulation started. Press F1 for help.", interrupt: true);
        }
    }

    /// <summary>Tidy up the screen we're leaving: silence the sim, or stop a sound demo.</summary>
    private void LeaveCurrentScreen()
    {
        switch (_screen)
        {
            case GameScreen.Playing:
                _audio.EngineEnabled = false;   // stop the drive drone under the menu
                _ship.SilenceAllWorldSounds();  // stop positioned world loops (ambients, rift hums, lock)
                _audio.ClearAllEffects();       // drop any lingering one-shots / loops
                _ship.ActiveMenu = null;        // close any open in-sim menu so re-entry isn't frozen inside it
                _ship.ResetHeldInput();         // drop held-key latches (e.g. Space) so they don't persist on resume
                _tutorial = null;               // leaving the sim ends any active tutorial
                break;
            case GameScreen.LearnSounds:
                _learnSounds.OnExit();          // stop any playing demo
                break;
        }
    }

    /// <summary>
    /// First launch only: speak the main-menu title + instructions after a short delay, so the screen
    /// reader's window-focus announcement finishes first instead of cutting them off. Cancelled if the
    /// player navigates earlier — their own input announces the items instead.
    /// </summary>
    private void UpdateMenuIntro(float dt, KeyboardState keys)
    {
        if (!_menuIntroPending) return;

        // Any menu key (including Escape, which opens the quit prompt) means the player is already
        // engaged — cancel the pending intro and let their input do the talking.
        if (IsKeyPressed(keys, Keys.Up) || IsKeyPressed(keys, Keys.Down)
            || IsKeyPressed(keys, Keys.Enter) || IsKeyPressed(keys, Keys.Space)
            || IsKeyPressed(keys, Keys.Escape))
        {
            _menuIntroPending = false;
            return;
        }

        _menuIntroTimer += dt;
        if (_menuIntroTimer >= MenuIntroDelay)
        {
            _menuIntroPending = false;
            _mainMenu.OnEnter();
        }
    }

    #endregion

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DebugLogger.Log("Init", "Dispose() started - cleaning up systems");

            // Tidy up the active screen first, so closing the window mid-sim (or mid-demo) stops world
            // sounds the same way Escape does, before the audio devices are torn down. No-op at the menu.
            LeaveCurrentScreen();

            // Persist preferences synchronously before we exit. We re-capture first so this write
            // carries the very latest state, and (being enqueued last) it gets the highest write
            // sequence — so even if a debounced async save is still in flight, the sequence guard in
            // SettingsStore makes this the winner and the newest preferences are guaranteed to land.
            // Keep the capture immediately before the blocking save: that ordering is the guarantee.
            if (_settings != null)
            {
                CaptureSettings();
                SettingsStore.SaveBlocking(_settings);
                DebugLogger.Log("Settings", "Preferences saved on exit");
            }

            // Unsubscribe from game events
            GameEvents.OnSpeak -= HandleSpeak;
            GameEvents.OnUniverseRegenNeeded -= HandleUniverseRegenNeeded;
            GameEvents.OnModeChanged -= HandleModeChanged;
            GameEvents.OnAscension -= HandleAscension;
            DebugLogger.Log("Init", "Unsubscribed from GameEvents");

            _audio?.Stop();
            _audio?.Dispose();
            DebugLogger.Log("Audio", "AudioSystem disposed");
            _openAl?.Dispose();
            _tolk?.Dispose();
            DebugLogger.Log("Init", "TolkSpeechService disposed");
            DebugLogger.Flush();
            DebugLogger.Shutdown();
        }

        base.Dispose(disposing);
    }

    #endregion
}
