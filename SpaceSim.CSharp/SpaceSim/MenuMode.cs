using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim;

/// <summary>
/// One screen-reader menu the player can be inside (HUD, upgrades, starmap, rift selection).
///
/// <para>
/// This replaces the old design of four parallel boolean flags
/// (<c>HudMode</c>/<c>UpgradeMode</c>/<c>StarmapMode</c>/<c>RiftSelectionMode</c>) plus a
/// stringly-typed <c>mode == "starmap"</c> dispatch. The <see cref="Ship"/> now holds a single
/// <c>ActiveMenu</c> (null when none is open) and the input loop just talks to it polymorphically.
/// </para>
///
/// <para>
/// The concrete menus are thin strategies: the per-menu data (item lists, indices) still lives on
/// the Ship and the heavy population/selection logic stays in <c>Ship.UI</c>, so this change is
/// purely about <em>dispatch</em> — behaviour is unchanged.
/// </para>
/// </summary>
public abstract class MenuMode
{
    #region Construction

    /// <summary>The ship this menu reads from and acts on.</summary>
    protected readonly Ship Ship;

    protected MenuMode(Ship ship) => Ship = ship;

    #endregion

    #region Per-menu contract

    /// <summary>Number of rows currently in the menu.</summary>
    public abstract int Count { get; }

    /// <summary>The row labels, for the renderer to draw.</summary>
    public abstract IReadOnlyList<string> ItemLabels { get; }

    /// <summary>The highlighted row index (backed by the ship's per-menu index field).</summary>
    public abstract int SelectedIndex { get; set; }

    /// <summary>The key that closes this menu.</summary>
    public abstract Keys ExitKey { get; }

    /// <summary>What the screen reader announces when this menu closes.</summary>
    public abstract string ExitMessage { get; }

    /// <summary>(Re)build the menu's rows.</summary>
    public abstract void Populate();

    /// <summary>Announce the currently highlighted row.</summary>
    public abstract void SpeakCurrent();

    /// <summary>Act on the highlighted row (Enter). Default: nothing (e.g. the read-only HUD).</summary>
    public virtual void Select() { }

    /// <summary>Hook for menu-specific keys beyond Up/Down/Enter (e.g. starmap first-letter jump).</summary>
    public virtual void HandleExtraKeys(Func<Keys, bool> isPressed) { }

    #endregion

    #region Shared navigation

    /// <summary>Move the highlight up one row (wrapping), announcing the new row. No-op for single-row menus.</summary>
    public void MoveUp()
    {
        if (Count <= 1) return;
        SelectedIndex = (SelectedIndex - 1 + Count) % Count;
        SpeakCurrent();
    }

    /// <summary>Move the highlight down one row (wrapping), announcing the new row. No-op for single-row menus.</summary>
    public void MoveDown()
    {
        if (Count <= 1) return;
        SelectedIndex = (SelectedIndex + 1) % Count;
        SpeakCurrent();
    }

    #endregion
}

/// <summary>The status read-out menu (U). Read-only — Enter does nothing. Shares storage with the HUD overlay.</summary>
public sealed class HudMenuMode : MenuMode
{
    public HudMenuMode(Ship ship) : base(ship) { }

    public override int Count => Ship.HudItems.Count;
    public override IReadOnlyList<string> ItemLabels => Ship.HudItems;
    public override int SelectedIndex { get => Ship.HudIndex; set => Ship.HudIndex = value; }
    public override Keys ExitKey => Keys.U;
    public override string ExitMessage => "Exiting menu.";

    public override void Populate() => Ship.UpdateHudItems(upgrade: false);
    public override void SpeakCurrent() => Ship.SpeakHudItem();
}

/// <summary>The attunement (upgrade) menu (U, when landed with all crystals). Enter applies the selected upgrade.</summary>
public sealed class UpgradeMenuMode : MenuMode
{
    public UpgradeMenuMode(Ship ship) : base(ship) { }

