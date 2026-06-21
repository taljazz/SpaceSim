using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpaceSim.Models;

/// <summary>
/// Serializable save-game state for JSON persistence.
/// Contains all ship state that needs to survive between sessions.
/// </summary>
public class SaveGameState
{
    // Core ship state
    /// <summary>The ship's 5D position.</summary>
    public float[] Position { get; set; } = Vec5.Zero();

    /// <summary>The ship's 5D velocity.</summary>
    public float[] Velocity { get; set; } = Vec5.Zero();

    /// <summary>Current drive frequencies per dimension.</summary>
    public float[] RDrive { get; set; } = Vec5.Zero();

    /// <summary>The base target frequencies the drives aim for.</summary>
    public float[] BaseFTarget { get; set; } = Vec5.Zero();

    /// <summary>Ship hull/health, 0..1.</summary>
    public float ResonanceIntegrity { get; set; } = 1f;

    /// <summary>Total crystals the player has collected.</summary>
    public int CrystalsCollected { get; set; }

    /// <summary>Tuning tolerance — wider makes resonance easier (upgradeable).</summary>
    public float ResonanceWidth { get; set; } = GameConstants.ResonanceWidthBase;

    /// <summary>Maximum travel speed (upgradeable).</summary>
    public float MaxVelocity { get; set; } = GameConstants.MaxVelocityBase;

    /// <summary>Bonus crystals granted per planet from the Crystal Growth upgrade.</summary>
    public int CrystalBonus { get; set; }

    /// <summary>Whether the Golden Harmony upgrade (PHI multiplier to all stats) is active.</summary>
    public bool GoldenHarmonyActive { get; set; }

    // Frequency presets (slot -> 5 frequencies)
    /// <summary>Saved frequency presets, keyed by slot (Ctrl+1-9 to save, Shift+1-9 to recall).</summary>
    public Dictionary<int, float[]> FrequencyPresets { get; set; } = new();

    // Atlantean state
    /// <summary>The currently selected Tuaoi Crystal mode.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TuaoiMode TuaoiMode { get; set; } = TuaoiMode.Navigation;

    /// <summary>Index of the current Tuaoi mode within the cycle.</summary>
    public int TuaoiModeIndex { get; set; } = 1;

    /// <summary>Raw consciousness progress value, 0..1.</summary>
    public float ConsciousnessValue { get; set; } = 0.3f;

    /// <summary>The consciousness level <see cref="ConsciousnessValue"/> currently maps to.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConsciousnessLevel ConsciousnessStage { get; set; } = ConsciousnessLevel.Awakening;

    /// <summary>Indices of the temple keys collected so far (all 12 unlock the Halls of Amenti).</summary>
    public List<int> TempleKeys { get; set; } = new();

    /// <summary>Whether the player has reached the Halls of Amenti.</summary>
    public bool VisitedAmenti { get; set; }

    /// <summary>Whether the permanent Amenti blessing (stat boosts) is active.</summary>
    public bool AmentiBlessingActive { get; set; }

    // Portal anchors
    /// <summary>The player's saved portal anchors.</summary>
    public List<PortalAnchorState> PortalAnchors { get; set; } = new();

    // Upgrade tracking
    /// <summary>Speech verbosity level (0 low, 1 medium, 2 high).</summary>
    public int VerboseMode { get; set; } = 1;

    /// <summary>On-screen HUD text size.</summary>
    public int HudTextSize { get; set; } = GameConstants.HudTextSizeBase;

    /// <summary>Whether high-contrast rendering is enabled.</summary>
    public bool HighContrast { get; set; }

    /// <summary>Whether the game autosaves.</summary>
    public bool AutosaveEnabled { get; set; } = true;

    /// <summary>Whether proximity-based ambient sounds play.</summary>
    public bool AmbientSoundsEnabled { get; set; } = true;

    /// <summary>Whether nebula dissonance effects (frequency drift, turbulence) are active.</summary>
    public bool NebulaDissonanceEnabled { get; set; } = true;
}

/// <summary>
/// Serializable portal anchor for save state.
/// </summary>
public class PortalAnchorState
{
    /// <summary>The anchor's saved 5D position.</summary>
    public float[] Position { get; set; } = Vec5.Zero();

    /// <summary>The anchor's display name.</summary>
    public string Name { get; set; } = "";
}
