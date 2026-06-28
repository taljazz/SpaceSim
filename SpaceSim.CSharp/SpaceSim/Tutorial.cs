using System;
using Microsoft.Xna.Framework.Input;

namespace SpaceSim;

/// <summary>
/// An interactive, hands-on beginner tutorial that runs <em>inside the live sim</em>. Each step speaks
/// one instruction, then watches the player's real actions and ship state; when they actually do the
/// thing, it confirms and moves on. Enter continues an explanation or skips an action; Escape (handled
/// by the host) leaves the tutorial. It is deliberately gentle and audio-only, like the rest of the game.
///
/// <para>
/// The tutorial is owned and ticked by <see cref="SpaceSimGame"/> while playing (so it sees menu state
/// like the scanner too). It only reads public ship state and speaks through <see cref="Ship.Speak"/>,
/// so its lines land in the speech buffer and can be repeated with Tab.
/// </para>
/// </summary>
public sealed class Tutorial
{
    #region Step model

    /// <summary>One tutorial step: an instruction, a completion test, and presentation options.</summary>
    private sealed class Step
    {
        public string Instruction = "";
        /// <summary>True when the player has accomplished this step (ship state + edge-triggered keys).</summary>
        public Func<Ship, Func<Keys, bool>, bool> IsDone = (_, _) => false;
        /// <summary>Spoken when the step is completed by doing it (not when skipped).</summary>
        public string? DoneMessage;
        /// <summary>A pure explanation that only advances on Enter (no action to perform).</summary>
        public bool IsInfo;
        /// <summary>When false, Enter will NOT skip this step — used for the scanner-lock step, where Enter
        /// is the menu's own select key and must not also advance the tutorial.</summary>
        public bool AllowSkip = true;
        /// <summary>Minimum seconds the instruction is given before it can complete (so it is heard first).</summary>
        public float MinTime = 1.2f;
        /// <summary>Seconds of no progress before the instruction (or <see cref="Hint"/>) is repeated.</summary>
        public float HintInterval = 14f;
        /// <summary>Optional shorter reminder spoken on the re-hint instead of the full instruction.</summary>
        public string? Hint;
    }

    #endregion

    #region State

    private readonly Step[] _steps;
    private int _index;
    private bool _announced;
    private float _elapsed;
    private float _sinceHint;
    private bool _pendingDone;
    private float _gap;
    private string? _lastLine;

    /// <summary>Seconds of pause between a step's confirmation and the next step's instruction.</summary>
    private const float StepGap = 2.5f;

    /// <summary>True once every step is done (the tutorial then goes quiet and the player flies on).</summary>
    public bool Finished { get; private set; }

    #endregion

    public Tutorial() => _steps = BuildSteps();

    #region Lifecycle

    /// <summary>
    /// Prepare the ship for the tutorial: detune the two higher realms so the by-ear tuning steps are
    /// genuinely hands-on (a fresh ship seeds them already locked). The offset stays inside the cue's
    /// coarse range so the new closeness/direction cue can guide the player back in.
    /// </summary>
    public void Begin(Ship ship)
    {
        ship.RDrive[3] = DetuneFromCentre(ship.BaseFTarget[3]);
        ship.RDrive[4] = DetuneFromCentre(ship.BaseFTarget[4]);
    }

    /// <summary>Offset a higher realm ~30 Hz toward the band interior so the clamp never eats the detune near a
    /// band edge (which would auto-complete a hands-on tuning step). Always lands well outside the still band.</summary>
    private static float DetuneFromCentre(float centre)
    {
        float mid = (GameConstants.FrequencyMin + GameConstants.FrequencyMax) * 0.5f;
        float dir = centre > mid ? -1f : 1f;
        return Math.Clamp(centre + dir * 30f, GameConstants.FrequencyMin, GameConstants.FrequencyMax);
    }

