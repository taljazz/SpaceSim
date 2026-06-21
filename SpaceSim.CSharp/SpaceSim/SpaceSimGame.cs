using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;
using SpaceSim.Rendering;

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
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont? _font;

    // --- Core systems ---
    private Ship _ship = null!;
    private AudioSystem _audio = null!;
    private OpenAlAudio _openAl = null!;   // OpenAL Soft spatial engine for positioned world sounds (Round 3)
    private TolkSpeechService _tolk = null!;

    // --- Camera ---
    private Camera3D _camera = null!;

    // --- Renderers ---
    private IGameRenderer _activeRenderer = null!;
    private Renderer3D _renderer3D = null!;
    private Renderer2D _renderer2D = null!;
    private bool _use3DRenderer = true;

    // --- World data ---
    private List<CelestialBody> _stars = new();
    private List<CelestialBody> _planets = new();
    private List<CelestialBody> _nebulae = new();
    private List<CelestialBody> _celestialBodies = new();
    private List<Temple> _temples = new();
    private List<LeyLine> _leyLines = new();
    private List<Pyramid> _pyramids = new();
    private readonly SpatialGrid _spatialGrid = new();

    // --- Input state ---
    private KeyboardState _prevKeyState;
    private MouseState _prevMouseState;
    private int _prevScrollValue;

    // --- Zoom ---
    private float _zoomLevel = 1f;

    // --- Audio click timer ---
    private float _nextClickTime;

    // --- Settings persistence (preferences, separate from the run savegame) ---
    private GameSettings? _settings;
    private bool _settingsDirty;
    private float _settingsDirtyTime;
    private const float SettingsSaveDebounce = 1.5f; // persist 1.5s after the last preference change

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
            DebugLogger.Log("Init", $"TolkSpeechService created, screen reader active: {_tolk.IsScreenReaderActive}");
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

        // Initialize camera
        _camera = new Camera3D();
        DebugLogger.Log("Init", "Camera3D created");

        // Create both renderers
        _renderer3D = new Renderer3D();
        _renderer2D = new Renderer2D();
        _activeRenderer = _renderer3D;
        DebugLogger.Log("Init", "Renderers created, active: 3D");

        // Load saved preferences (volumes, accessibility, toggles, render mode) and apply them so
        // the player's choices carry over from the last session.
        _settings = SettingsStore.Load();
        ApplySettings(_settings);
        DebugLogger.Log("Init", "Preferences loaded and applied");

        // Capture initial input state
        _prevKeyState = Keyboard.GetState();
        _prevMouseState = Mouse.GetState();
        _prevScrollValue = _prevMouseState.ScrollWheelValue;

        DebugLogger.Log("Init", "Initialize() completed");
        base.Initialize();
    }

    protected override void LoadContent()
    {
        DebugLogger.Log("Init", "LoadContent() started");
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Try to load the font; if not available, font stays null and text is skipped
        try
        {
            _font = Content.Load<SpriteFont>("DefaultFont");
            DebugLogger.Log("Init", "Font 'DefaultFont' loaded successfully");
        }
        catch (Exception ex)
        {
            _font = null;
            DebugLogger.LogError("Init", "Font loading failed - HUD text will be disabled", ex);
        }

        // Initialize primitive renderers
        PrimitiveRenderer.Initialize(GraphicsDevice);
        PrimitiveRenderer2D.Initialize(GraphicsDevice);
        DebugLogger.Log("Init", "PrimitiveRenderers initialized");

        // Initialize both game renderers
        _renderer3D.Initialize(GraphicsDevice, Content);
        _renderer2D.Initialize(GraphicsDevice, Content);
        DebugLogger.Log("Init", "Renderer3D and Renderer2D initialized");

        // Set camera reference on 3D renderer
        _renderer3D.SetCamera(_camera);

        // Set world data on both renderers
        UpdateRendererWorldData();

        // Update camera projection
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _camera.UpdateProjection(w, h);

        DebugLogger.Log("Init", $"LoadContent() completed. Viewport: {w}x{h}");
    }

    #endregion

    #region GameEvent handlers

    private void HandleSpeak(object? sender, SpeakEventArgs e)
    {
        _tolk.Speak(e.Message);
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
        _ship.HudTextSize = s.HudTextSize;
        _ship.HighContrast = s.HighContrast;
        _ship.AutosaveEnabled = s.AutosaveEnabled;
        _ship.AmbientSoundsEnabled = s.AmbientSoundsEnabled;
        _ship.NebulaDissonanceEnabled = s.NebulaDissonanceEnabled;

        _use3DRenderer = s.Use3DRenderer;
        _activeRenderer = _use3DRenderer ? _renderer3D : _renderer2D;
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
        if (s.HudTextSize != _ship.HudTextSize) { s.HudTextSize = _ship.HudTextSize; changed = true; }
        if (s.HighContrast != _ship.HighContrast) { s.HighContrast = _ship.HighContrast; changed = true; }
        if (s.AutosaveEnabled != _ship.AutosaveEnabled) { s.AutosaveEnabled = _ship.AutosaveEnabled; changed = true; }
        if (s.AmbientSoundsEnabled != _ship.AmbientSoundsEnabled) { s.AmbientSoundsEnabled = _ship.AmbientSoundsEnabled; changed = true; }
        if (s.NebulaDissonanceEnabled != _ship.NebulaDissonanceEnabled) { s.NebulaDissonanceEnabled = _ship.NebulaDissonanceEnabled; changed = true; }

        if (s.Use3DRenderer != _use3DRenderer) { s.Use3DRenderer = _use3DRenderer; changed = true; }

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

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DebugLogger.Log("Init", "Dispose() started - cleaning up systems");

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
