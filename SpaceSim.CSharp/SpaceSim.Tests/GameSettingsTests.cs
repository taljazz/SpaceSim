using System.Text.Json;
using SpaceSim;
using SpaceSim.Models;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="GameSettings"/> persistence. Preferences are round-tripped through JSON the
/// same way <see cref="SettingsStore"/> does, so every field must survive serialization — this guards
/// against a future field that isn't a public get/set (which JSON would silently drop).
/// </summary>
public class GameSettingsTests
{
    [Fact]
    public void Defaults_MatchLiveCodeDefaults()
    {
        var s = new GameSettings();

        Assert.Equal(0.2f, s.MasterVolume);
        Assert.Equal(0.3f, s.BeepVolume);
        Assert.Equal(0.2f, s.EffectVolume);
        Assert.Equal(0.05f, s.DriveVolume);
        Assert.Equal(1, s.VerboseMode);
        Assert.False(s.ByEarMode);
        Assert.True(s.AutosaveEnabled);
        Assert.True(s.AmbientSoundsEnabled);
        Assert.True(s.NebulaDissonanceEnabled);
    }

    [Fact]
    public void RoundTrip_PreservesAllNonDefaultValues()
    {
        var original = new GameSettings
        {
            MasterVolume = 0.55f,
            BeepVolume = 0.4f,
            EffectVolume = 0.6f,
            DriveVolume = 0.12f,
            VerboseMode = 2,
            ByEarMode = true,
            AutosaveEnabled = false,
            AmbientSoundsEnabled = false,
            NebulaDissonanceEnabled = false,
        };

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<GameSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.MasterVolume, restored!.MasterVolume);
        Assert.Equal(original.BeepVolume, restored.BeepVolume);
        Assert.Equal(original.EffectVolume, restored.EffectVolume);
        Assert.Equal(original.DriveVolume, restored.DriveVolume);
        Assert.Equal(original.VerboseMode, restored.VerboseMode);
        Assert.Equal(original.ByEarMode, restored.ByEarMode);
        Assert.Equal(original.AutosaveEnabled, restored.AutosaveEnabled);
        Assert.Equal(original.AmbientSoundsEnabled, restored.AmbientSoundsEnabled);
        Assert.Equal(original.NebulaDissonanceEnabled, restored.NebulaDissonanceEnabled);
    }

    // A missing/partial file must not throw and must keep code defaults for absent fields — this is
    // what makes a first run (or a settings file from an older build) behave exactly as before.
    [Fact]
    public void Deserialize_PartialJson_KeepsDefaultsForMissingFields()
    {
        var restored = JsonSerializer.Deserialize<GameSettings>("{\"MasterVolume\":0.9}");

        Assert.NotNull(restored);
        Assert.Equal(0.9f, restored!.MasterVolume);
        Assert.Equal(0.3f, restored.BeepVolume);          // untouched -> default
        Assert.True(restored.AutosaveEnabled);            // untouched -> default
        Assert.True(restored.AmbientSoundsEnabled);       // untouched -> default
    }
}
