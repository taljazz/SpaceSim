using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Menus;
using SpaceSim.Models;
using SpaceSim.Rendering;

namespace SpaceSim;

public partial class SpaceSimGame
{
    #region Game loop

    /// <summary>
    /// The per-frame game loop: reads input, handles the global fullscreen key, then dispatches to the
    /// active screen — the main menu, the sound dictionary, or the live sim (<see cref="UpdatePlaying"/>) —
    /// and finally keeps the spatial-audio engine and saved preferences in step.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // F11 toggles fullscreen on any screen.
        if (IsKeyPressed(keys, Keys.F11))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
            _camera.UpdateProjection(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
        }

        // F1 toggles the help screen from anywhere (it remembers where to return to).
        if (IsKeyPressed(keys, Keys.F1))
            ApplyTransition(_screen == GameScreen.Help ? ScreenTransition.CloseHelp : ScreenTransition.OpenHelp);

        // Route the frame to the active top-level screen.
        switch (_screen)
        {
            case GameScreen.MainMenu:
                UpdateMenuIntro(dt, keys);
                ApplyTransition(_mainMenu.HandleInput(keys, _prevKeyState));
                break;
            case GameScreen.LearnSounds:
                ApplyTransition(_learnSounds.HandleInput(keys, _prevKeyState));
                break;
            case GameScreen.Help:
                ApplyTransition(_help.HandleInput(keys, _prevKeyState));
                break;
            case GameScreen.Playing:
                UpdatePlaying(dt, keys, mouse);
                break;
        }

        // Keep the OpenAL spatial engine in step every frame, on any screen: match its master level
        // to NAudio's (so the volume keys line up) and reclaim finished sources. Game-thread only —
        // these calls must never run on the NAudio audio-callback thread.
        _openAl.SetMasterGain(_audio.MasterVolume);
        _openAl.Update();

        // Persist any changed preferences (debounced + async, so this never stalls the loop).
        UpdateSettingsPersistence();

        // Save previous input states
        _prevKeyState = keys;
        _prevMouseState = mouse;
        _prevScrollValue = mouse.ScrollWheelValue;

