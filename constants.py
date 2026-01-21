"""
Constants and configuration for the Golden Spiral Spaceship Simulator.

This module contains all game constants, including physics parameters,
audio settings, gameplay thresholds, and the instructions text.
"""

import numpy as np

# Core dimensions and display
N_DIMENSIONS = 5  # 3 spatial + 2 higher dimensions
SCREEN_WIDTH, SCREEN_HEIGHT = 800, 600  # Screen dimensions
FPS = 60  # Frames per second
DT = 1.0 / FPS  # Time delta per frame

# Physics constants
MAX_VELOCITY_BASE = 10.0  # Base maximum velocity, upgradable
RESONANCE_WIDTH_BASE = 10.0  # Base resonance width in Hz, upgradable
FREQUENCY_RANGE = (200.0, 800.0)  # Frequency range for drives and targets
PHI = (1 + np.sqrt(5)) / 2  # Golden ratio constant

# Audio settings
SAMPLE_RATE = 44100  # Audio sample rate
SCHUMANN_FREQ = 7.83  # Schumann resonance frequency
SCHUMANN_VOLUME = 0.01  # -40 dB equivalent

# Celestial body generation
N_STARS = 200  # Number of stars in the universe
N_PLANETS_PER_STAR = 3  # Planets per star
N_NEBULAE = 10  # Number of nebulae
ORBIT_RADIUS = 5.0  # Radius for planet orbits around stars
PLANET_RADIUS = 10.0  # Visual radius for planets
INTERACTION_DISTANCE = 15.0  # Distance for dimensional interactions

# Fibonacci sequence for golden spiral generation
N_FIBONACCI = 8  # Fibonacci sequence length for generation
FIB_SEQ = [0, 1]  # Initialize Fibonacci sequence
for _ in range(N_FIBONACCI - 2):
    FIB_SEQ.append(FIB_SEQ[-1] + FIB_SEQ[-2])  # Generate Fibonacci sequence
SCALE_FACTOR = 100.0 / FIB_SEQ[-1]  # Scaling for positioning

# Speech and audio feedback
SPEECH_COOLDOWN = 0.5  # Cooldown between speech messages in seconds
VIEW_LANDMARK_THRESHOLD = 10.0  # Degrees threshold for audible landmarks
ROTATION_SOUND_DURATION = 0.2  # Duration of rotation whoosh sound
LANDMARK_SPEECH_COOLDOWN = 1.0  # Cooldown for landmark announcements in seconds
CURSOR_SPEECH_COOLDOWN = 0.2  # Cooldown for cursor position speech

# Landing and planet exploration
LANDING_THRESHOLD = 0.8  # Average resonance required for landing
LANDING_TIME = 3.0  # Time to charge landing sequence
CRYSTAL_COUNT_BASE = 3  # Base crystals per planet, upgradable
GRID_SIZE = 10  # Size of planet exploration grid
CRYSTAL_COLLECTION_THRESHOLD = 0.8  # Resonance to collect crystal (lowered for easier collection)

# Resonance and power mechanics
POWER_BUILD_THRESHOLD = 0.8  # Resonance threshold for power buildup
POWER_BUILD_TIME = 5.0  # Time for full power boost
DISSONANCE_THRESHOLD = 0.2  # Average resonance for dissonance trigger
DISSONANCE_DURATION = 10.0  # Duration of low resonance to trigger dissonance
PERFECT_RESONANCE_THRESHOLD = 0.999  # Threshold for perfect resonance chime

# Rift mechanics
RIFT_ALIGNMENT_TOLERANCE = 20.0  # Tolerance for rift entry alignment (widened for accessibility)
RIFT_FADE_TIME = 30.0  # Time before rift fades
RIFT_ENTRY_RES_THRESHOLD = 0.6  # Required resonance for rift entry (lowered for easier entry)
RIFT_MAX_DIST = 20.0  # Max distance for rift volume modulation
RIFT_FOCUS_THRESHOLD = 90.0  # Adjusted for vertical alignment (centered when |angle| ≈ 90)
RIFT_ENTRY_ALIGNMENT_ANGLE = 20.0  # Deviation from 90 degrees for entry alignment (widened for accessibility)
RIFT_CHARGE_TIME = 4.0  # Charge duration for rift entry
RIFT_NUDGE_RATE = 0.2  # Auto-nudge speed during charge for alignment
PERFECT_FIFTH_TOLERANCE = 0.5  # Hz tolerance for perfect fifth rift
PERFECT_FIFTH_PROB = 0.0001  # Super-rare probability for perfect fifth rift

