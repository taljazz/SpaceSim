# Tuning Hybrid Spec — "Tune-by-ear-and-fly"

**Created:** 2026-06-27
**Status:** Approved & locked. Implementation starting.
**North star:** See memory `project_hidden_purpose.md`. The sim covertly brings players to resonance / present-moment listening through the tones. A mechanic is good to the degree it makes the player *listen*, suspect to the degree it listens for them.

## Scope

Default flight becomes a hybrid: **WASD flies and auto-tunes the three spatial realms; the player tunes the two higher realms (4 & 5) by ear.** Gentle "breathing" makes tuning a living thing to tend, scoped so resting stays restful. Full-manual (J) stays as the power mode. Onboarding teaches the new verb.

Chosen over: instrument-behind-J (doesn't fix the root cause — default flight auto-tunes, so the player never listens); full-manual-by-default (too steep, and tuning a pitch to "move left" is muddy); breathing-only (invisible while realms auto-tune).

## The control model (plain)

- **Move:** W/A/S/D + PageUp/PageDown — unchanged. They fly you and keep realms 1-3 in tune automatically.
- **Tune:** press **4** or **5** to choose a higher realm, **Up/Down** to tune it, listen for the beat to slow and go steady. The universe breathes, so you nudge them now and then.
- **Rest:** come to a near-stop while tuned, and the regeneration bath forms (now needs the higher realms attuned — listening is the doorway).
- **J** = full manual tuning of all five (existing hardcore mode), unchanged.

## Change-by-file overview

| File | What changes | Tier |
|------|-------------|------|
| `Ship.Input.cs` | Higher realms hand-tunable; 4/5 select & 1-3 explain; self-fining rate; stop auto-snapping 4 & 5; first-rest nudge | 1 (core) |
| `Ship.Update.cs` | Breathing on higher-realm targets via Model B `breathScale`; nudge trigger | 0.1 |
| `Ship.cs` | `SelectedDim` default → 3; `SpeedMode` default → 0; seed realms 4 & 5 in tune; nudge flags | 1 / 0.3 |
| `GameConstants.cs` | Breathing constants; self-fining constants; bumped beat-cue constants | 0.1 / 0.2 |
| `Menus/HelpScreen.cs` | Rewrite two "Flying" topics + tweak the rest topic + optional movement bridge | 2 |

## A. Control scheme (core — Ship.Input.cs)

**Key behavior (locked):**
- **4 / 5** select the fourth / fifth realm to tune (the two upper realms). One at a time; switch by pressing 4 or 5.
- **1 / 2 / 3** *explain* rather than select: "Realm 1 flies itself as you fly. Press 4 or 5 to tune a higher realm by ear."
- **Up / Down** tune the selected realm, held-continuous (sweep).
- **Self-fining rate:** in default-mode higher-realm tuning, the rate scales with distance to the target — full `TuningRate` when far, easing to `TuningFineMin` at the lock — so you settle gently. (Landed crystal tuning keeps its own scaling; full-J tuning keeps flat rate.)
- **Selected-only beat cue:** the wavering tone you hear always belongs to the currently selected realm.
- **First-rest nudge:** once per session, the first time you come to rest after flying (and haven't tuned a higher realm yet), a one-line spoken hint fires (see E).

**A1. Stop auto-snapping realms 4 & 5.** Delete the two lines (~506-507) that pin the higher realms each frame in manual flight:
```csharp
RDrive[3] = FTarget[3];   // ← remove
RDrive[4] = FTarget[4];   // ← remove
```

**A2. Tuning gate** (~439):
```csharp
bool allowTuning = TuningMode || (SelectedDim >= 3 && LockedTarget == null && !LandedMode);
```

**A3. Self-fining rate** for default-mode higher-realm tuning (before the Up/Down apply):
```csharp
if (!LandedMode && !TuningMode && SelectedDim >= 3) {
    float delta = MathF.Abs(RDrive[SelectedDim] - FTarget[SelectedDim]);
    float frac  = Math.Clamp(delta / GameConstants.TuningCoarseDelta, 0f, 1f);
    rate = GameConstants.TuningFineMin + (GameConstants.TuningRate - GameConstants.TuningFineMin) * frac;
}
```

**A4. Number keys** (~104-117): full TuningMode → 1-5 select as today. Default mode → 4 & 5 select; 1-3 speak the explanation. When a higher realm is tuned in default mode, set `_hasTunedHigherRealm = true`.

**A5. Updated hint** (~447-450) for Up/Down with a self-tuning realm selected: point at 4/5 and J.

**A6. Lock-release logic unchanged** (~428): only full-tuning Up/Down releases a lock.

## B. Breathing — Model B "stillness calms the tones" (Ship.Update.cs + GameConstants.cs)

**B1. Constants** (playtest dials):
```csharp
public const float BreathAmplitude    = 6f;    // Hz peak wander on a higher realm
public const float BreathPeriodRealm4 = 13f;   // s
public const float BreathPeriodRealm5 = 17f;   // s (offset so the two never move in lockstep)
public const float BreathDwellSettle  = 18f;   // s for breathing to fade to zero once resting
public const float BreathStillSpeed   = 0.3f;  // fraction of MaxVelocity below which you "settle" (breathing swells)
public const float BreathCruiseFloor  = 0.15f; // faint breathing that remains even at full cruise
```

**B2. `breathScale` rule** — a small pure helper (unit-testable). Breathing is full when you're still and free, fades as you speed up *and* as you sink into the bath, and holds steady whenever the ship/craft isn't yours to fly:
```csharp
float BreathScale() {
    if (LandedMode || LockedTarget != null || AstralMode || IdleMode) return 0f; // ship-managed / grounded / out-of-body / idle
    float speed = Vec5.Norm(Velocity);
    float stillness = 1f - Math.Clamp(speed / (MaxVelocity * GameConstants.BreathStillSpeed), 0f, 1f); // 0 cruise, 1 still
    float scale = GameConstants.BreathCruiseFloor + (1f - GameConstants.BreathCruiseFloor) * stillness;
    if (InRegeneration)
        scale *= MathF.Max(0f, 1f - (DwellTimer - GameConstants.DwellEnterTime) / GameConstants.BreathDwellSettle);
    return scale;
}
```

**B3. Apply it** in the target-assembly loop (~106-113), higher realms only:
```csharp
float breathScale = BreathScale();
for (int i = 0; i < N; i++) {
    float pull = MathF.Min(_envInfluence[i], GameConstants.EnvInfluenceMax);
    float breath = 0f;
    if (i >= 3) {
        float period = (i == 3) ? GameConstants.BreathPeriodRealm4 : GameConstants.BreathPeriodRealm5;
        breath = GameConstants.BreathAmplitude * breathScale * MathF.Sin(MathF.Tau * SimulationTime / period);
    }
    FTarget[i] = Math.Clamp(BaseFTarget[i] + pull + breath, GameConstants.FrequencyMin, GameConstants.FrequencyMax);
}
```
Deterministic (sine of sim-time — smooth, no GC, no `Random`). `InRegeneration`/`DwellTimer`/`Velocity` are at most one frame stale here; harmless. **The beat cue needs no change** — it reads `FTarget` for the selected realm, so breathing flows into the wavering tone you chase.

**Edges:** astral projection → breathing off (craft parked while you scout). Idle / cosmic meditation → breathing off (no one is there to tend; the universe rests).

## C. Beat cue + first speed (Tier 0.2 / 0.3)

**C1. Foreground the beat** (GameConstants ~77-83):
```csharp
public const float BeatCueRange  = 40f;   // was 25 — cue appears from further out
public const float BeatCueVolume = 0.12f; // was 0.07 — clearer, still subtle
public const float BeatLockZone  = 2.5f;  // was 3 — slightly crisper snap-to-steady
```

**C2. Gentle first speed** (Ship.cs ~135): `SpeedMode = 0` (Approach) instead of `2`. Not persisted, so every session starts calm.

## D. New-game seeding (Ship.cs)

Constructor frequency init (~336-341) — seed the higher realms in tune so the first encounter is gentle tending, not a from-scratch hunt:
```csharp
for (int i = 0; i < N; i++) {
    BaseFTarget[i] = MathHelpers.RandomRange(FrequencyMin, FrequencyMax);
    FTarget[i] = BaseFTarget[i];
    RDrive[i] = (i >= 3) ? BaseFTarget[i]                                  // higher realms start locked
                         : MathHelpers.RandomRange(FrequencyMin, FrequencyMax);
}
```
Also `SelectedDim = 3` (~49) so Up/Down and the beat cue target realm 4 immediately. Loaded games restore the player's tuned values, so this only shapes fresh starts.

## E. Onboarding (HelpScreen.cs — Tier 2)

**"How flying works":**
> Your craft has five drives, one per realm, and each makes a tone. The universe around you also has a tone in each realm. When your tone matches the universe's tone, that realm comes into resonance, and resonance is what carries you. So flying is really tuning: the closer the match, the more you resonate, and the faster and stronger you move. The three lower realms are the ones that move you through space, and your movement keys handle them for you — they keep themselves in tune as you fly, so you can travel from the very first moment. The two higher realms are different. They are yours to tune by ear, and tending them is how you deepen your resonance and open the deeper parts of the journey. The next topic shows you how. And if you would rather tune all five realms by hand, press J for full tuning at any time.

**"Tuning by ear":**
> Tuning the two higher realms is the heart of flying, and you tend them one at a time. Press 4 to take up the fourth realm, or 5 to take up the fifth; Up and Down then tune whichever one you are holding, and you move between them by pressing 4 and 5. As your tone nears its mark you will hear a pulsing beat, like two notes wavering against each other; the closer you come, the slower the pulse, until it smooths into one steady, single tone. That steady tone means the realm is locked. The tone sweeps quickly when you are far, and slows under your hand of its own accord as you near the lock, so you can settle gently onto the still center. The universe is alive and breathes, so these higher tones drift a little — most of all when you slow down and grow quiet, which is simply the cosmos inviting you to listen. A small nudge now and then keeps them tuned; you need never chase them. Press Q to hear how the selected realm sits. By default the game tells you the exact tone to aim for; if you would rather tune purely by sound, press N for by-ear mode, and it will tell you only how close you are: far, near, very close, or locked. And if you wish to tune all five realms by hand, press J for full tuning.

**"Consciousness and rest":**
> As you spend time in harmony, your consciousness slowly rises through levels of awakening, and the higher you rise, the more of the universe you can hear as distant voices open up around you. To rest, come nearly to a stop with your realms in tune — including the two higher realms you tend by ear, which is what truly opens the door. When you do, you settle into a regeneration bath that heals your craft and deepens your consciousness while you simply rest in the tones. And as you rest, the breathing of the tones settles with you, until they grow still; the longer you stay, the deeper the quiet. It is the gentlest, and one of the most powerful, things you can do.

**"Moving through space" — optional opening bridge:**
> These keys carry you through the three lower realms — the ones your craft keeps in tune for you — so your listening is free for the higher two.

**First-run spoken nudge** (not in F1; heard once, on first rest after flying):
> Two higher realms are yours to tune. Press 4, then Up and Down, and listen for the beat to steady into one clear tone.

## How this protects the resting reward

The bath gate is `avgRes ≥ 0.7` while nearly still. At rest, realms 1-3 auto-tune to ~1.0, so `avgRes = (3 + r4 + r5) / 5`; clearing 0.7 needs `r4 + r5 ≥ 0.5` — a little attunement of the higher realms becomes the doorway, gently. Once in the bath, `breathScale` decays to zero over ~18 s, so the longer you rest, the more the tones still themselves: presence to enter, stillness as you stay.

## Emergent bonus

Temple keys are claimed by resonating any one realm to the temple's note. With the higher realms now hand-tunable, key collection becomes a deliberate by-ear act with a real tradeoff. No code change needed.

## Soft decisions (locked)

1. Higher realms **seeded in tune** at new game (gentle).
2. Breathing on the **two higher realms only** (clean; no movement wobble).
3. Default-mode keys **1-3 explain rather than select**.
4. Keys: direct number selection (4/5); self-fining rate; selected-only beat cue; first-rest nudge.
5. Breathing **Model B** (stillness-coupled); off during astral & idle.

## Verification

- Extract `BreathScale()` and the `allowTuning`/self-fining logic as small pure methods; add unit tests (deterministic, fits the existing test net).
- Run the full test suite after each file.
- By-ear playtest pass — amplitude/periods (B1) and beat-cue levels (C1) are tuned to the ear, not locked at spec guesses.

## Execution order

C + D first (smallest, reversible, makes the feel testable) → A (control core) → B (breathing) → E (help) → tests throughout.

## Post-review refinements (2026-06-27)

An adversarial review (7 reviewers + skeptics) surfaced 8 findings; the verify/synth agents were lost to a transient API rate limit, so findings were adjudicated by hand against the code. 7 fixed, build green, 118 tests:

1. **BreathScale fade clamp** (TuningDynamics.cs): the in-bath fade factor is clamped to [0,1] so the pure function honors its documented 0..1 range even outside the in-game invariant. Added unit tests (maxVelocity=0 no divide-by-zero; over-unity-fade guard).
2. **Landed crystal tuning works in normal mode** (Ship.Input.cs): `allowTuning` now includes `LandedMode`, and number keys 1-5 select dims while landed (the "flies itself" line is flight-only). Collection needs `RDrive` tuned to the crystal's per-dim freqs (CollectCrystal: mean resonance > 0.8) — previously a landed player got misleading speech and a dead-end.
3. **No stranded selection** (Ship.Input.cs): leaving full tuning (J) with a spatial realm selected snaps `SelectedDim` back to 3, so Up/Down never lands on an un-tunable realm.
4. **Nudge suppression parity** (Ship.Update.cs): the first-rest nudge now also suppresses during Idle (cosmic meditation) and Astral projection, matching `BreathScale`'s suppression set.

**Flagged, NOT changed (pre-existing, out of scope — separate task):** the nebula-dissonance block writes random drift to `FTarget` and turbulence to `Velocity`, but both are overwritten each frame before use (FTarget is rebuilt at the top of `Update`; `Velocity` is reassigned in the resonance loop; position integrates before the nebula block). So nebula dissonance currently has ~no gameplay effect. Predates this work.

## Playtest fixes — audio crackle & by-ear feedback (2026-06-27)

Playtest surfaced two issues; a focused adversarial review (3 investigators + synthesis + 2 red-teamers) drove the fixes. Build green, 131 tests.

**CRACKLE (clicks when resonance changes).** Root cause: absolute-time phase — every drive voice was `sin(2*PI*f*t)` with unbounded `t`, so any frequency change (tuning, breathing, resonance-tied vibrato) stepped the phase and clicked. Fix: converted ALL voices (fundamental, 3 PHI overtones, subharmonic, both vibrato LFOs, dim 3/4 tremolo, Schumann, charge tone, intermod sum/diff) to per-sample **double phase accumulators** (`AudioSystem.Synthesis.cs` + new audio-thread-only fields in `AudioSystem.cs`). A frequency change now only bends the phase ramp's slope. Plus, per the red-team: per-buffer smoothing of drive/master gain and resonance (amplitude steps click too); intermod ramped in/out on stable per-pair slots (no detect/lose pop); accumulators never reset (resume is click-free via the drive-gain ramp); double-precision wrap modulus. New pure helper `VibratoShape` (+tests).

**FEEDBACK (couldn't gauge closeness/direction by ear).** Rebuilt the cue (new pure `CueShape` +tests): closeness now spans a useful range (`CueCoarseRange`=200 Hz, not the old 40 Hz on/off gate) and stays audible far (`CueFloor`); a countable pulse (`TremoloMin..Max`, capped <12 Hz) slows to near-still at lock; and a NEW signed **direction** cue via stereo pan (flat=left, sharp=right) with the carrier pitch held fixed as a landmark — direction and closeness on separate perceptual channels. **Critical red-team catch honored:** the cue tracks the still centre `BaseFTarget`, not the breathing/nebula-jittered `FTarget`, so a held tuning reads as a steady lock instead of flipping direction every breath. The cue is gated on "actively tuning" (recent Up/Down or realm-select, or full tuning mode; never on a planet) so the flight soundscape stays calm, and fades out smoothly when you stop. New `Ship.LastTuneTime` carries the active-tuning signal to the audio thread.

**By-ear dials (tune in playtest):** `BeatCueVolume`(0.18), `CueFloor`(0.5), `CueCoarseRange`(200), `TremoloMin`(0.3)/`Max`(9), `CueDeadband`(3), `DirPan`(0.7), `CueActiveWindow`(2.5), `GainSmoothingPerBuffer`(0.35).

**Deferred (noted, not done):** focus-duck (off by default, not implemented); aligning the spoken "true note" (Q) with the cue's `BaseFTarget` centre; watching pre-tanh headroom with the louder cue. Direction shipped as stereo pan (the red-team's screen-reader-friendly default) — switchable to carrier-pitch-lean if preferred after playtest.
