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
    public float[] Position { get; set; } = Vec5.Zero();
    public float[] Velocity { get; set; } = Vec5.Zero();
    public float[] RDrive { get; set; } = Vec5.Zero();
    public float[] BaseFTarget { get; set; } = Vec5.Zero();
    public float ResonanceIntegrity { get; set; } = 1f;
    public int CrystalsCollected { get; set; }
    public float ResonanceWidth { get; set; } = GameConstants.ResonanceWidthBase;
    public float MaxVelocity { get; set; } = GameConstants.MaxVelocityBase;
    public int CrystalBonus { get; set; }
    public bool GoldenHarmonyActive { get; set; }

    // Frequency presets (slot -> 5 frequencies)
    public Dictionary<int, float[]> FrequencyPresets { get; set; } = new();

    // Atlantean state
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TuaoiMode TuaoiMode { get; set; } = TuaoiMode.Navigation;
    public int TuaoiModeIndex { get; set; } = 1;
    public float ConsciousnessValue { get; set; } = 0.3f;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConsciousnessLevel ConsciousnessStage { get; set; } = ConsciousnessLevel.Awakening;
    public List<int> TempleKeys { get; set; } = new();
    public bool VisitedAmenti { get; set; }
    public bool AmentiBlessingActive { get; set; }

    // Portal anchors
    public List<PortalAnchorState> PortalAnchors { get; set; } = new();

    // Upgrade tracking
    public int VerboseMode { get; set; } = 1;
    public int HudTextSize { get; set; } = GameConstants.HudTextSizeBase;
    public bool HighContrast { get; set; }
    public bool AutosaveEnabled { get; set; } = true;
    public bool AmbientSoundsEnabled { get; set; } = true;
    public bool NebulaDissonanceEnabled { get; set; } = true;
}

/// <summary>
/// Serializable portal anchor for save state.
/// </summary>
public class PortalAnchorState
{
    public float[] Position { get; set; } = Vec5.Zero();
    public string Name { get; set; } = "";
}