# UI and display
HUD_TEXT_SIZE_BASE = 24  # Base HUD text font size
HIGH_CONTRAST = False  # Toggle for high contrast mode
CLICK_INTERVAL = 0.5  # Interval between velocity tone plays

# Upgrades and progression
UPGRADE_COSTS = [1, 1, 2, 3, 5, 8, 13, 21]  # Fibonacci costs for upgrades
ASCENSION_CRYSTAL_THRESHOLD = 21  # Crystals needed for ascension

# Navigation and tuning
ROTATION_SPEED = 3.0  # View rotation speed in radians per second (was 0.1, way too slow!)
TUNING_RATE = 100.0  # Rate for manual frequency tuning
TUNING_RATE_PLANET = 20.0  # Slower tuning rate on planets
SCANNER_RANGE = 50.0  # Range for starmap scanner
SLOWDOWN_DIST = 20.0  # Distance to slow down on approach
AUTO_SNAP_THRESHOLD = 0.5  # Threshold for auto-snapping to frequencies (lowered for easier snapping)
APPROACHING_LOCK_THRESHOLD = 10.0  # Delta freq for approaching lock

# Speed modes
SPEED_FACTORS = [0.3, 0.6, 1.0]  # Speed factors for approach, cruise, quantum modes
SPEED_MODE_NAMES = ["Approach", "Cruise", "Quantum"]  # Names for speed modes

# Celestial body effects
STAR_HARMONY_RADIUS = 12.0
STAR_MAX_BENEFIT_RADIUS = 6.0
NEBULA_DISSONANCE_RADIUS = 10.0

# Special mechanics
IDLE_TIME_THRESHOLD = 120.0  # 2 minutes for cosmic meditation
PITCH_RECORD_DURATION = 1.0  # Duration for mic recording in sing mode
EASTER_EGG_FREQ = 432.0  # For easter egg
EASTER_EGG_TOLERANCE = 0.1  # Hz tolerance for easter egg
AUTOSAVE_INTERVAL = 300.0  # 5 minutes for autosave
WATER_BLESSING_HOLD_TIME = 33.0  # Seconds to hold spacebar for water blessing
WATER_BLESSING_RES_THRESHOLD = 0.999  # Resonance threshold for blessing
WATER_BLESSING_FREQ = 432.0  # Frequency for gift.wav
WATER_BLESSING_DURATION = 60.0  # Duration of gift.wav in seconds
SING_SILENCE_THRESHOLD = 4.0  # Seconds of silence in sing mode to trigger heartbeat
HEARTBEAT_VOLUME = 0.1  # Low volume for heartbeat pulse