    /// <summary>Per-frame tick (called by the host while playing). Announces, tests completion, and advances.</summary>
    public void Update(Ship ship, KeyboardState keys, KeyboardState prev, float dt)
    {
        bool Pressed(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);

        // Shift+T repeats the last tutorial line on demand, cutting through any speech noise that buried it.
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        if (shift && Pressed(Keys.T))
        {
            if (_lastLine != null) ship.SpeakTutorial(_lastLine, interrupt: true);
            return;
        }

        if (Finished) return;

        if (!_announced)
        {
            // A gap between steps so a step's confirmation isn't immediately followed by the next instruction.
            if (_gap > 0f) { _gap -= dt; return; }
            Say(ship, _steps[_index].Instruction);
            _announced = true;
            _elapsed = 0f;
            _sinceHint = 0f;
            _pendingDone = false;
            return;
        }

        _elapsed += dt;
        _sinceHint += dt;
        Step step = _steps[_index];

        // Evaluate completion EVERY frame and latch it, so an edge-triggered keypress that lands during the
        // MinTime window (before the instruction has finished) is never silently dropped.
        if (step.IsDone(ship, Pressed)) _pendingDone = true;

        if (_elapsed >= step.MinTime)
        {
            bool advanceByEnter = step.AllowSkip && Pressed(Keys.Enter);
            if (_pendingDone || advanceByEnter)
            {
                if (_pendingDone && step.DoneMessage != null) Say(ship, step.DoneMessage);
                else if (advanceByEnter && !_pendingDone && !step.IsInfo) Say(ship, "Skipping ahead.");
                Advance(ship);
                return;
            }
        }

        // Re-hint if stuck — but never while a menu is open, where it would cut off the player's menu
        // navigation announcements (the tutorial keeps ticking through menus to watch for the scanner/lock).
        if (_sinceHint >= step.HintInterval && !ship.IsInMenuMode)
        {
            Say(ship, step.Hint ?? step.Instruction);
            _sinceHint = 0f;
        }
    }

    /// <summary>Speak one tutorial line (non-interrupting, so consecutive lines queue and play in order) and
    /// remember it so Shift+T can repeat it.</summary>
    private void Say(Ship ship, string line)
    {
        _lastLine = line;
        ship.SpeakTutorial(line);
    }

    private void Advance(Ship ship)
    {
        _index++;
        _announced = false;
        _pendingDone = false;
        _gap = StepGap;   // brief pause before the next step's instruction
        if (_index >= _steps.Length)
        {
            Finished = true;
            Say(ship, "Tutorial complete. The universe is yours to explore. Press F1 any time for the full guide. Safe travels, pilot.");
        }
    }

    #endregion

    #region Steps

