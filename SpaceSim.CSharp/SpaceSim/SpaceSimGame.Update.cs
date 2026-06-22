using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceSim.Menus;
using SpaceSim.Models;

namespace SpaceSim;

public partial class SpaceSimGame
{
    #region Game loop

    /// <summary>
    /// The per-frame game loop: reads input, then dispatches to the active screen — the main menu, the
    /// sound dictionary, or the live sim (<see cref="UpdatePlaying"/>) — and finally keeps the
    /// spatial-audio engine and saved preferences in step.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var keys = Keyboard.GetState();

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
                UpdatePlaying(dt, keys);
                break;
        }

        // Keep the OpenAL spatial engine in step every frame, on any screen: match its master level
        // to NAudio's (so the volume keys line up) and reclaim finished sources. Game-thread only —
        // these calls must never run on the NAudio audio-callback thread.
        _openAl.SetMasterGain(_audio.MasterVolume);
        _openAl.Update();

        // Persist any changed preferences (debounced + async, so this never stalls the loop).
        UpdateSettingsPersistence();

        // Save previous input state
        _prevKeyState = keys;

        base.Update(gameTime);
    }

    /// <summary>
    /// Per-frame update while the simulation is running: global keys, ship input and update, and
    /// universe regeneration. Escape returns to the main menu.
    /// </summary>
    private void UpdatePlaying(float dt, KeyboardState keys)
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

        // Update celestial positions (orbital mechanics) BEFORE ship update
        // so the spatial grid and proximity queries use fresh positions.
        CelestialGenerator.UpdateCelestialPositions(_stars, _planets, _nebulae,
                                                     _ship.SimulationTime);
        _spatialGrid.Rebuild(_celestialBodies);
        _ship.SpatialGrid = _spatialGrid;

        _ship.HandleInput(keys, _prevKeyState, _stars, _planets, _nebulae);
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
            _ship.Temples = _temples;
            _ship.Pyramids = _pyramids;
            DebugLogger.Log("Init", "Universe regeneration completed");
        }

        // Audio click effect (periodic resonance feedback)
        UpdateClickEffect();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Visuals have been removed: the window is simply cleared each frame. The game is entirely
    /// audio- and screen-reader-driven, so no world, HUD, or menu is drawn — but MonoGame still
    /// requires a Draw pass and a live graphics device for the window to exist.
    /// </summary>
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
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