# Instructions text with updated controls and rift entry details
INSTRUCTIONS = """
Golden Spiral Spaceship Simulator Instructions

Controls:
- W/S: Move forward/backward (adjusts r_drive in y-dim for resonance propulsion)
- A/D: Move left/right (adjusts r_drive in x-dim)
- PageUp: Descend (adjusts r_drive in z-dim for -z movement)
- PageDown: Ascent (adjusts r_drive in z-dim for +z movement)
- Left/Right Arrow: Rotate view left/right
- Up/Down Arrow: Increase/Decrease drive frequency in selected dim
- 1-5: Select dimension to tune (1: x, 2: y, 3: z, 4: higher1, 5: higher2); In HUD mode: Navigate items
- J: Toggle between resonance tuning mode (tune all dims) and manual mode (tune only higher dims)
- W/A/S/D: On planet: Move cursor north/west/south/east by 1 unit per press
- R: Read full status (position, velocity, etc.)
- U: Toggle HUD dialog mode (navigable menu); On planet after all crystals: Upgrade menu
- Q: Quick query target freq in selected dim
- V: Toggle verbosity mode (Low/Medium/High)
- M: Toggle starmap mode (navigable menu of nearby bodies/rifts)
- T + =/-: Increase/Decrease HUD text size
- C: Toggle high contrast mode
- L: Initiate landing (near planet, high resonance)
- T: Takeoff from planet
- F: On planet, scan nearest crystal freq
- X: On planet, collect locked crystal
- E: If locked on rift and near with sufficient resonance: Charge/enter rift with guidance. Otherwise: Toggle rift selection mode (if rifts detected)
- Z: In manual mode, toggle speed modes (Approach, Cruise, Quantum)
- =/-: Adjust master volume
- Shift + =/-: Adjust beep volume (planets/rifts/locks)
- Ctrl + =/-: Adjust effect volume (clicks/rotations/chords/hums)
- Alt + =/-: Adjust drive signal volume
- F1: Open this instructions file
- H: Toggle sing-to-tune mode (hum into mic to set drive freq in selected dim)
- Ctrl+S: Save game
- Ctrl+L: Load game
- Ctrl+A: Toggle autosave (every 5 minutes)
- ESC: Quit
- In starmap: Type first letter to jump to items (P for Planet, R for Rift, S for Star, N for Nebula, etc.)

Resonance System:
- Tune r_drive close to f_target per dim for velocity (magnitude by resonance level, direction by sign).
- Power buildup: Sustain >0.8 resonance for boosts.
- Dissonance: Low resonance triggers turbulence jitter.
- Upgrades: Collect crystals on planets for tiered upgrades (width, velocity, etc.).

Viewing System:
- Rotate to mix higher dims into 2D projection—scan for rifts/objects with panned audio.

Rifts:
- Detected nearby with panned hum; rotate to center (pan=0) for entry.
- Lock via selection mode or starmap; guidance provided for approach/alignment.
- Near with high resonance, press E to charge sequence (auto-nudges alignment; sustain resonance to enter).
- Rewards warp boosts or bonuses.

Landing/Exploration:
- Near planet with high resonance, press L to land.
- On planet: Press W/A/S/D to move cursor on grid, scan/tune to crystals, collect with X.
- Sing mode (H): Hum into mic to auto-tune to nearest crystal.
- Upgrade menu via U on planet after collecting all.
- Press T to takeoff.

Starmap:
- Press M to toggle.
- Navigate items with up/down, enter to lock on (autopilot tunes to navigate, lock sound for alignment).
- Items: Nearby planets, rifts with dist/angle.

Accessibility:
- All actions spoken via Tolk.
- Verbosity modes, spatial audio, navigable HUD/starmap.
"""

# Harmonic relationship system
HARMONIC_TOLERANCE = 0.02  # Tolerance for detecting harmonic ratios (2%)
HARMONIC_RATIOS = {
    'octave': 2.0,           # Perfect octave (2:1)
    'perfect_fifth': 1.5,    # Perfect fifth (3:2)
    'perfect_fourth': 1.333, # Perfect fourth (4:3)
    'major_third': 1.25,     # Major third (5:4)
    'minor_third': 1.2,      # Minor third (6:5)
    'major_sixth': 1.667,    # Major sixth (5:3)
    'minor_sixth': 1.6,      # Minor sixth (8:5)
    'tritone': 1.414,        # Tritone (√2:1) - the devil's interval
    'golden': PHI,           # Golden ratio (φ:1)
}
HARMONIC_DETECTION_INTERVAL = 0.5  # Check for harmonics every 0.5 seconds
HARMONIC_BONUS_DURATION = 2.0  # How long harmonic bonuses last
HARMONIC_BONUS_MULTIPLIER = 1.15  # Resonance width multiplier during harmonic alignment

