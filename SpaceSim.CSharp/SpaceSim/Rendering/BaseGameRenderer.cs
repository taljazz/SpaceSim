using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SpaceSim.Models;

namespace SpaceSim.Rendering;

/// <summary>
/// Abstract base class for game renderers. Provides shared world data references,
/// color lookup helpers, and logging infrastructure used by both 2D and 3D renderers.
/// </summary>
public abstract class BaseGameRenderer : IGameRenderer
{
    protected GraphicsDevice Device = null!;

    // World data references (set by SpaceSimGame)
    public List<CelestialBody>? Stars;
    public List<CelestialBody>? Planets;
    public List<CelestialBody>? Nebulae;
    public List<Temple>? Temples;
    public List<LeyLine>? LeyLines;
    public List<Pyramid>? Pyramids;

    // Logging throttle
    protected int FrameCount;
    protected double LastLogTime;

    // =========================================================================
    //  INITIALIZATION (Template Method pattern)
    // =========================================================================

    public void Initialize(GraphicsDevice device, ContentManager content)
    {
        Device = device;
        OnInitialize(content);
        DebugLogger.Log("Render", $"{GetType().Name} initialized");
    }

    /// <summary>
    /// Called during initialization for renderer-specific setup (e.g. creating
    /// primitive renderers, loading shaders, etc.).
    /// </summary>
    protected abstract void OnInitialize(ContentManager content);

    // =========================================================================
    //  ABSTRACT DRAWING METHODS
    // =========================================================================

    public abstract void DrawWorld(SpriteBatch spriteBatch, Ship ship, GameTime gameTime, int screenW, int screenH);
    public abstract void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Ship ship, int screenW, int screenH);

    // =========================================================================
    //  SHARED COLOR HELPERS
    // =========================================================================

    protected static Color GetStellarColor(StellarType? stellarClass)
    {
        if (stellarClass == null) return Color.Yellow;
        if (GameConstants.StellarTypes.TryGetValue(stellarClass.Value, out var info))
            return info.Color;
        return Color.Yellow;
    }

    protected static Color GetPlanetColor(ExoplanetType? exoplanetClass)
    {
        return exoplanetClass switch
        {
            ExoplanetType.HotJupiter => new Color(255, 120, 50),
            ExoplanetType.SuperEarth => new Color(100, 180, 100),
            ExoplanetType.OceanWorld => new Color(50, 100, 200),
            ExoplanetType.RoguePlanet => new Color(80, 80, 100),
            ExoplanetType.IceGiant => new Color(150, 200, 255),
            _ => Color.Gray,
        };
    }

    protected static Color GetNebulaColor(NebulaType? nebulaClass)
    {
        if (nebulaClass == null) return new Color(100, 50, 150);
        if (GameConstants.NebulaTypes.TryGetValue(nebulaClass.Value, out var info))
            return info.Color;
        return new Color(100, 50, 150);
    }

    protected static Color GetTuaoiColor(TuaoiMode tuaoiMode)
    {
        if (GameConstants.TuaoiModes.TryGetValue(tuaoiMode, out var info))
            return info.Color;
        return Color.Cyan;
    }
}
