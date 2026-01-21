# Golden Spiral Spaceship Simulator

A 5-dimensional resonance-based spaceship simulator with Atlantean theming, designed for accessibility through audio-driven gameplay and screen reader support.

---

## Important Notice

**This simulator is designed for those who wish to explore consciousness, sacred geometry, and the mystical traditions of Atlantis.** The concepts, mechanics, and physics presented here are rooted in esoteric knowledge, ancient wisdom, and multidimensional theory rather than conventional linear science.

If you are firmly committed to materialist, reductionist, or purely empirical worldviews, this experience may not resonate with you. The resonance-based navigation, 5-dimensional space, sacred frequencies (Solfeggio, Schumann, 432 Hz), and consciousness progression systems cannot be "proven" through conventional scientific methods - and that is by design.

**This simulator is for:**
- Those drawn to the mysteries of Atlantis and ancient civilizations
- Seekers exploring consciousness expansion and multidimensional awareness
- Anyone who feels the call to experience reality beyond the 3D linear paradigm
- Those who understand that some truths are felt and experienced, not measured

Welcome, fellow traveler. May your frequencies align and your consciousness ascend.

---

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Quick Start Guide](#quick-start-guide)
4. [Core Concepts](#core-concepts)
5. [Controls Reference](#controls-reference)
6. [Game Mechanics](#game-mechanics)
7. [The Atlantean Universe](#the-atlantean-universe)
8. [Progression Guide](#progression-guide)
9. [Tips for New Players](#tips-for-new-players)
10. [Accessibility Features](#accessibility-features)
11. [Troubleshooting](#troubleshooting)
12. [Credits](#credits)

---

## Overview

In this simulator, you pilot a "Light Vehicle" through 5-dimensional space by tuning drive frequencies to match target frequencies. Instead of conventional thrust and steering, you navigate by achieving **resonance** - the closer your frequencies match, the faster you move in that dimension.

The game draws inspiration from Atlantean mythology, sacred geometry, and the concept that ancient civilizations navigated dimensions through harmonic frequency alignment rather than physical propulsion.

### Key Features

- **5-Dimensional Navigation**: 3 spatial dimensions (x, y, z) + 2 higher dimensions
- **Resonance-Based Movement**: Tune frequencies to move - no traditional controls
- **Audio-First Design**: Fully playable through sound alone
- **Screen Reader Support**: All actions announced via Tolk
- **Golden Ratio Integration**: PHI (1.618...) woven throughout mechanics
- **Atlantean Theming**: Temples, ley lines, sacred crystals, consciousness progression

---

## Installation

### Requirements

- Python 3.10+
- Conda (recommended) or pip
- Windows (for Tolk screen reader support)

### Setup with Conda (Recommended)

```bash
# Create and activate environment
conda create -n ss python=3.10
conda activate ss

# Install dependencies
conda install pygame numpy
pip install sounddevice cytolk

# Run the game
python main.py
```

### Setup with pip

```bash
# Create virtual environment
python -m venv venv
venv\Scripts\activate  # Windows
source venv/bin/activate  # Linux/Mac

# Install dependencies
pip install pygame numpy sounddevice cytolk

# Run the game
python main.py
```

### Dependencies

| Package | Purpose |
|---------|---------|
| pygame | Graphics, input handling |
| numpy | Vector math, audio synthesis |
| sounddevice | Real-time audio output |
| cytolk | Screen reader integration |

---

## Quick Start Guide

### Your First 5 Minutes

1. **Launch the game**: `python main.py`
2. **Listen**: You'll hear a continuous tone - this is your drive signal
3. **Press R**: Read your current status (position, velocity, resonance)
4. **Press Q**: Query the target frequency for your selected dimension
5. **Use Up/Down arrows**: Adjust your drive frequency toward the target
6. **Watch resonance climb**: As frequencies match, you'll move faster

### The Basic Loop

```
Listen to drive tone → Check target (Q) → Tune frequency (Up/Down) → Achieve resonance → Move through space
```

### Your First Landing

1. Press **M** to open the starmap
2. Use **Up/Down** to navigate, find a planet
3. Press **Enter** to lock on - autopilot engages
4. When close, press **L** to land (need 80% resonance)
5. On the planet, use **WASD** to move cursor
6. Press **F** to scan for crystals
7. Tune to crystal frequency, press **X** to collect

---

## Core Concepts

### Resonance

Resonance is the fundamental mechanic. For each dimension:

```
Resonance = 1 / (1 + (drive_freq - target_freq)^2 / width^2)
```

- **Perfect match** (0 Hz difference): 100% resonance
- **Close match** (within width): High resonance, good speed
- **Far off**: Low resonance, minimal movement

Your **resonance width** (upgradable) determines how forgiving the tuning is.

### The Five Dimensions

| Dimension | Keys | Description |
|-----------|------|-------------|
| 1 (X) | A/D | Left/Right spatial |
| 2 (Y) | W/S | Forward/Back spatial |
| 3 (Z) | PgUp/PgDn | Up/Down spatial |
| 4 (Higher 1) | Arrows | First higher dimension |
| 5 (Higher 2) | Arrows | Second higher dimension |

In **Manual Mode** (default): WASD/PgUp/PgDn control spatial dims, arrows tune higher dims.
In **Resonance Tuning Mode** (J to toggle): Arrows tune the selected dimension (1-5 keys).

### Frequency Range

All frequencies operate between **200 Hz and 800 Hz**.

Special frequencies to know:
- **432 Hz**: Natural harmony (Atlantean tuning)
- **528 Hz**: "Miracle frequency" (DNA repair)
- **110 Hz**: Temple resonance (ancient healing)
- **118 Hz**: Pyramid resonance (Great Pyramid)

---

## Controls Reference

### Navigation

| Key | Action |
|-----|--------|
| W/S | Move forward/backward (Y dimension) |
| A/D | Move left/right (X dimension) |
| PgUp | Descend (negative Z) |
| PgDn | Ascend (positive Z) |
| Left/Right Arrow | Rotate view |
| Up/Down Arrow | Tune frequency in selected dimension |

### Dimension & Mode Selection

| Key | Action |
|-----|--------|
| 1-5 | Select dimension to tune |
| J | Toggle Manual/Resonance Tuning mode |
| Z | Cycle speed modes (Approach/Cruise/Quantum) |

### Information

| Key | Action |
|-----|--------|
| R | Read full status |
| Q | Query target frequency for selected dimension |
| M | Toggle starmap |
| U | Toggle HUD / Open upgrade menu (on planet) |
| V | Cycle verbosity (Low/Medium/High) |
| F1 | Open instructions file |

### Planet Exploration

| Key | Action |
|-----|--------|
| L | Initiate landing (near planet, high resonance) |
| T | Take off from planet |
| W/A/S/D | Move cursor on planet grid |
| F | Scan nearest crystal |
| X | Collect crystal (when tuned) |

### Atlantean Features

| Key | Action |
|-----|--------|
| G | Cycle Tuaoi Crystal modes |
| P | Create portal anchor (costs 3 crystals) |
| Shift+P | Teleport to portal anchor |
| B | Enter/exit astral projection |
| E | Interact with Harmonic Chamber (rift) |

### Audio & Settings

| Key | Action |
|-----|--------|
| =/- | Adjust master volume |
| Shift + =/- | Adjust beep volume |
| Ctrl + =/- | Adjust effect volume |
| Alt + =/- | Adjust drive signal volume |
| C | Toggle high contrast mode |
| T + =/- | Adjust HUD text size |

### System

| Key | Action |
|-----|--------|
| Ctrl+S | Save game |
| Ctrl+L | Load game |
| Ctrl+A | Toggle autosave |
| H | Toggle sing-to-tune mode (microphone) |
| ESC | Quit |

---

## Game Mechanics

### Celestial Bodies

#### Stars (4 Types)

| Type | Color | Frequency | Sound |
|------|-------|-----------|-------|
| Main Sequence | Yellow | 1.0x | Standard |
| Red Giant | Orange-Red | 0.7x | Deep 40 Hz pulse |
| White Dwarf | Blue-White | 1.8x | High 1350 Hz whine |
| Brown Dwarf | Dark Brown | 0.3x | Barely audible rumble |

#### Planets (5 Types)

| Type | Landing Difficulty | Crystal Multiplier | Sound |
|------|-------------------|-------------------|-------|
| Ocean World | 0.8x (easier) | 1.5x | Flowing water |
| Super-Earth | 1.0x (normal) | 1.2x | Solid tone |
| Ice Giant | 1.3x (harder) | 0.8x | Crystalline chimes |
| Hot Jupiter | 1.5x (very hard) | 0.5x | Roaring furnace |
| Rogue Planet | 2.0x (extreme) | 2.0x | Ominous silence |

#### Nebulae (4 Types)

| Type | Dissonance | Effect |
|------|------------|--------|
| Emission | 0.5 | Moderate frequency drift |
| Reflection | 0.3 | Mild drift |
| Planetary | 0.4 | Medium drift |
| Supernova Remnant | 0.9 | Extreme chaos, velocity jitter |

### Harmonic Chambers (Rifts)

Dimensional portals that appear when resonance is high (>90%). To enter:

1. Detect nearby rift (announced with direction)
2. Press E to enter rift selection mode
3. Select and lock onto rift
4. Approach within 20 units
5. Achieve >60% resonance
6. Press E to charge entry (4 seconds)

Rift types:
- **Boost**: Velocity bonus
- **Crystal**: Free crystal
- **Hazard**: Integrity damage
- **Perfect Fifth**: Permanent crystal bonus (very rare)

### Harmonic Relationships

When two dimensions have frequencies in musical ratios, bonuses activate:

| Interval | Ratio | Bonus |
|----------|-------|-------|
| Octave | 2:1 | Velocity boost |
| Perfect Fifth | 3:2 | Stability bonus |
| Perfect Fourth | 4:3 | Integrity regeneration |
| Major Third | 5:4 | Easier tuning |
| Golden Ratio | PHI:1 | Enhanced rift detection |
| Tritone | √2:1 | Chaotic velocity (danger!) |

### Crystal Collection

On planets, crystals are scattered in a grid. To collect:

1. Press F to scan nearest crystal (announces direction and frequency)
2. Use WASD to move cursor toward crystal
3. Tune your frequencies to match crystal frequencies
4. When close and resonance >80%, press X to collect

**Crystal Types by Frequency:**

| Crystal | Frequency Range | Chakra | Bonus |
|---------|----------------|--------|-------|
| Ruby | 200-285 Hz | Root | Stability |
| Carnelian | 285-350 Hz | Sacral | Crystal finding |
| Citrine | 350-417 Hz | Solar Plexus | Velocity |
| Emerald | 417-528 Hz | Heart | Integrity |
| Lapis | 528-639 Hz | Throat | Scan range |
| Amethyst | 639-741 Hz | Third Eye | Rift detection |
| Quartz | 741-800 Hz | Crown | Universal |

**Special Atlantean Crystals (15% spawn rate):**

| Crystal | Effect | Value |
|---------|--------|-------|
| Fire Crystal | 1.5x speed burst | 2x |
| Aquamarine | +0.2 integrity | 1.5x |
| Larimar | Wisdom message | 1.8x |
| Moldavite | +0.1 consciousness | 2.5x |
| Lemurian Seed | Reveals nearby items | 2x |
| Black Tourmaline | Clears all dissonance | 1.3x |
| Celestite | Enhanced rift detection | 1.7x |

### Upgrades

After collecting all crystals on a planet, press U to access upgrades:

| Upgrade | Cost | Effect |
|---------|------|--------|
| Resonance Width | 1 | +0.809 Hz tolerance |
| Integrity Repair | 1 | +0.324 integrity |
| Max Velocity | 2 | 1.618x speed multiplier |
| Auto-Tune Helper | 3 | Slight automatic frequency adjustment |
| Crystal Growth | 5 | +1 crystal per planet |
| Golden Harmony | 8 | Permanent PHI multiplier to all stats |

Costs follow Fibonacci sequence: 1, 1, 2, 3, 5, 8, 13, 21...

---

## The Atlantean Universe

### Solfeggio Frequencies

Tune any dimension to these frequencies for special effects:

| Frequency | Name | Effect |
|-----------|------|--------|
| 174 Hz | Foundation | Shield bonus |
| 285 Hz | Quantum | Minor healing |
| 396 Hz | Liberation | Stability |
| 417 Hz | Transmutation | Rift assist |
| 432 Hz | Natural Harmony | Base healing |
| 528 Hz | Miracle | Major healing (2x) |
| 639 Hz | Connection | Communication boost |
| 741 Hz | Awakening | Rift detection |
| 852 Hz | Intuition | Third eye bonus |
| 963 Hz | Divine | Transcendence |

### Tuaoi Crystal Modes

Press **G** to cycle through 6 modes of your ship's central crystal:

| Mode | Base Frequency | Effect |
|------|---------------|--------|
| Healing | 432 Hz | +0.01 integrity/second |
| Navigation | 414 Hz (PHI×256) | 1.5x autopilot efficiency |
| Communication | 7.83 Hz | 2x scan range |
| Power | 528 Hz | 1.25x velocity |
| Regeneration | 285 Hz | 1.3x resonance width |
| Transcendence | 963 Hz | 1.4x higher dimension bonus |

### Merkaba Activation

When ALL 5 dimensions achieve >90% resonance simultaneously:

- **Shield**: 50% damage reduction
- **Speed**: 1.3x velocity multiplier
- **Detection**: 2x range for rifts and crystals
- Announced: "Merkaba activated. Light vehicle field engaged."

### The 12+1 Temple System

Twelve temples are arranged in a sacred dodecagon pattern around the universe:

| Temple | Key | Frequency |
|--------|-----|-----------|
| Aries | 1 | 396 Hz |
| Taurus | 2 | 417 Hz |
| Gemini | 3 | 432 Hz |
| Cancer | 4 | 444 Hz |
| Leo | 5 | 480 Hz |
| Virgo | 6 | 512 Hz |
| Libra | 7 | 528 Hz |
| Scorpio | 8 | 576 Hz |
| Sagittarius | 9 | 594 Hz |
| Capricorn | 10 | 639 Hz |
| Aquarius | 11 | 672 Hz |
| Pisces | 12 | 741 Hz |

**To collect a temple key:**
1. Approach the temple (shown as golden triangle on screen)
2. Tune any dimension to the temple's frequency
3. Achieve 70% resonance at that frequency
4. Key automatically collected

### Ley Line Highways

30 energy corridors connect the temples:
- **Ring lines**: Connect adjacent temples (12 lines)
- **Major lines**: Connect opposite temples (6 lines)
- **Amenti paths**: Connect each temple to the center (12 lines)

**Benefits when on a ley line:**
- 3x velocity multiplier
- Natural 432 Hz resonance boost
- Announced when entering/leaving

### Pyramid Resonance Chambers

3 pyramids at sacred locations provide enhanced healing:

| Pyramid | Location |
|---------|----------|
| Giza Resonance | PHI×50, 0, PHI×30 |
| Stellar Alignment | -PHI×40, PHI×40, -PHI×20 |
| Dimensional Gateway | 0, -PHI×60, PHI×40 |

**At a pyramid:**
- Tune to 118 Hz (±3 Hz tolerance)
- 3x healing multiplier
- 2x consciousness gain
- Announced: "Entering Pyramid of Giza Resonance."

### Halls of Amenti (Master Temple)

The ultimate destination at the universe's center (0, 0, 0, 0, 0).

**Entry Requirements:**
- All 12 temple keys collected
- Consciousness level: Enlightened or Ascended
- Merkaba active (all dims >90%)

**Rewards:**
- Permanent PHI multiplier to resonance width
- 3x crystal collection multiplier
- Consciousness unlocked to "Ascended"
- Future: Access to 6th dimension

### Consciousness Levels

Your consciousness evolves based on sustained high resonance:

| Level | Threshold | Multiplier | Description |
|-------|-----------|------------|-------------|
| Dormant | 0% | 1.0x | Unawakened |
| Awakening | 30% | 1.2x | Beginning to sense harmonics |
| Aware | 50% | 1.4x | Consciously navigating |
| Attuned | 70% | 1.6x | Deeply connected |
| Enlightened | 85% | 1.8x | Mastery achieved |
| Ascended | 95% | 2.0x | One with universal frequency |

- Gain consciousness at >80% average resonance
- Lose consciousness at <30% average resonance
- Pyramids boost gain rate 2x

### Portal Anchor System

Bookmark locations for fast travel:

**Create Anchor (P key):**
- Costs 3 crystals
- Maximum 7 anchors
- Saves current position

**Use Anchor (Shift+P):**
- Requires 85% resonance
- 30 second cooldown
- Instant teleportation

### Astral Projection Mode

Scout ahead without moving your ship:

**Enter (B key):**
- Requires 90% resonance in all dimensions
- 60 second cooldown between uses

**While Projecting:**
- 5x movement speed
- 30 second duration limit
- Stay within 200 units of body
- Press B to return early

---

## Progression Guide

### Early Game (0-10 Crystals)

1. **Learn the basics**: Practice tuning frequencies near your starting position
2. **Find your first planet**: Use starmap (M), lock on, let autopilot guide you
3. **Land and collect**: Ocean worlds are easiest (0.8x difficulty)
4. **Upgrade resonance width first**: Makes everything easier

### Mid Game (10-30 Crystals)

1. **Explore different planet types**: Try super-earths and ice giants
2. **Hunt for Harmonic Chambers**: High resonance spawns rifts
3. **Visit temples**: Start collecting zodiac keys
4. **Use ley lines**: Travel between temples faster
5. **Experiment with Tuaoi modes**: Find your preferred playstyle

### Late Game (30+ Crystals)

1. **Collect all 12 temple keys**
2. **Reach "Enlightened" consciousness**
3. **Master Merkaba activation** (all dims >90%)
4. **Set up portal anchor network**
5. **Attempt rogue planets** (2x difficulty, 2x crystals)
6. **Enter the Halls of Amenti**

### Ascension

When you collect 21+ crystals, ascension triggers:
- Position resets to origin
- Golden Harmony permanently activates
- Universe regenerates with new layout
- Your journey continues at a higher level

---

## Tips for New Players

### Getting Started

1. **Start with Q and R keys**: These tell you what you need to know
2. **Listen to the audio**: Pitch changes indicate frequency changes
3. **Use the starmap**: It's your navigation computer
4. **Don't panic at low resonance**: Just retune, no permanent damage

### Tuning Tips

1. **Smaller adjustments = more precision**: Tap arrows, don't hold
2. **Watch for "approaching lock" message**: You're getting close
3. **Auto-snap helps**: When resonance >50%, you'll lock automatically
4. **Higher dimensions (4-5) affect projection**: Useful for seeing rifts

### Combat Survival (There Is None)

This isn't a combat game. "Damage" comes from:
- Failed landing attempts (-0.1 integrity)
- Dissonance (-0.01/second at <20% resonance)
- Hazard rifts (-0.1 integrity)
- Nebula turbulence (indirect via dissonance)

**Healing:**
- Tuaoi Healing mode (+0.01/second)
- Temple resonance at 110 Hz (+0.02/second)
- Pyramid resonance at 118 Hz (+0.06/second with 3x multiplier)
- 528 Hz Solfeggio frequency (major heal bonus)
- Integrity Repair upgrade

### Efficiency Tips

1. **Ley lines are fast**: Plan routes between temples
2. **Portal anchors at key locations**: One near a pyramid is valuable
3. **Astral scout before committing**: Check dangerous areas
4. **Golden Spiral pattern**: Crystals on planets follow this - predict their positions

### Audio Cues to Learn

| Sound | Meaning |
|-------|---------|
| Rising pitch | Frequency increasing |
| Falling pitch | Frequency decreasing |
| Click sounds | Resonance feedback (faster = higher resonance) |
| Chime | Harmonic relationship detected |
| Deep pulse | Near red giant star |
| High whine | Near white dwarf |
| Whoosh | View rotating |
| Double beep | Lock achieved |

---

## Accessibility Features

### Screen Reader Support

- All game events announced via Tolk
- Verbosity levels: Low (minimal), Medium (standard), High (detailed)
- Press V to cycle verbosity

### Audio-Only Play

The game is fully playable without visuals:
- Spatial audio indicates direction (pan left/right)
- Pitch indicates frequency
- Click rate indicates resonance quality
- All menus are navigable with Up/Down/Enter

### Visual Aids

For sighted players or those with partial vision:
- High contrast mode (C key): Black/white swap
- Adjustable text size (T + =/-)
- Color-coded celestial bodies
- Golden spiral ship visualization

### Cognitive Load Management

- Autosave every 5 minutes (toggle with Ctrl+A)
- Pause-like behavior in menus (game doesn't advance)
- Status readout on demand (R key)
- No time pressure on most actions

---

## Troubleshooting

### No Audio

1. Check sounddevice is installed: `pip install sounddevice`
2. Verify audio output device is working
3. Check volume isn't muted (=/- keys)
4. Try restarting the game

### Screen Reader Not Working

1. Verify cytolk is installed: `pip install cytolk`
2. Ensure a screen reader is running (NVDA, JAWS, etc.)
3. On first run, Tolk needs to initialize

### Game Won't Start

1. Check Python version: `python --version` (need 3.10+)
2. Verify all dependencies: `pip list`
3. Check for import errors in console output

### Performance Issues

1. Close other audio applications
2. Reduce number of active sound effects
3. The game targets 60 FPS - lower spec machines may struggle

### Save File Issues

- Save location: `savegame.pkl` in game directory
- Delete to reset: `del savegame.pkl`
- Config location: `config.ini`

---

## File Structure

```
SpaceSim/
├── main.py            # Entry point, game loop, rendering
├── ship.py            # Ship class, all game logic
├── audio_system.py    # Audio synthesis, SoundEffect class
├── celestial.py       # Universe generation
├── constants.py       # All game constants
├── utils.py           # Helper functions
├── config.ini         # User settings (generated)
├── savegame.pkl       # Save file (generated)
├── README.md          # This file (press F1 in-game to open)
├── CLAUDE.md          # Development documentation
└── Reserve/           # Historical versions
```

---

## Credits

**Concept & Design**: A 5-dimensional resonance-based navigation system inspired by Atlantean mythology, sacred geometry, and the golden ratio.

**Accessibility Philosophy**: Audio-first, screen reader compatible, playable without vision.

**Mathematical Foundation**: Golden ratio (PHI = 1.618...) integration throughout:
- Celestial positioning (golden spiral)
- Audio harmonics (PHI overtones)
- Upgrade curves (Fibonacci costs)
- Velocity calculations
- Sacred geometry patterns

**Audio Design**: Real-time synthesis using:
- Pure sine waves with golden ratio overtones
- Solfeggio frequency detection
- Spatial audio panning
- Type-specific ambient soundscapes

**Research Sources**:
- Atlantean crystal technology and Tuaoi Stone mythology
- Sacred geometry (Flower of Life, Metatron's Cube, Merkaba)
- Solfeggio frequencies and sound healing
- Ancient temple acoustics (110 Hz resonance)
- Great Pyramid frequency (118 Hz)

---

## License

[Add your license here]

---

## Contributing

[Add contribution guidelines here]

---

*"The spiral binds all realms in golden eternity."*
