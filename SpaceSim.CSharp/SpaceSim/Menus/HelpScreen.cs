using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim.Menus;

/// <summary>
/// An in-game, screen-reader-navigable beginner's guide and reference, opened with F1 from anywhere.
/// Modelled on the Aircraft Explorer information view: the player arrows through a short list of titled
/// topics — each announced as "n of N, Category, Title" — and presses Enter to hear the full topic read
/// aloud. That keeps the guide scannable, an overview first and detail on demand, instead of one long wall
/// of lines. Read-only; Escape or F1 returns to whatever the player was doing.
/// </summary>
public sealed class HelpScreen : MenuScreen
{
    #region Content

    private readonly record struct Topic(string Category, string Title, string Content);

    // Topics are grouped by category and ordered for a first-time player: what the game is, how to fly, how
    // to find your way, then planets, the deeper Atlantean path, and comfort/controls. Any key named here
    // must stay in lockstep with Ship.Input.cs and SpaceSimGame.
    private static readonly Topic[] _topics =
    {
        new("Welcome", "What this game is",
            "This is a game you play entirely by sound. You pilot a light vehicle, an ancient Atlantean craft, " +
            "through a wide universe made of five realms. There is nothing to look at and no steering wheel; you " +
            "travel the way the Atlanteans were said to travel, by listening and tuning sound into harmony rather " +
            "than burning fuel. Take your time. Nothing here punishes you for being slow; the game rewards calm, " +
            "careful listening more than speed. If you are new, choose Begin Tutorial from the main menu for a " +
            "gentle, hands-on, step-by-step walkthrough that guides you as you fly."),

        new("Flying", "How flying works",
            "Your craft has five drives, one per realm, and each makes a tone. The universe around you also has a " +
            "tone in each realm. When your tone matches the universe's tone, that realm comes into resonance, and " +
            "resonance is what carries you. So flying is really tuning: the closer the match, the more you resonate, " +
            "and the faster and stronger you move. The three lower realms are the ones that move you through space, " +
            "and your movement keys handle them for you; they keep themselves in tune as you fly, so you can travel " +
            "from the very first moment. The two higher realms are different. They are yours to tune by ear, and " +
            "tending them is how you deepen your resonance and open the deeper parts of the journey. The next topic " +
            "shows you how. And if you would rather tune all five realms by hand, press J for full tuning at any time."),

        new("Flying", "Tuning by ear",
            "Tuning the two higher realms is the heart of flying, and you tend them one at a time. Press 4 to take " +
            "up the fourth realm, or 5 to take up the fifth; Up and Down then tune whichever one you are holding, " +
            "and you move between them by pressing 4 and 5. As your tone nears its mark you will hear a pulsing " +
            "beat, like two notes wavering against each other; the closer you come, the slower the pulse, until it " +
            "smooths into one steady, single tone. That steady tone means the realm is locked. The tone sweeps " +
            "quickly when you are far, and slows under your hand of its own accord as you near the lock, so you can " +
            "settle gently onto the still center. The universe is alive and breathes, so these higher tones drift a " +
            "little, most of all when you slow down and grow quiet, which is simply the cosmos inviting you to " +
            "listen; a small nudge now and then keeps them tuned, and you need never chase them. Press Q to hear how " +
            "the selected realm sits. By default the game tells you the exact tone to aim for; if you would rather " +
            "tune purely by sound, press N for by-ear mode, and it will tell you only how close you are: far, near, " +
            "very close, or locked. And if you wish to tune all five realms by hand, press J for full tuning."),

        new("Flying", "Resonance and integrity",
            "Two things to keep an ear on. Resonance is how well you are tuned right now; more resonance means more " +
            "speed, more power, and access to the deeper parts of the game. Integrity is your craft's wellbeing; it " +
            "frays in harsh, dissonant places and mends when you are in harmony."),

        new("Flying", "Moving through space",
            "These keys carry you through the three lower realms, the ones your craft keeps in tune for you, so your " +
            "listening is free for the higher two. W and S push you forward and back. A and D slide you left and " +
            "right. Page Up and Page Down move you through the third realm, which you can treat as up and down. The " +
            "Left and Right arrows turn your heading; since everything you hear is placed around you, turning changes " +
            "which direction sounds come from, helping you face where you want to go. Z changes your speed between " +
            "approach, cruise, and quantum: use approach for gentle, careful movement, and quantum to cross long distances."),

        new("Navigation", "The scanner and locking on",
            "Press M to open the scanner. It lists what is around you: the Atlantean temples and pyramids come first, " +
            "since they are your goals, followed by stars, planets, nebulae, and harmonic chambers, nearest first, " +
            "each with its distance and direction. Arrow to something and press Enter to lock onto it. Your craft " +
            "then steers itself there while a steady homing beat guides you in. Choose the unlock row to let a " +
            "target go."),

        new("Navigation", "Orbiting an object",
            "Planets and stars drift along their own orbits, so once your craft reaches one it settles into an orbit " +
            "around it to keep you alongside. Press O to orbit your locked target at any time, and O again to break " +
            "orbit and fly free. While orbiting you stay in tune, so you can anchor a planet straight away."),

        new("Navigation", "Harmonic chambers",
            "Harmonic chambers are shimmering doorways that carry you to fresh regions of the universe. Approach one, " +
            "raise your resonance, line up with it, and hold E to charge through. Pressing E away from a chamber " +
            "opens a list of the ones you have found."),

        new("Navigation", "Status reports",
            "Press R at any time for a spoken status report: your resonance, integrity, speed, heading, and crystals."),

        new("Planets", "Anchoring and crystals",
            "When you are near a planet and well tuned, press L to anchor, the Atlantean word for landing. On the " +
            "surface, W, A, S, and D move a cursor over the ground. Press F to be told how many crystals the planet " +
            "holds and where the nearest one lies, X to collect the one at your cursor, and T to rise back into " +
            "space. Crystals are your craft's currency: while anchored, press U to open the attunement menu and " +
            "spend them to permanently improve your craft, with wider tuning, stronger integrity, more speed, and " +
            "more. While flying, U instead reads your status panel aloud."),

        new("The Atlantean Path", "The Tuaoi crystal",
            "The heart of your craft is the Tuaoi crystal, a great six-sided stone. Press G to turn it to its next " +
            "face. Each face shapes the craft differently: one for healing, one for navigation, one for power, one " +
            "for reaching the higher realms, and more."),

        new("The Atlantean Path", "Consciousness and rest",
            "As you spend time in harmony, your consciousness slowly rises through levels of awakening, and the " +
            "higher you rise, the more of the universe you can hear as distant voices open up around you. To rest, " +
            "come nearly to a stop with your realms in tune, including the two higher realms you tend by ear, which " +
            "is what truly opens the door. When you do, you settle into a regeneration bath that heals your craft " +
            "and deepens your consciousness while you simply rest in the tones. And as you rest, the breathing of " +
            "the tones settles with you, until they grow still; the longer you stay, the deeper the quiet. It is the " +
            "gentlest, and one of the most powerful, things you can do."),

        new("The Atlantean Path", "Your journey",
            "Twelve temples are scattered across the universe, each holding a key. Fly to a temple, tune a realm to " +
            "its note — press T near a temple to hear that note, or how close you already are — and you receive its " +
            "key. Gather all twelve keys and raise your consciousness high enough, and " +
            "the Halls of Amenti at the very center of the universe will open to you; reaching them is the heart of " +
            "the game. Pyramids are resonance chambers: tune to a pyramid's note nearby and it will greatly heal and " +
            "quicken you. Two paths lie open: the way of accumulation, gathering crystals until your craft renews " +
            "into a whole new universe, and the way of the One, raising your consciousness and gathering the keys " +
            "toward Amenti. Both are yours to walk."),

        new("The Atlantean Path", "Special abilities",
            "Press P to drop a portal anchor that marks your spot, and Shift plus P to travel back to it later. " +
            "Press B for astral projection, leaving your craft to scout ahead in spirit, then B again to return. " +
            "Press I to focus your intention on a locked target, gently drawing yourself toward it. And hold the " +
            "Space bar with all five realms in near-perfect resonance to earn the water blessing: a full minute of " +
            "healing and complete protection. It is hard to earn, and worth it."),

        new("Comfort", "Message buffers",
            "The game sorts what it says into buffers: All, Navigation, Atlantean, and System. Tuning and other " +
            "essentials are always spoken, whichever buffer you are in. Press the left and right bracket keys to " +
            "move between buffers; when you focus a single one, only its messages are spoken as they happen. Press " +
            "comma and period to step back and forward through the messages already in the focused buffer, so you " +
            "can re-read anything you missed. Hold Control and press a bracket to move the focused buffer earlier or " +
            "later in the list, arranging them to suit you."),

        new("Comfort", "Comfort, saving, and help",
            "Press V to change how much the game speaks, from quiet to detailed. Press Tab to repeat the last thing " +
            "said. Press N to switch between exact tuning numbers and tuning by ear. Plus and minus raise and lower " +
            "the overall volume; hold Alt, Shift, or Control while pressing them to adjust the drive tones, the " +
            "beeps, or the effects on their own. Hold Control and press a number from 1 to 9 to save your current " +
            "five tones as a preset, and Shift plus that number to recall them. Control plus S saves your game, " +
            "Control plus L loads it, and Control plus A turns autosave on or off. Press F3 to choose which sound " +
            "device the simulator plays through. Press F1 any time to open or close this guide, and Escape to " +
            "return to the main menu."),
    };