# Harmonic series settings
N_HARMONICS = 7  # Number of harmonics per drive signal
HARMONIC_FALLOFF = 1.5  # Exponential falloff for harmonic amplitudes (higher = faster fade)
SUBHARMONIC_DEPTH = 0.15  # Amplitude of subharmonic (octave below fundamental)
INTERMOD_DEPTH = 0.08  # Amplitude of intermodulation tones

# ===== REALISTIC UNIVERSE PHENOMENA =====

# Stellar types and evolution stages
STELLAR_TYPES = {
    'main_sequence': {
        'color': (255, 255, 200),
        'freq_mult': 1.0,
        'desc': 'stable hydrogen-burning star',
        'audio_range': (200, 400)
    },
    'red_giant': {
        'color': (255, 100, 50),
        'freq_mult': 0.7,
        'desc': 'ancient bloated star',
        'audio_range': (30, 50)  # Deep bass pulse
    },
    'white_dwarf': {
        'color': (200, 220, 255),
        'freq_mult': 1.8,
        'desc': 'dense stellar core',
        'audio_range': (1200, 1500)  # High whine
    },
    'brown_dwarf': {
        'color': (100, 50, 30),
        'freq_mult': 0.3,
        'desc': 'failed star',
        'audio_range': (20, 30)  # Barely audible rumble
    }
}
STELLAR_TYPE_PROBABILITIES = {
    'main_sequence': 0.70,
    'red_giant': 0.15,
    'white_dwarf': 0.10,
    'brown_dwarf': 0.05
}

# Nebula types (expand existing generic nebulae)
NEBULA_TYPES = {
    'emission': {
        'color': (255, 50, 50),
        'freq_range': (200, 300),
        'dissonance': 0.5,
        'desc': 'ionized gas cloud'
    },
    'reflection': {
        'color': (50, 150, 255),
        'freq_range': (600, 800),
        'dissonance': 0.3,
        'desc': 'dust reflecting starlight'
    },
    'planetary': {
        'color': (150, 255, 150),
        'freq_range': (400, 600),
        'dissonance': 0.4,
        'desc': 'dying star shell'
    },
    'supernova_remnant': {
        'color': (255, 150, 100),
        'freq_range': (100, 900),
        'dissonance': 0.9,
        'desc': 'expanding blast wave'
    }
}
NEBULA_TYPE_PROBABILITIES = {
    'emission': 0.40,
    'reflection': 0.30,
    'planetary': 0.20,
    'supernova_remnant': 0.10
}

# Exoplanet types
EXOPLANET_TYPES = {
    'hot_jupiter': {
        'size_mult': 3.0,
        'crystal_mult': 0.5,
        'difficulty': 1.5,
        'desc': 'scorching gas giant'
    },
    'super_earth': {
        'size_mult': 1.5,
        'crystal_mult': 1.2,
        'difficulty': 1.0,
        'desc': 'massive rocky world'
    },
    'ocean_world': {
        'size_mult': 1.2,
        'crystal_mult': 1.5,
        'difficulty': 0.8,
        'desc': 'water-covered planet'
    },
    'rogue_planet': {
        'size_mult': 1.0,
        'crystal_mult': 2.0,
        'difficulty': 2.0,
        'desc': 'sunless wanderer'
    },
    'ice_giant': {
        'size_mult': 2.5,
        'crystal_mult': 0.8,
        'difficulty': 1.3,
        'desc': 'frozen methane world'
    }
}
EXOPLANET_TYPE_PROBABILITIES = {
    'super_earth': 0.35,
    'hot_jupiter': 0.25,
    'ice_giant': 0.20,
    'ocean_world': 0.15,
    'rogue_planet': 0.05
}

# ===== ATLANTEAN ENHANCEMENTS =====

