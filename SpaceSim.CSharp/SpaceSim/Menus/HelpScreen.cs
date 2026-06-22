using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// An in-game, screen-reader-navigable beginner's guide and reference. Opened with F1 from anywhere (and
/// from a main-menu item); the player arrows through one line at a time and presses F1 or Escape to return
/// to whatever they were doing. Read-only — there are no actions to activate here.
/// </summary>
public sealed class HelpScreen : MenuScreen
{
    #region Content

    // A plain-language guide written for a first-time player who has never seen the game. Each line is a
    // self-contained thought so it reads naturally on its own when arrowed to; concepts come before the
    // keys that use them. Any key named here must stay in lockstep with Ship.Input.cs and SpaceSimGame.
    private static readonly string[] _lines =
    {
        // --- What this is ---
        "Welcome. This is a game you play entirely by sound. You pilot a light vehicle, an ancient Atlantean craft, through a wide universe made of five realms.",
        "There is nothing to look at and no steering wheel. You travel the way the Atlanteans were said to travel: by listening, and by tuning sound into harmony rather than burning fuel.",
        "Take your time. Nothing here punishes you for being slow. The game rewards calm, careful listening more than speed.",

        // --- How flying works ---
        "How you fly. Your craft has five drives, one per realm, and each makes a tone. The universe around you also has a tone in each realm. When your tone matches the universe's tone, that realm comes into resonance, and resonance is what carries you.",
        "So flying is really tuning. You raise and lower your five tones until they match what is around you. The closer the match, the more you resonate, and the faster and stronger you move.",
        "You need not tune all five by hand. In the normal mode your movement keys fly the first three realms for you, and you tune the upper two realms by ear. Press J to switch to full tuning, where you tune whichever realm you have selected.",

        // --- Listening to tune ---
        "Tuning by ear. As a tone nears its target you hear a pulsing beat, like two notes wavering against each other. The closer you get, the slower the pulse, until it smooths into one steady tone. A steady tone means that realm is locked.",
        "By default the game also speaks the exact tone to aim for. If you would rather tune purely by sound, press N for by-ear mode; then it only tells you how close you are: far, near, very close, or locked.",

        // --- The two things to watch ---
        "Two things to keep an ear on. Resonance is how well you are tuned right now; more resonance means more speed, more power, and access to the deeper parts of the game. Integrity is your craft's wellbeing; it frays in harsh, dissonant places and mends when you are in harmony.",

        // --- Tuning controls ---
        "Choosing a realm. Press the number keys 1 through 5 to pick which realm you are tuning.",
        "Tuning it. With a realm chosen, press Up to raise its tone and Down to lower it, listening for the beat to slow and steady.",
        "Press J to toggle full tuning of all five realms. Press Q to hear the selected realm, either the tone to aim for, or how close you are when in by-ear mode.",

        // --- Moving ---
        "Moving through space. W and S push you forward and back. A and D slide you left and right. Page Up and Page Down move you through the third realm, which you can treat as up and down.",
        "Left and Right arrows turn your heading. Since everything you hear is placed around you, turning changes which direction sounds come from, helping you face where you want to go.",
        "Z changes your speed between approach, cruise, and quantum. Use approach for gentle, careful movement, and quantum to cross long distances.",

        // --- Finding your way ---
        "Finding places. Press M to open the scanner. It lists everything near you, stars, planets, nebulae, temples, pyramids, and harmonic chambers, each with its distance and direction.",
        "In the scanner, arrow to something and press Enter to lock onto it. Your craft then steers itself toward it while a steady homing beat guides you in. Choose the unlock row to let a target go.",
        "Press R at any time for a spoken status report: your resonance, integrity, speed, heading, and crystals.",

        // --- Harmonic chambers ---
        "Harmonic chambers are shimmering doorways that carry you to fresh regions of the universe. Approach one, raise your resonance, line up with it, and hold E to charge through. Pressing E away from a chamber opens a list of the ones you have found.",

        // --- Planets, crystals, attunements ---
        "Planets. When you are near a planet and well tuned, press L to anchor, the Atlantean word for landing. On the surface you gather crystals.",
        "On a planet, W, A, S, and D move a cursor over the ground. Press F to be told where the nearest crystal lies, X to collect the one at your cursor, and T to rise back into space when you are done.",
        "Crystals are your craft's currency. While anchored, press U to open the attunement menu and spend crystals to permanently improve your craft: wider tuning, stronger integrity, more speed, and more.",
        "While flying, U instead reads your status panel aloud: your tones, resonance, integrity, and power.",

        // --- The Tuaoi crystal ---
        "The heart of your craft is the Tuaoi crystal, a great six-sided stone. Press G to turn it to its next face. Each face shapes the craft differently: one for healing, one for navigation, one for power, one for reaching the higher realms, and more.",

        // --- Consciousness and rest ---
        "As you spend time in harmony, your consciousness slowly rises through levels of awakening. The higher you rise, the more of the universe you can hear, as distant voices open up around you.",
        "If you tune into harmony with where you are and come to rest, holding steady resonance while nearly still, you settle into a regeneration bath. It heals your craft and deepens your consciousness while you simply rest in the tones. It is the gentlest, and one of the most powerful, things you can do.",

        // --- The journey / goal ---
        "Your journey. Twelve temples are scattered across the universe, each holding a key. Fly to a temple, tune a realm to its note, and you receive its key.",
        "Gather all twelve keys and raise your consciousness high enough, and the Halls of Amenti at the very center of the universe will open to you. Reaching them is the heart of the game.",
        "Pyramids are resonance chambers. Tune to a pyramid's note nearby and it will greatly heal and quicken you.",
        "Two paths lie open. The way of accumulation: gather crystals until your craft renews into a whole new universe, ever stronger. And the way of the One: raise your consciousness and gather the keys toward Amenti. Both are yours to walk.",

        // --- Special abilities ---
        "Special abilities. Press P to drop a portal anchor that marks your spot, and Shift plus P to travel back to it later.",
        "Press B for astral projection, leaving your craft to scout ahead in spirit, then B again to return.",
        "Press I to focus your intention on a locked target, gently drawing yourself toward it.",
        "Hold the Space bar with all five realms in near-perfect resonance to earn the water blessing: a full minute of healing and complete protection. It is hard to earn, and worth it.",

        // --- Favorite tunings ---
        "Saving favorite tunings. Hold Control and press a number from 1 to 9 to save your current five tones as a preset; hold Shift and press that number to recall them instantly. Handy for a temple's note or a healing tone.",

        // --- Comfort and accessibility ---
        "Comfort. Press V to change how much the game speaks, from quiet to detailed. Press Tab to repeat the last thing said. Press N to switch between exact tuning numbers and tuning by ear.",
        "Volume. Plus and minus raise and lower the overall volume. Hold Alt, Shift, or Control while pressing them to adjust the drive tones, the beeps, or the effects on their own.",

        // --- Saving and leaving ---
        "Saving your journey. Control plus S saves your game, Control plus L loads it, and Control plus A turns autosave on or off.",
        "Press F1 at any time to open or close this guide. Press Escape to return to the main menu.",
    };

    #endregion

    #region Construction & surface

    public HelpScreen(AudioSystem audio, Action<string> speak) : base(audio, speak) { }

    public override string Title => "Help and Guide";
    protected override IReadOnlyList<string> ItemLabels => _lines;

    protected override string EntryHint =>
        "Use up and down to read the guide, from what the game is through every control. Press F1 or Escape to return.";

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Escape))
            return ScreenTransition.CloseHelp;

        if (Pressed(keys, prev, Keys.Up)) MoveSelection(-1);
        else if (Pressed(keys, prev, Keys.Down)) MoveSelection(+1);
        else if (Pressed(keys, prev, Keys.Enter)) AnnounceSelection();  // re-read the current line

        return ScreenTransition.None;
    }

    #endregion
}