        base.Update(gameTime);
    }

    /// <summary>
    /// Per-frame update while the simulation is running: sim/global keys, zoom, camera orbit, ship
    /// input and update, and universe regeneration. Escape returns to the main menu.
    /// </summary>
    private void UpdatePlaying(float dt, KeyboardState keys, MouseState mouse)
    {
        // Escape leaves the sim and returns to the main menu (the world goes quiet).
        if (IsKeyPressed(keys, Keys.Escape))
        {
            ApplyTransition(ScreenTransition.BackToMainMenu);
            return;
        }

        // Advance the simulation clock only while playing, so menu time is effectively paused —
        // orbits and cooldowns freeze instead of silently elapsing during a menu visit.
        _ship.SimulationTime += dt;

        // F10 -> toggle 2D/3D renderer
        if (IsKeyPressed(keys, Keys.F10))
        {
            _use3DRenderer = !_use3DRenderer;
            _activeRenderer = _use3DRenderer ? _renderer3D : _renderer2D;
            _tolk.Speak(_use3DRenderer ? "3D rendering mode." : "2D rendering mode.");
            DebugLogger.Log("Render", $"Renderer toggled to {(_use3DRenderer ? "3D" : "2D")}");
        }

        // --- Zoom controls ---
        // Mouse wheel
        int scrollDelta = mouse.ScrollWheelValue - _prevScrollValue;
        if (scrollDelta != 0)
        {
            float zoomDelta = scrollDelta > 0 ? 0.1f : -0.1f;
            _zoomLevel = MathHelper.Clamp(_zoomLevel + zoomDelta, 0.2f, 5f);
            _camera.AdjustZoom(-scrollDelta * 0.02f);
        }

        // Bracket keys for zoom
        if (keys.IsKeyDown(Keys.OemCloseBrackets))
        {
            _zoomLevel = MathHelper.Clamp(_zoomLevel + 2f * dt, 0.2f, 5f);
            _camera.AdjustZoom(-15f * dt);
        }
        if (keys.IsKeyDown(Keys.OemOpenBrackets))
        {
            _zoomLevel = MathHelper.Clamp(_zoomLevel - 2f * dt, 0.2f, 5f);
            _camera.AdjustZoom(15f * dt);
        }

        // Backslash -> reset zoom
        if (IsKeyPressed(keys, Keys.OemBackslash))
        {
            _zoomLevel = 1f;
            _camera.ZoomDistance = 30f;
        }

        // --- Camera orbit controls ---
        float rotationSpeed = 3f * dt;
        float pitchSpeed = 60f * dt;

        // Left/Right arrows: rotate camera yaw and sync ship view rotation
        if (keys.IsKeyDown(Keys.Left))
        {
            _camera.RotateYaw(-rotationSpeed);
            _ship.ViewRotation = _camera.YawAngle;
        }
        if (keys.IsKeyDown(Keys.Right))
        {
            _camera.RotateYaw(rotationSpeed);
            _ship.ViewRotation = _camera.YawAngle;
        }

        // Home/End: adjust camera pitch
        if (keys.IsKeyDown(Keys.Home))
            _camera.AdjustPitch(pitchSpeed);
        if (keys.IsKeyDown(Keys.End))
            _camera.AdjustPitch(-pitchSpeed);

        // Comma/Period: horizontal orbit (alternative to arrows)
        if (keys.IsKeyDown(Keys.OemComma))
        {
            _camera.RotateYaw(-rotationSpeed);
            _ship.ViewRotation = _camera.YawAngle;
        }
        if (keys.IsKeyDown(Keys.OemPeriod))
        {
            _camera.RotateYaw(rotationSpeed);
            _ship.ViewRotation = _camera.YawAngle;
        }

        // --- Ship input and update ---
        _ship.ZoomLevel = _zoomLevel;

        // Update celestial positions (orbital mechanics) BEFORE ship update
        // so the spatial grid and proximity queries use fresh positions.
        CelestialGenerator.UpdateCelestialPositions(_stars, _planets, _nebulae,
                                                     _ship.SimulationTime);
        _spatialGrid.Rebuild(_celestialBodies);
        _ship.SpatialGrid = _spatialGrid;

        _ship.HandleInput(keys, _prevKeyState, mouse, _prevMouseState,
                          _stars, _planets, _nebulae);
        _ship.Update(dt, _celestialBodies, keys, _temples, _leyLines, _pyramids);

        // Check if universe needs regeneration (after rift transit)
        if (_ship.NeedsUniverseRegeneration)
        {
            DebugLogger.Log("Init", "Universe regeneration triggered");
            _ship.NeedsUniverseRegeneration = false;
            GenerateUniverse();
            _ship.Stars = _stars;
            _ship.Planets = _planets;
            _ship.Nebulae = _nebulae;
            UpdateRendererWorldData();
            DebugLogger.Log("Init", "Universe regeneration completed");
        }

        // Audio click effect (periodic resonance feedback)
        UpdateClickEffect();

        // Update camera position
        _camera.Update(_ship.Position, dt);
    }

    #endregion

    #region Rendering

    /// <summary>Clears the screen, draws the world via the active renderer, then overlays the HUD text.</summary>
    protected override void Draw(GameTime gameTime)
    {
        int screenW = GraphicsDevice.Viewport.Width;
        int screenH = GraphicsDevice.Viewport.Height;

        // High-contrast white background only applies to the live sim; the menus draw on black.
        bool whiteBg = _screen == GameScreen.Playing && _ship.HighContrast;
        GraphicsDevice.Clear(whiteBg ? Color.White : Color.Black);

        switch (_screen)
        {
            case GameScreen.Playing:
                // Draw the game world via the active renderer, then the HUD overlay on top.
                _activeRenderer.DrawWorld(_spriteBatch, _ship, gameTime, screenW, screenH);
                if (_font != null)
                {
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    HudRenderer.DrawHud(_spriteBatch, _font, _ship, screenW, screenH);
                    _spriteBatch.End();
                }
                break;

            case GameScreen.MainMenu:
            case GameScreen.LearnSounds:
            case GameScreen.Help:
                if (_font != null)
                {
                    MenuScreen menu = _screen switch
                    {
                        GameScreen.MainMenu => _mainMenu,
                        GameScreen.LearnSounds => _learnSounds,
                        _ => _help,
                    };
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    menu.Draw(_spriteBatch, _font, screenW, screenH);
                    _spriteBatch.End();
                }
                break;
        }

        base.Draw(gameTime);
    }

    #endregion

    #region Helpers

    /// <summary>True only on the frame a key transitions from up to down (edge-triggered).</summary>
    private bool IsKeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_prevKeyState.IsKeyDown(key);
    }

    /// <summary>
    /// (Re)generate the entire universe and store the results in the world-data fields.
    /// Called at startup and again after a rift transit to spawn a fresh universe.
    /// </summary>
    private void GenerateUniverse()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var universe = CelestialGenerator.GenerateCompleteUniverse();
        _stars = universe.Stars;
        _planets = universe.Planets;
        _nebulae = universe.Nebulae;
        _celestialBodies = universe.CelestialBodies;
        _temples = universe.Temples;
        _leyLines = universe.LeyLines;
        _pyramids = universe.Pyramids;
        sw.Stop();
        DebugLogger.Log("Celestial", $"Universe generated in {sw.ElapsedMilliseconds}ms: " +
            $"{_stars.Count} stars, {_planets.Count} planets, {_nebulae.Count} nebulae, " +
            $"{_temples.Count} temples, {_leyLines.Count} ley lines, {_pyramids.Count} pyramids");
    }

    /// <summary>Pushes the current world-data lists (and zoom) into both renderers so they draw the latest universe.</summary>
    private void UpdateRendererWorldData()
    {
        _renderer3D.Stars = _stars;
        _renderer3D.Planets = _planets;
        _renderer3D.Nebulae = _nebulae;
        _renderer3D.Temples = _temples;
        _renderer3D.LeyLines = _leyLines;
        _renderer3D.Pyramids = _pyramids;

        _renderer2D.Stars = _stars;
        _renderer2D.Planets = _planets;
        _renderer2D.Nebulae = _nebulae;
        _renderer2D.Temples = _temples;
        _renderer2D.LeyLines = _leyLines;
        _renderer2D.Pyramids = _pyramids;
        _renderer2D.ZoomLevel = _zoomLevel;
    }

    /// <summary>
    /// Plays a periodic click sound based on average resonance,
    /// giving audio feedback about how well-tuned the ship is.
    /// </summary>
    private void UpdateClickEffect()
    {
        if (_ship.SimulationTime < _nextClickTime) return;

        // Average resonance determines click interval
        float avgResonance = 0f;
        for (int i = 0; i < GameConstants.NDimensions; i++)
            avgResonance += _ship.ResonanceLevels[i];
        avgResonance /= GameConstants.NDimensions;

        // Higher resonance = faster clicks (more feedback)
        float interval = MathHelper.Lerp(2f, 0.2f, avgResonance);
        _nextClickTime = _ship.SimulationTime + interval;

        if (avgResonance > 0.1f && _audio.ClickWaveform.Length > 0)
        {
            _audio.AddSoundEffect(new GameSoundEffect(
                _audio.ClickWaveform, volume: 0.3f * avgResonance));
        }
    }

    #endregion
}