# Solfeggio Frequencies - Ancient healing tones
SOLFEGGIO_FREQUENCIES = {
    174: {'name': 'Foundation', 'effect': 'pain_relief', 'desc': 'natural anesthetic', 'bonus': 'shield', 'mult': 1.1},
    285: {'name': 'Quantum', 'effect': 'tissue_healing', 'desc': 'cellular regeneration', 'bonus': 'minor_heal', 'mult': 0.5},
    396: {'name': 'Liberation', 'effect': 'release_fear', 'desc': 'liberating guilt and fear', 'bonus': 'stability', 'mult': 1.2},
    417: {'name': 'Transmutation', 'effect': 'facilitate_change', 'desc': 'undoing situations', 'bonus': 'rift_assist', 'mult': 1.15},
    432: {'name': 'Natural Harmony', 'effect': 'universal_tuning', 'desc': 'cosmic frequency', 'bonus': 'base_heal', 'mult': 1.0},
    528: {'name': 'Miracle', 'effect': 'transformation', 'desc': 'DNA repair, love frequency', 'bonus': 'major_heal', 'mult': 2.0},
    639: {'name': 'Connection', 'effect': 'relationships', 'desc': 'harmonizing connections', 'bonus': 'comm_boost', 'mult': 1.3},
    741: {'name': 'Awakening', 'effect': 'expression', 'desc': 'awakening intuition', 'bonus': 'rift_detect', 'mult': 1.4},
    852: {'name': 'Intuition', 'effect': 'spiritual_order', 'desc': 'returning to spiritual order', 'bonus': 'third_eye', 'mult': 1.25},
    963: {'name': 'Divine', 'effect': 'oneness', 'desc': 'connection to Source', 'bonus': 'transcend', 'mult': 1.5},
}
SOLFEGGIO_TOLERANCE = 5.0  # Hz tolerance for detecting solfeggio frequencies

# Crystal Color Spectrum (frequency to chakra color mapping)
CRYSTAL_SPECTRUM = {
    'ruby': {'freq_range': (200, 285), 'color': (220, 20, 60), 'chakra': 'root', 'bonus': 'stability', 'mult': 1.2},
    'carnelian': {'freq_range': (285, 350), 'color': (255, 127, 80), 'chakra': 'sacral', 'bonus': 'crystal_find', 'mult': 1.3},
    'citrine': {'freq_range': (350, 417), 'color': (255, 215, 0), 'chakra': 'solar_plexus', 'bonus': 'velocity', 'mult': 1.15},
    'emerald': {'freq_range': (417, 528), 'color': (0, 201, 87), 'chakra': 'heart', 'bonus': 'integrity', 'mult': 1.25},
    'lapis': {'freq_range': (528, 639), 'color': (38, 97, 156), 'chakra': 'throat', 'bonus': 'scan_range', 'mult': 1.4},
    'amethyst': {'freq_range': (639, 741), 'color': (153, 102, 204), 'chakra': 'third_eye', 'bonus': 'rift_detect', 'mult': 1.35},
    'quartz': {'freq_range': (741, 800), 'color': (255, 255, 255), 'chakra': 'crown', 'bonus': 'universal', 'mult': 1.1},
}

# Temple of Regeneration (110 Hz resonance - ancient temple frequency)
TEMPLE_RESONANCE_FREQ = 110.0  # Hz - Malta Hypogeum, Newgrange frequency
TEMPLE_RESONANCE_RANGE = (95.0, 120.0)  # Acceptable range for temple effects
TEMPLE_COUNT = 7  # Number of temples in universe (one per chakra)
TEMPLE_HEALING_RATE = 0.02  # Integrity per second when in temple resonance
TEMPLE_CONSCIOUSNESS_BOOST = 1.5  # Multiplier for consciousness-related bonuses

# Merkaba Activation (Star Tetrahedron - vehicle of light)
MERKABA_ACTIVATION_THRESHOLD = 0.9  # All 5 dimensions must be above this
MERKABA_SHIELD_STRENGTH = 0.5  # Damage reduction when active
MERKABA_VELOCITY_BOOST = 1.3  # Speed multiplier when active
MERKABA_DETECTION_RANGE = 2.0  # Multiplier for rift/crystal detection