    // Browse labels: "Category: Title", what the player hears (with a position prefix) while arrowing.
    private static readonly string[] _labels = BuildLabels();

    private static string[] BuildLabels()
    {
        var labels = new string[_topics.Length];
        for (int i = 0; i < _topics.Length; i++)
            labels[i] = $"{_topics[i].Category}: {_topics[i].Title}";
        return labels;
    }

    #endregion

    #region Construction & surface

    public HelpScreen(AudioSystem audio, Action<string> speak) : base(audio, speak) { }

    public override string Title => "Help and Guide";
    protected override IReadOnlyList<string> ItemLabels => _labels;

    protected override string EntryHint =>
        $"{_topics.Length} topics. Use up and down to browse, Enter to read a topic aloud, R to open it in a text window, F1 or Escape to return.";

    // Announce position, category, and title on each move (the Aircraft Explorer pattern) so the player
    // always knows where they are and what a topic covers before choosing to read it.
    protected override void AnnounceSelection()
        => Speak($"{SelectedIndex + 1} of {_topics.Length}. {_labels[SelectedIndex]}.");

    #endregion

    #region Input

    public override ScreenTransition HandleInput(KeyboardState keys, KeyboardState prev)
    {
        if (Pressed(keys, prev, Keys.Escape))
            return ScreenTransition.CloseHelp;

        if (Pressed(keys, prev, Keys.Up)) MoveSelection(-1);
        else if (Pressed(keys, prev, Keys.Down)) MoveSelection(+1);
        else if (Pressed(keys, prev, Keys.Enter))
            Speak($"{_topics[SelectedIndex].Title}. {_topics[SelectedIndex].Content}");
        else if (Pressed(keys, prev, Keys.R))
            OpenInReader();

        return ScreenTransition.None;
    }

    /// <summary>Opens the highlighted topic in a read-only WinForms text window for screen-reader review.</summary>
    private void OpenInReader()
    {
        var t = _topics[SelectedIndex];
        using var form = new TopicReaderForm(t.Title, $"{t.Title}\r\n{t.Category}\r\n\r\n{t.Content}");
        form.ShowDialog();
    }

    #endregion
}