    public override int Count => Ship.HudItems.Count;
    public override IReadOnlyList<string> ItemLabels => Ship.HudItems;
    public override int SelectedIndex { get => Ship.HudIndex; set => Ship.HudIndex = value; }
    public override Keys ExitKey => Keys.U;
    public override string ExitMessage => "Exiting menu.";

    public override void Populate() => Ship.UpdateHudItems(upgrade: true);
    public override void SpeakCurrent() => Ship.SpeakHudItem();
    public override void Select() => Ship.ApplyUpgrade();
}

/// <summary>The starmap scanner menu (M). Enter locks on to the selected object; supports A–Z first-letter jump.</summary>
public sealed class StarmapMenuMode : MenuMode
{
    public StarmapMenuMode(Ship ship) : base(ship) { }

    public override int Count => Ship.StarmapItems.Count;
    public override IReadOnlyList<string> ItemLabels => Ship.StarmapItems.Select(s => s.Label).ToList();
    public override int SelectedIndex { get => Ship.StarmapIndex; set => Ship.StarmapIndex = value; }
    public override Keys ExitKey => Keys.M;
    public override string ExitMessage => "Exiting starmap.";

    public override void Populate() => Ship.UpdateStarmapItems(Ship.Stars, Ship.Planets, Ship.Nebulae);
    public override void SpeakCurrent() => Ship.SpeakStarmapItem();
    public override void Select() => Ship.LockOnStarmapItem();

    public override void HandleExtraKeys(Func<Keys, bool> isPressed)
    {
        // Jump to the first row whose label starts with the pressed letter (one letter per frame).
        for (Keys k = Keys.A; k <= Keys.Z; k++)
        {
            if (!isPressed(k)) continue;

            char ch = (char)('a' + (k - Keys.A));
            for (int idx = 0; idx < Ship.StarmapItems.Count; idx++)
            {
                if (Ship.StarmapItems[idx].Label.Length > 0 &&
                    char.ToLower(Ship.StarmapItems[idx].Label[0]) == ch)
                {
                    Ship.StarmapIndex = idx;
                    Ship.SpeakStarmapItem();
                    break;
                }
            }
            break;
        }
    }
}

/// <summary>The rift selection menu (E, when not already locked on a rift). Enter locks on the selected rift.</summary>
public sealed class RiftMenuMode : MenuMode
{
    public RiftMenuMode(Ship ship) : base(ship) { }

    public override int Count => Ship.RiftItems.Count;
    public override IReadOnlyList<string> ItemLabels => Ship.RiftItems.Select(r => r.Label).ToList();
    public override int SelectedIndex { get => Ship.RiftSelectionIndex; set => Ship.RiftSelectionIndex = value; }
    public override Keys ExitKey => Keys.E;
    public override string ExitMessage => "Exiting Harmonic Chamber selection.";

    public override void Populate() => Ship.UpdateRiftItems();
    public override void SpeakCurrent() => Ship.SpeakRiftItem();
    public override void Select() => Ship.LockOnRiftItem();
}

/// <summary>The portal-anchor pick-list (Shift+P). Enter teleports to the selected anchor; P closes it.
/// Lets the player reach ANY of their anchors (the old one-key teleport only ever used the first).</summary>
public sealed class PortalMenuMode : MenuMode
{
    public PortalMenuMode(Ship ship) : base(ship) { }

    public override int Count => Ship.PortalItems.Count;
    public override IReadOnlyList<string> ItemLabels => Ship.PortalItems.Select(p => p.Label).ToList();
    public override int SelectedIndex { get => Ship.PortalSelectionIndex; set => Ship.PortalSelectionIndex = value; }
    public override Keys ExitKey => Keys.P;
    public override string ExitMessage => "Exiting portal anchors.";

    public override void Populate() => Ship.UpdatePortalItems();
    public override void SpeakCurrent() => Ship.SpeakPortalItem();
    public override void Select() => Ship.TeleportToSelectedAnchor();
}