# Tuaoi Crystal Modes (6-sided hexagonal prism)
TUAOI_MODES = {
    'healing': {
        'freq_base': 432.0,
        'color': (0, 255, 128),
        'effect': 'integrity_regen',
        'rate': 0.01,  # Integrity per second
        'desc': 'Atlantean healing frequency'
    },
    'navigation': {
        'freq_base': PHI * 256,  # ~414 Hz
        'color': (100, 150, 255),
        'effect': 'enhanced_autopilot',
        'rate': 1.5,  # Autopilot efficiency multiplier
        'desc': 'Golden ratio navigation'
    },
    'communication': {
        'freq_base': 7.83,  # Schumann resonance
        'color': (255, 200, 100),
        'effect': 'expanded_scan',
        'rate': 2.0,  # Scan range multiplier
        'desc': 'Earth resonance connection'
    },
    'power': {
        'freq_base': 528.0,
        'color': (255, 100, 100),
        'effect': 'velocity_boost',
        'rate': 1.25,  # Max velocity multiplier
        'desc': 'Miracle frequency power'
    },
    'regeneration': {
        'freq_base': 285.0,
        'color': (200, 100, 255),
        'effect': 'resonance_recovery',
        'rate': 1.3,  # Resonance width multiplier
        'desc': 'Cellular regeneration frequency'
    },
    'transcendence': {
        'freq_base': 963.0,
        'color': (255, 255, 200),
        'effect': 'higher_dim_sensitivity',
        'rate': 1.4,  # Higher dimension bonus
        'desc': 'Divine connection frequency'
    },
}
TUAOI_MODE_SWITCH_COOLDOWN = 2.0  # Seconds between mode switches

# Halls of Amenti (ultimate destination)
AMENTI_RESONANCE_THRESHOLD = 0.95  # All 5 dims must exceed this
AMENTI_TIME_DILATION = 0.5  # Game speed multiplier inside Amenti
AMENTI_WISDOM_BONUS = 2.0  # Permanent multiplier after visiting

# Sacred Geometry Patterns (for crystal arrangements on planets)
SACRED_PATTERNS = {
    'vesica_piscis': {'points': 2, 'bonus': 'creation', 'mult': 1.2},
    'seed_of_life': {'points': 7, 'bonus': 'crystal_regen', 'mult': 1.5},
    'flower_of_life': {'points': 19, 'bonus': 'all_harmonics', 'mult': 2.0},
    'metatrons_cube': {'points': 13, 'bonus': 'max_resonance', 'mult': 1.8},
    'merkaba': {'points': 8, 'bonus': 'protection', 'mult': 1.6},
    'golden_spiral': {'points': 5, 'bonus': 'phi_stacking', 'mult': PHI},
}

# Brainwave States (consciousness levels)
BRAINWAVE_STATES = {
    'delta': {'freq_range': (0.5, 4.0), 'state': 'deep_healing', 'effect': 'auto_repair', 'mult': 2.0},
    'theta': {'freq_range': (4.0, 8.0), 'state': 'meditation', 'effect': 'rift_vision', 'mult': 1.5},
    'alpha': {'freq_range': (8.0, 13.0), 'state': 'relaxed_focus', 'effect': 'enhanced_scan', 'mult': 1.3},
    'beta': {'freq_range': (13.0, 30.0), 'state': 'active', 'effect': 'fast_tuning', 'mult': 1.2},
    'gamma': {'freq_range': (30.0, 100.0), 'state': 'transcendence', 'effect': 'all_bonus', 'mult': 1.4},
}

# Atlantean Terminology Mapping
ATLANTEAN_TERMS = {
    'rift': 'Harmonic Chamber',
    'rifts': 'Harmonic Chambers',
    'crystal': 'Atlantean Crystal',
    'crystals': 'Atlantean Crystals',
    'upgrade': 'Attunement',
    'upgrades': 'Attunements',
    'landed': 'Anchored',
    'landing': 'Anchoring',
    'takeoff': 'Ascension',
    'meditation': 'Atla-Ra Meditation',
    'resonance': 'Harmonic Alignment',
    'frequency': 'Vibrational Tone',
    'dimension': 'Realm',
    'ship': 'Light Vehicle',
}