    private static Step[] BuildSteps() => new[]
    {
        new Step
        {
            IsInfo = true, MinTime = 4f,
            Instruction = "Welcome to the hands-on tutorial. I will walk you through flying the craft, step by step, " +
                          "and we will move on once you have done each thing. Press Enter to continue, or to skip any step. " +
                          "Press Shift plus T at any time to repeat what I last said. Press Escape to leave the tutorial. " +
                          "Press Enter when you are ready.",
        },
        new Step
        {
            Instruction = "First, flying. Press and hold W to glide forward through space. Try it now.",
            Hint = "Press and hold W to move forward.",
            DoneMessage = "Good — you are moving.",
            // SPATIAL speed only: the higher realms are detuned for the later tuning steps, which drifts the
            // ship through dims 4-5 — that is not the player flying. Only WASD thrust moves dims 0-2.
            IsDone = (s, _) => s.SpatialSpeed > s.MaxVelocity * 0.1f,
        },
        new Step
        {
            Instruction = "Tap the Left and Right arrow keys to turn your heading. Everything you hear is placed around " +
                          "you, so turning changes which direction the sounds come from.",
            Hint = "Tap the Left or Right arrow key to turn.",
            DoneMessage = "Well done. That is how you face where you want to go.",
            IsDone = (_, p) => p(Keys.Left) || p(Keys.Right),
        },
        new Step
        {
            IsInfo = true, MinTime = 3f,
            Instruction = "Your craft flies on five realms. The lower three steer themselves as you fly. The two higher " +
                          "realms, four and five, are yours to tune by ear. Let us tune them. Press Enter to continue.",
        },
        new Step
        {
            Instruction = "Press 4 to take up the fourth realm. Then hold Up or Down and listen. The pulsing slows as you " +
                          "near its note; the sound centers between your ears at the lock, and leans left if you are low, " +
                          "right if you are high. Tune realm four until it locks.",
            Hint = "Press 4, then hold Up or Down. Aim for a slow pulse, centered between your ears.",
            DoneMessage = "Realm four is locked. Hear how steady and centered it sounds.",
            HintInterval = 18f,
            IsDone = (s, _) => MathF.Abs(s.RDrive[3] - s.BaseFTarget[3]) < GameConstants.HigherRealmStillBand,
        },
        new Step
        {
            Instruction = "Now press 5 and tune the fifth realm the same way, by ear, until it locks.",
            Hint = "Press 5, then hold Up or Down until the pulse slows and the tone centers.",
            DoneMessage = "Both higher realms in tune. That is the heart of flying.",
            HintInterval = 18f,
            IsDone = (s, _) => MathF.Abs(s.RDrive[4] - s.BaseFTarget[4]) < GameConstants.HigherRealmStillBand,
        },
        new Step
        {
            Instruction = "With your realms in tune, ease off the controls and come to a near stop. Hold still in the " +
                          "resonance, and a regeneration bath will form around you to restore your craft.",
            Hint = "Stop moving and hold still while your realms stay in tune; the bath forms after a few seconds.",
            DoneMessage = "You are resting in the bath. Resting in harmony is the gentlest, and one of the most " +
                          "powerful, things you can do.",
            HintInterval = 20f,
            IsDone = (s, _) => s.InRegeneration,
        },
        new Step
        {
            Instruction = "Now let us find a destination. Press M to open the scanner. It lists the stars, planets, " +
                          "temples, and other places around you.",
            Hint = "Press M to open the scanner.",
            // No DoneMessage: opening the scanner speaks its first row, which is the real confirmation
            // (a tutorial line here would interrupt and clobber it).
            IsDone = (_, p) => p(Keys.M),
        },
        new Step
        {
            Instruction = "In the scanner, use Up and Down to browse the list, then press Enter to lock onto a place. " +
                          "Your craft will then steer itself there.",
            Hint = "Open the scanner with M if it is closed, then arrow to a place and press Enter to lock onto it.",
            DoneMessage = "Locked on. Your craft is steering itself there now.",
            AllowSkip = false, // Enter is the menu's select key here; it must not also skip the step
            MinTime = 2f, HintInterval = 20f,
            // Require being out of the scanner too, so "steering itself there now" lands once steering can
            // actually begin (the autopilot only runs after the menu closes).
            IsDone = (s, _) => s.LockedTarget != null && !s.IsInMenuMode,
        },
        new Step
        {
            Instruction = "Press G to turn the Tuaoi crystal to its next face. Each face shapes your craft differently: " +
                          "healing, navigation, power, reaching the higher realms, and more.",
            Hint = "Press G to turn the Tuaoi crystal.",
            // No DoneMessage: turning the Tuaoi already announces its new face — let that be the confirmation.
            IsDone = (_, p) => p(Keys.G),
        },
        new Step
        {
            Instruction = "Press R for a spoken status report: your resonance, integrity, speed, heading, and crystals.",
            Hint = "Press R for your status report.",
            // No DoneMessage: R speaks the full status, which is the real confirmation.
            IsDone = (_, p) => p(Keys.R),
        },
        new Step
        {
            IsInfo = true, MinTime = 4f,
            Instruction = "Last, your journey. Twelve temples are scattered across the universe, each holding a key. " +
                          "Fly near a temple, tune a realm to its note, and claim its key — press T near a temple to hear " +
                          "that note. Gather all twelve keys and raise your awareness, and the Halls of Amenti at the very " +
                          "center will open to you. Press Enter to finish.",
        },
    };

    #endregion
}
