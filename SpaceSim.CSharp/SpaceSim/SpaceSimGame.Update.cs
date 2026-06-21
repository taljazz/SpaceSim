using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Models;
using SpaceSim.Rendering;

namespace SpaceSim;

public partial class SpaceSimGame
{
    #region Game loop

    /// <summary>
    /// The per-frame game loop: reads input, handles global keys (exit, fullscreen, renderer
    /// toggle, zoom, camera orbit), advances celestial mechanics and the ship, regenerates the
    /// universe after a rift transit, and updates audio and camera.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ship.SimulationTime += dt;

        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // --- Global keys ---

        // ESC -> exit
        if (keys.IsKeyDown(Keys.Escape))
            Exit();

        // F11 -> toggle fullscreen
        if (IsKeyPressed(keys, Keys.F11))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
            _camera.UpdateProjection(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
        }

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

        // Bracket keys for zoom (with Shift held)
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

        // Save previous input states
        _prevKeyState = keys;
        _prevMouseState = mouse;
        _prevScrollValue = mouse.ScrollWheelValue;

        base.Update(gameTime);
    }

    #endregion

    #region Rendering

    /// <summary>Clears the screen, draws the world via the active renderer, then overlays the HUD text.</summary>
    protected override void Draw(GameTime gameTime)
    {
        // Clear screen
        GraphicsDevice.Clear(_ship.HighContrast ? Color.White : Color.Black);

        int screenW = GraphicsDevice.Viewport.Width;
        int screenH = GraphicsDevice.Viewport.Height;

        // Draw the game world using the active renderer
        _activeRenderer.DrawWorld(_spriteBatch, _ship, gameTime, screenW, screenH);

        // Draw HUD overlay
        if (_font != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            HudRenderer.DrawHud(_spriteBatch, _font, _ship, screenW, screenH);
            _spriteBatch.End();
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