# ===== LEY LINE HIGHWAYS =====
# Energy pathways connecting temples and power centers
LEY_LINE_COUNT = 12  # Number of ley lines in universe
LEY_LINE_SPEED_MULT = 3.0  # Velocity multiplier when on ley line
LEY_LINE_WIDTH = 8.0  # Distance from ley line center to be "on" it
LEY_LINE_DETECTION_RANGE = 25.0  # Range to detect nearby ley lines
LEY_LINE_FREQ = 432.0  # Natural ley line resonance frequency

# ===== PORTAL ANCHOR SYSTEM =====
# Bookmark locations using crystals as anchors
MAX_PORTAL_ANCHORS = 7  # Maximum anchors (one per chakra)
PORTAL_ANCHOR_COST = 3  # Crystals required to create anchor
PORTAL_TRAVEL_RESONANCE = 0.85  # Required resonance to use portal
PORTAL_COOLDOWN = 30.0  # Seconds between portal uses

# ===== CRYSTAL ACTIVATION SEQUENCES =====
# 5-step ritual for awakening dormant crystals
ACTIVATION_SEQUENCE_LENGTH = 5  # Steps in activation ritual
ACTIVATION_FREQUENCIES = [396, 417, 528, 639, 741]  # Solfeggio sequence
ACTIVATION_TOLERANCE = 8.0  # Hz tolerance for each step
ACTIVATION_TIME_LIMIT = 30.0  # Seconds to complete sequence
ACTIVATION_REWARD_MULT = 2.0  # Crystal value multiplier when activated

# ===== 12+1 TEMPLE SYSTEM =====
# 12 minor temples + 1 Master Temple (Halls of Amenti)
MINOR_TEMPLE_COUNT = 12  # Zodiac temples
TEMPLE_KEY_NAMES = [
    'Aries', 'Taurus', 'Gemini', 'Cancer', 'Leo', 'Virgo',
    'Libra', 'Scorpio', 'Sagittarius', 'Capricorn', 'Aquarius', 'Pisces'
]
TEMPLE_KEY_FREQUENCIES = [
    396, 417, 432, 444, 480, 512, 528, 576, 594, 639, 672, 741
]  # Each temple's unique frequency
MASTER_TEMPLE_UNLOCK_KEYS = 12  # All keys needed for Master Temple

# ===== PYRAMID RESONANCE CHAMBERS =====
# Special structures with 117-121 Hz (Great Pyramid frequency)
PYRAMID_RESONANCE_FREQ = 118.0  # Hz - Great Pyramid King's Chamber
PYRAMID_RESONANCE_RANGE = (117.0, 121.0)  # Acceptable range
PYRAMID_HEALING_MULT = 3.0  # Enhanced healing in pyramid
PYRAMID_CONSCIOUSNESS_BOOST = 2.0  # Consciousness gain multiplier
PYRAMID_COUNT = 3  # Number of pyramids in universe

# ===== ATLANTEAN CRYSTAL TYPES =====
# Special crystal varieties with unique properties
ATLANTEAN_CRYSTAL_TYPES = {
    'fire_crystal': {
        'color': (255, 69, 0),
        'freq_range': (200, 300),
        'effect': 'velocity_burst',
        'mult': 2.0,
        'desc': 'Volcanic energy crystal from Atlantean forges'
    },
    'aquamarine': {
        'color': (127, 255, 212),
        'freq_range': (300, 400),
        'effect': 'shield_boost',
        'mult': 1.5,
        'desc': 'Ocean-born crystal of protection'
    },
    'larimar': {
        'color': (135, 206, 235),
        'freq_range': (400, 500),
        'effect': 'communication',
        'mult': 1.8,
        'desc': 'Dolphin stone of ancient Atlantean wisdom'
    },
    'moldavite': {
        'color': (154, 205, 50),
        'freq_range': (500, 600),
        'effect': 'transformation',
        'mult': 2.5,
        'desc': 'Extraterrestrial glass of rapid evolution'
    },
    'lemurian_seed': {
        'color': (255, 182, 193),
        'freq_range': (600, 700),
        'effect': 'memory_unlock',
        'mult': 2.0,
        'desc': 'Ancient knowledge carrier from Lemuria'
    },
    'black_tourmaline': {
        'color': (47, 79, 79),
        'freq_range': (100, 200),
        'effect': 'purification',
        'mult': 1.3,
        'desc': 'Protective stone against negative frequencies'
    },
    'celestite': {
        'color': (176, 224, 230),
        'freq_range': (700, 800),
        'effect': 'angelic_connection',
        'mult': 1.7,
        'desc': 'Bridge to higher realms and celestial beings'
    }
}
ATLANTEAN_CRYSTAL_CHANCE = 0.15  # Chance of finding special crystal

# ===== CONSCIOUSNESS LEVEL SYSTEM =====
# Progression through levels of awareness
CONSCIOUSNESS_LEVELS = {
    'dormant': {'threshold': 0.0, 'mult': 1.0, 'desc': 'Unawakened state'},
    'awakening': {'threshold': 0.3, 'mult': 1.2, 'desc': 'Beginning to sense the harmonics'},
    'aware': {'threshold': 0.5, 'mult': 1.4, 'desc': 'Consciously navigating frequencies'},
    'attuned': {'threshold': 0.7, 'mult': 1.6, 'desc': 'Deeply connected to cosmic vibrations'},
    'enlightened': {'threshold': 0.85, 'mult': 1.8, 'desc': 'Mastery of harmonic navigation'},
    'ascended': {'threshold': 0.95, 'mult': 2.0, 'desc': 'One with the universal frequency'}
}
CONSCIOUSNESS_GAIN_RATE = 0.001  # Per second at high resonance
CONSCIOUSNESS_DECAY_RATE = 0.0005  # Per second at low resonance

# ===== ASTRAL PROJECTION MODE =====
# Out-of-body exploration for scouting
ASTRAL_PROJECTION_RESONANCE = 0.9  # Required resonance to enter
ASTRAL_PROJECTION_RANGE = 200.0  # Maximum distance from body
ASTRAL_SPEED_MULT = 5.0  # Fast exploration speed
ASTRAL_DURATION = 30.0  # Maximum seconds in astral form
ASTRAL_RETURN_KEY = 'b'  # Key to return to body
ASTRAL_COOLDOWN = 60.0  # Cooldown after returning

# ===== INTENTION-BASED NAVIGATION =====
# Thought-directed travel system
INTENTION_ACTIVATION_TIME = 5.0  # Seconds of focused intention
INTENTION_RESONANCE_THRESHOLD = 0.8  # Required resonance for intention
INTENTION_RANGE = 100.0  # Maximum intention-travel distance
INTENTION_PRECISION = 0.9  # Accuracy of arrival (0.9 = within 10%)

# ===== CYMATICS VISUALIZATION =====
# Visual patterns from sound frequencies
CYMATICS_ENABLED = True
CYMATICS_PATTERNS = {
    'hexagon': {'freq_range': (200, 300), 'complexity': 6},
    'star': {'freq_range': (300, 400), 'complexity': 5},
    'flower': {'freq_range': (400, 500), 'complexity': 12},
    'mandala': {'freq_range': (500, 600), 'complexity': 8},
    'spiral': {'freq_range': (600, 700), 'complexity': PHI},
    'merkaba': {'freq_range': (700, 800), 'complexity': 24}
}

# ===== HALLS OF AMENTI (MASTER TEMPLE) =====
HALLS_OF_AMENTI_POS = np.array([0.0, 0.0, 0.0, 0.0, 0.0])  # Center of universe
AMENTI_ENTRY_REQUIREMENTS = {
    'all_keys': True,  # Must have all 12 temple keys
    'consciousness': 'enlightened',  # Minimum consciousness level
    'resonance': AMENTI_RESONANCE_THRESHOLD,  # All dims above this
    'merkaba': True  # Must have Merkaba active
}
AMENTI_REWARDS = {
    'permanent_resonance_boost': PHI,
    'crystal_multiplier': 3.0,
    'consciousness_unlock': 'ascended',
    'new_dimension_access': True  # Unlocks 6th dimension (future feature)
}
