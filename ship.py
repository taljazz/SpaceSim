"""
Ship class for the Golden Spiral Spaceship Simulator.

This module contains the Ship class which manages all game state and logic
including physics, navigation, upgrades, landing, rift interaction, and UI.
"""

import numpy as np
import random
import pickle
import time
import threading
import wave
import sounddevice as sd
import pygame
import os
from cytolk import tolk
from constants import *
from audio_system import SoundEffect
from utils import project_to_2d
from celestial import generate_celestial

class Ship:
    def __init__(self, config, audio_system):
        """
        Initialize the Ship.

        Args:
            config: ConfigParser object with game settings
            audio_system: AudioSystem instance for playing sounds
        """
        self.config = config
        self.audio_system = audio_system

        # Initialize ship position, velocity, and heading
        self.position = np.zeros(N_DIMENSIONS)  # Ship position in all dimensions
        self.velocity = np.zeros(N_DIMENSIONS)  # Ship velocity in all dimensions
        self.heading = 0.0  # Ship heading (unused for now)
        # Drive and target frequencies
        self.r_drive = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]  # Drive frequencies
        self.base_f_target = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]  # Base target frequencies
        self.f_target = self.base_f_target[:]  # Current target frequencies
        # Tuning and mode flags
        self.selected_dim = 0  # Currently selected dimension for tuning
        self.tuning_mode = False  # False: manual mode (only higher dims tunable), True: resonance tuning mode (all dims)
        # Proximity and resonance tracking
        self.near_object = False  # Flag for nearby celestial object
        self.resonance_levels = np.zeros(N_DIMENSIONS)  # Resonance per dimension
        # View and rotation controls
        self.view_rotation = 0.0  # View rotation for projection
        self.rotating_left = False  # Flag for left rotation
        self.rotating_right = False  # Flag for right rotation
        self.was_rotating_left = False  # Previous state for rotation start detection
        self.was_rotating_right = False  # Previous state for rotation start detection
        self.last_rotation_sound_time = 0.0  # Time of last rotation sound play
        self.prev_view_rotation = 0.0  # Previous view rotation for delta
        self.last_landmark_speak_time = 0.0  # Cooldown for landmark speech
        self.last_approaching_beep_time = 0.0  # Cooldown for approaching beep
        # Landing and exploration states
        self.landed_mode = False  # Flag for landed on planet
        self.landed_planet = None  # Landed planet position
        self.landed_planet_body = None  # Full planet body dictionary
        self.landing_timer = 0.0  # Timer for landing sequence
        self.resonance_integrity = 1.0  # Ship integrity level
        self.crystals_collected = 0  # Total crystals collected
        # Power and dissonance management
        self.resonance_power = np.zeros(N_DIMENSIONS)  # Power buildup per dimension
        self.dissonance_timer = 0.0  # Timer for dissonance buildup
        # User interface settings
        self.verbose_mode = config.getint('Settings', 'verbose_mode', fallback=1)  # Verbosity level (0 low, 1 medium, 2 high)
        self.hud_text_size = config.getint('Settings', 'hud_text_size', fallback=HUD_TEXT_SIZE_BASE)  # Current HUD text size
        self.high_contrast = config.getboolean('Settings', 'high_contrast', fallback=HIGH_CONTRAST)  # High contrast mode flag
        self.autosave_enabled = config.getboolean('Settings', 'autosave_enabled', fallback=True)  # Autosave toggle
        # Phase 1.5 settings
        self.ambient_sounds_enabled = config.getboolean('Settings', 'ambient_sounds_enabled', fallback=True)  # Proximity ambient audio toggle
        self.nebula_dissonance_enabled = config.getboolean('Settings', 'nebula_dissonance_enabled', fallback=True)  # Nebula dissonance effects toggle
        self.last_autosave_time = 0.0  # Time of last autosave
        # Upgradable attributes
        self.resonance_width = RESONANCE_WIDTH_BASE  # Current resonance width
        self.max_velocity = MAX_VELOCITY_BASE  # Current max velocity
        self.crystal_count = CRYSTAL_COUNT_BASE  # Crystals per planet
        self.crystal_bonus = 0  # Bonus to crystal count
        # Previous state tracking
        self.prev_resonance_levels = np.zeros(N_DIMENSIONS)  # Previous resonance levels
        # Rift management
        self.rifts = []  # List of rifts: {'pos': np.array, 'timer': float, 'type': str, 'sound': SoundEffect, 'self.last_beep_time': float}
        # Input debounce flags
        self.last_click_time = [0.0] * N_DIMENSIONS  # Last click times per dimension
        self.verbose_toggled = False  # Flag to debounce verbosity toggle
        self.contrast_toggled = False  # Flag to debounce contrast toggle
        self.text_size_adjusted = False  # Flag to debounce text size adjustment
        self.instructions_opened = False  # Flag to debounce instructions open
        self.tuning_mode_toggled = False  # Flag to debounce tuning mode toggle
        # HUD dialog
        self.hud_mode = False  # HUD mode flag
        self.hud_index = 0  # Current HUD item index
        self.hud_items = []  # List of HUD items
        # Planet exploration
        self.cursor_pos = np.array([0, 0])  # Cursor position on planet grid
        self.crystal_positions = []  # Crystal positions on planet
        self.crystal_freqs = []  # Crystal frequencies
        self.locked_crystals = set()  # Collected crystal indices
        self.planet_biome = 'harmonic'  # Planet biome type
        self.approaching_lock_announced = False  # Flag for approaching lock announcement
        # Upgrades
        self.upgrade_mode = False  # Upgrade menu flag
        self.upgrades = [
            {'name': 'Resonance Width', 'cost': UPGRADE_COSTS[0], 'effect': self.upgrade_width, 'desc': 'Increases tuning tolerance by golden increment.'},
            {'name': 'Integrity Repair', 'cost': UPGRADE_COSTS[1], 'effect': self.upgrade_integrity, 'desc': 'Restores ship harmony.'},
            {'name': 'Max Velocity', 'cost': UPGRADE_COSTS[2], 'effect': self.upgrade_velocity, 'desc': 'Boosts top speed with divine proportion.'},
            {'name': 'Auto-Tune Helper', 'cost': UPGRADE_COSTS[3], 'effect': self.auto_tune, 'desc': 'Subtly aligns frequencies automatically.'},
            {'name': 'Crystal Growth', 'cost': UPGRADE_COSTS[4], 'effect': self.upgrade_crystal_count, 'desc': 'Increases crystals per planet.'},
            {'name': 'Golden Harmony Mode', 'cost': UPGRADE_COSTS[5], 'effect': self.activate_golden_harmony, 'desc': 'Permanent PHI multiplier to all stats for ascension prep.'}
        ]
        self.golden_harmony_active = False  # Golden harmony flag
        # Starmap
        self.starmap_mode = False  # Starmap mode flag
        self.starmap_index = 0  # Current starmap item index
        self.starmap_items = []  # List of starmap items (now dicts: {'label': str, 'pos': array, 'type': str})
        self.locked_target = None  # Locked target position
        self.lock_sound = None  # Lock sound effect
        self.locked_is_rift = False  # Flag if locked target is rift
        # Rift selection
        self.rift_selection_mode = False  # Rift selection mode flag
        self.rift_selection_index = 0  # Current rift item index
        self.rift_items = []  # List of rift items (now dicts: {'label': str, 'pos': array, 'type': str, 'rift': dict})
        self.locked_rift = None  # Locked rift dict
        self.last_cursor_pos = np.array([0, 0])  # Last cursor position
        self.last_cursor_speak_time = 0.0  # Debounce for cursor speech
        self.nearest_body = None  # Nearest celestial body
        self.ship_heading = 0.0  # Ship yaw orientation (future use)
        self.pitch = 0.0  # Ship pitch orientation (optional)
        self.speed_mode = 2  # Speed mode: 0 - Approach, 1 - Cruise, 2 - Quantum
        self.speed_mode_toggled = False  # Flag to debounce speed mode toggle
        # Rift charge and guidance
        self.rift_charge_timer = 0.0  # Timer for rift charge sequence
        self.last_guidance_time = 0.0  # Last time guidance speech was given
        self.approached_rift_announced = False  # Flag for approached rift announcement
        self.prev_rift_dist = float('inf')  # Previous rift distance for spam check
        self.prev_rift_align = 0.0  # Previous rift alignment for spam check
        self.prev_rift_res = 0.0  # Previous rift resonance for spam check
        # Proximity ambient sounds
        self.star_sound = None
        self.nebula_sound = None
        self.planet_sound = None
        # New: Sing mode
        self.sing_mode = False
        self.sing_toggled = False
        self.sing_active = False  # Flag for thread
        self.detected_pitch = None
        self.pitch_thread = None
        self.last_detected_rhythm = 60.0  # Default heartbeat BPM
        self.last_sing_time = 0.0  # Last time pitch was detected
        # New: Idle mode
        self.last_input_time = time.time()
        self.idle_mode = False
        # New: Biome sound
        self.biome_sound = None
        # New: Water blessing
        self.spacebar_hold_timer = 0.0
        self.spacebar_pressed = False

        # Tracking for speak_with_cooldown
        self.last_spoken = {}
        self.simulation_time = 0.0
        self.last_beep_time = -1.0
        self.easter_egg_announced = False  # Prevent easter egg spam

        # Flag for universe regeneration (used after ascension)
        self.needs_universe_regeneration = False

        # Harmonic relationship tracking
        self.active_harmonics = {}  # Dict of {harmonic_name: (dimensions_tuple, expiry_time)}
        self.last_harmonic_check = 0.0

        # ===== ATLANTEAN ENHANCEMENTS =====
        # Tuaoi Crystal mode (6-sided hexagonal prism)
        self.tuaoi_mode = 'navigation'  # Current mode: healing, navigation, communication, power, regeneration, transcendence
        self.tuaoi_mode_index = 1  # Index in mode list for cycling
        self.last_tuaoi_switch = 0.0  # Cooldown timer

        # Merkaba activation (star tetrahedron)
        self.merkaba_active = False  # True when all 5 dimensions > MERKABA_ACTIVATION_THRESHOLD
        self.merkaba_announced = False  # Prevent spam announcements

        # Solfeggio frequency bonuses
        self.active_solfeggio = {}  # Dict of {freq: (effect, expiry_time)}
        self.last_solfeggio_check = 0.0

        # Temple resonance tracking
        self.in_temple_resonance = False  # True when drive matches 110 Hz temple frequency
        self.temple_announced = False

        # Consciousness/brainwave state
        self.consciousness_level = 'beta'  # Current brainwave state
        self.consciousness_announced = False

        # ===== ADDITIONAL ATLANTEAN STATE VARIABLES =====
        # Sacred Geometry Crystal Patterns
        self.current_pattern = None  # Detected sacred geometry pattern
        self.pattern_progress = []  # Crystal collection sequence
        self.pattern_bonus_timer = 0.0  # Timer for pattern bonuses

        # Temple Key System (12+1 temples)
        self.temple_keys = set()  # Collected temple keys by index
        self.last_temple_check = 0.0
        self.near_temple = None  # Current nearby temple
        self.temple_nearby_announced = False  # Prevent "nearby" spam
        self.amenti_sealed_announced = False  # Prevent "sealed" spam

        # Ley Line Navigation
        self.on_ley_line = False
        self.current_ley_line = None
        self.ley_line_announced = False

        # Portal Anchor System
        self.portal_anchors = []  # List of {'pos': array, 'name': str, 'freq': float}
        self.last_portal_use = 0.0  # Cooldown timer

        # Crystal Activation Sequences
        self.activation_sequence_active = False
        self.activation_step = 0
        self.activation_timer = 0.0
        self.activation_target_crystal = None

        # Pyramid Proximity
        self.near_pyramid = None
        self.pyramid_announced = False

        # Consciousness Level (float 0.0 to 1.0)
        self.consciousness_value = 0.3  # Starting at 'awakening' level
        self.consciousness_name = 'awakening'

        # Astral Projection Mode
        self.astral_mode = False
        self.astral_body_pos = None  # Original position when projecting
        self.astral_timer = 0.0
        self.last_astral_return = 0.0

        # Intention-Based Navigation
        self.intention_active = False
        self.intention_timer = 0.0
        self.intention_target = None

        # Halls of Amenti state
        self.visited_amenti = False
        self.amenti_blessing_active = False

        # Frequency Preset System (save/recall favorite frequency configurations)
        self.frequency_presets = {}  # Dict of {slot_number: [freq1, freq2, freq3, freq4, freq5]}
        self.pending_preset_overwrite = None  # Slot number awaiting overwrite confirmation
        self.pending_preset_time = 0.0  # Time when overwrite warning was given

        # Celestial body references (set by main.py after universe generation)
        self.stars = []
        self.planets = []
        self.nebulae = []

    def speak(self, msg):
        """Helper method to speak with cooldown."""
        if msg not in self.last_spoken or self.simulation_time - self.last_spoken[msg] > SPEECH_COOLDOWN:
            tolk.speak(msg)
            self.last_spoken[msg] = self.simulation_time

    def get_effective_scan_range(self):
        """Get effective interaction distance, boosted by Communication mode."""
        base_range = INTERACTION_DISTANCE
        if self.tuaoi_mode == 'communication':
            base_range *= TUAOI_MODES['communication']['rate']  # 2.0x range
        return base_range

    def get_crystal_type(self, frequency):
        """Determine crystal type based on frequency (Atlantean color spectrum)."""
        for crystal_name, info in CRYSTAL_SPECTRUM.items():
            freq_min, freq_max = info['freq_range']
            if freq_min <= frequency < freq_max:
                return crystal_name, info
        # Default to quartz if out of range
        return 'quartz', CRYSTAL_SPECTRUM['quartz']

    def get_atlantean_term(self, term):
        """Get Atlantean terminology for a game term."""
        return ATLANTEAN_TERMS.get(term.lower(), term)

    # Upgrade function for resonance width
    def upgrade_width(self):
        # Increase resonance width by a golden ratio increment
        self.resonance_width += PHI * 0.5

    # Upgrade function for integrity
    def upgrade_integrity(self):
        # Restore ship integrity by a golden ratio amount, capped at 1.0
        self.resonance_integrity = min(1.0, self.resonance_integrity + PHI * 0.2)

    # Upgrade function for max velocity
    def upgrade_velocity(self):
        # Multiply max velocity by the golden ratio
        self.max_velocity *= PHI

    # Auto-tune helper upgrade
    def auto_tune(self):
        # Subtly adjust drive frequencies towards targets
        for i in range(N_DIMENSIONS):
            self.r_drive[i] += (self.f_target[i] - self.r_drive[i]) * 0.1

    # Upgrade for crystal count bonus
    def upgrade_crystal_count(self):
        # Increase bonus crystals per planet
        self.crystal_bonus += 1

    # Activate golden harmony mode
    def activate_golden_harmony(self):
        # Activate golden harmony, applying PHI multipliers to stats
        self.golden_harmony_active = True
        self.max_velocity *= PHI
        self.resonance_width *= PHI
        self.speak("Golden Harmony activated. The universe sings in perfect proportion.")

    # Generate crystals on landed planet
    def generate_crystals(self):
        # Reset crystal data and generate new positions/freqs based on biome
        self.crystal_positions = []
        self.crystal_freqs = []
        self.locked_crystals = set()
        self.planet_biome = random.choice(['harmonic', 'dissonant'])
        self.pattern_progress = []  # Reset pattern progress

        # Apply exoplanet type crystal multiplier
        crystal_mult = 1.0
        if self.landed_planet_body and 'crystal_mult' in self.landed_planet_body:
            crystal_mult = self.landed_planet_body['crystal_mult']

        # Calculate base crystal count
        base_count = random.randint(1 + self.crystal_bonus, 8 + self.crystal_bonus)
        # Apply multiplier and round to integer
        self.crystal_count = int(base_count * crystal_mult)
        # Ensure at least 1 crystal
        self.crystal_count = max(1, self.crystal_count)

        # Detect sacred geometry pattern based on crystal count
        self.current_pattern = None
        for pattern_name, pattern_info in SACRED_PATTERNS.items():
            if pattern_info['points'] == self.crystal_count:
                self.current_pattern = pattern_name
                break

        exoplanet_type = self.landed_planet_body.get('exoplanet_type', 'super_earth') if self.landed_planet_body else 'super_earth'
        exoplanet_desc = EXOPLANET_TYPES[exoplanet_type]['desc']

        pattern_msg = ""
        if self.current_pattern:
            pattern_msg = f" Sacred {self.current_pattern.replace('_', ' ').title()} pattern detected!"
        self.speak(f"Anchored on {self.planet_biome} biome planet. {exoplanet_desc.capitalize()}. {self.crystal_count} Atlantean crystals detected.{pattern_msg}")

        # Generate crystals in sacred geometry positions if pattern detected
        for i in range(self.crystal_count):
            if self.current_pattern == 'seed_of_life' and self.crystal_count == 7:
                # Seed of Life: 1 center + 6 in hexagon
                if i == 0:
                    pos = np.array([0.0, 0.0])
                else:
                    angle = (i - 1) * (2 * np.pi / 6)
                    r = SCALE_FACTOR / 10
                    pos = np.array([r * np.cos(angle), r * np.sin(angle)])
            elif self.current_pattern == 'merkaba' and self.crystal_count == 8:
                # Merkaba: 2 tetrahedra (8 vertices)
                if i < 4:
                    angle = i * (2 * np.pi / 4) + np.pi / 4
                    r = SCALE_FACTOR / 10
                    pos = np.array([r * np.cos(angle), r * np.sin(angle)])
                else:
                    angle = (i - 4) * (2 * np.pi / 4)
                    r = SCALE_FACTOR / 10 * PHI
                    pos = np.array([r * np.cos(angle), r * np.sin(angle)])
            elif self.current_pattern == 'golden_spiral' and self.crystal_count == 5:
                # Golden Spiral: Fibonacci positions
                theta = i * 2 * np.pi * PHI
                r = FIB_SEQ[i % len(FIB_SEQ)] * (SCALE_FACTOR / 10)
                pos = np.array([r * np.cos(theta), r * np.sin(theta)])
            else:
                # Default golden spiral for other patterns
                theta = i * 2 * np.pi * PHI
                r = FIB_SEQ[i % len(FIB_SEQ)] * (SCALE_FACTOR / 10)
                pos = np.array([r * np.cos(theta), r * np.sin(theta)])

            self.crystal_positions.append(pos)

            # Assign Atlantean crystal type with chance
            if random.random() < ATLANTEAN_CRYSTAL_CHANCE:
                # Special Atlantean crystal
                crystal_type = random.choice(list(ATLANTEAN_CRYSTAL_TYPES.keys()))
                crystal_info = ATLANTEAN_CRYSTAL_TYPES[crystal_type]
                freq_min, freq_max = crystal_info['freq_range']
                base_freq = random.uniform(freq_min, freq_max)
                freqs = [base_freq + random.uniform(-20, 20) for _ in range(N_DIMENSIONS)]
                self.crystal_freqs.append({'freqs': freqs, 'atlantean_type': crystal_type, 'special': True})
            else:
                # Regular crystal with chakra type
                freqs = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]
                self.crystal_freqs.append({'freqs': freqs, 'atlantean_type': None, 'special': False})

        freq_str = ', '.join([f"{f['freqs'][0]:.2f}" for f in self.crystal_freqs])
        self.speak(f"Crystals detected at frequencies: {freq_str} Hz in primary dim.")
        self.approaching_lock_announced = False  # Reset flag

        # Play biome sound
        if self.biome_sound:
            if self.biome_sound in self.audio_system.active_sound_effects:
                self.audio_system.active_sound_effects.remove(self.biome_sound)
        if self.planet_biome == 'harmonic':
            self.biome_sound = SoundEffect(self.audio_system.chord_waveform, loop=True, volume=self.audio_system.effect_volume * 0.5)
        else:
            self.biome_sound = SoundEffect(self.audio_system.dissonant_waveform, loop=True, volume=self.audio_system.effect_volume * 0.5)
        self.audio_system.active_sound_effects.append(self.biome_sound)

    # New: Continuous pitch detection in thread
    def continuous_pitch_detection(self):
        while self.sing_active:
            pitch = self.detect_pitch()
            if pitch and FREQUENCY_RANGE[0] <= pitch <= FREQUENCY_RANGE[1]:
                self.r_drive[self.selected_dim] = pitch
                self.speak(f"Tuned to hummed pitch {pitch:.2f} Hz.")
                self.last_sing_time = time.time()
                self.last_detected_rhythm = self.calculate_rhythm(pitch)  # Placeholder for rhythm detection
            time.sleep(0.1)  # Prevent CPU overload

    # Placeholder for rhythm calculation (e.g., based on pitch changes or timing)
    def calculate_rhythm(self, pitch):
        # For now, default to 60 BPM; in future, analyze timing between pitches
        return 60.0

    # New: Detect pitch from mic
    def detect_pitch(self):
        try:
            recording = sd.rec(int(PITCH_RECORD_DURATION * SAMPLE_RATE), samplerate=SAMPLE_RATE, channels=1)
            sd.wait()
            y = recording[:, 0]
            # Simple FFT pitch detection
            fft_data = np.fft.fft(y)
            freqs = np.fft.fftfreq(len(y), 1/SAMPLE_RATE)
            peak = np.argmax(np.abs(fft_data))
            pitch = abs(freqs[peak])
            return pitch
        except Exception as e:
            self.speak("No microphone detected or error in pitch detection.")
            return None

    # Handle user input
    def handle_input(self, keys, events, stars, planets, nebulae):
        # No global variables needed anymore - using self.audio_system
        global font  # Keep font as global for pygame rendering
        # Update last input time for idle detection
        if any(keys) or events:
            self.last_input_time = time.time()
            if self.idle_mode:
                self.idle_mode = False
                self.speak("Resuming active control.")
        # Check if in menu modes; handle navigation if so
        if self.hud_mode or self.upgrade_mode or self.starmap_mode or self.rift_selection_mode:
            if self.rift_selection_mode:
                mode = 'rift'
            elif self.starmap_mode:
                mode = 'starmap'
            elif self.upgrade_mode:
                mode = 'upgrade'
            else:
                mode = 'hud'
            for event in events:
                if event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_m and mode == 'starmap':
                        self.starmap_mode = False
                        self.speak("Exiting starmap.")
                    elif event.key == pygame.K_e and mode == 'rift':
                        self.rift_selection_mode = False
                        self.speak("Exiting rift selection.")
                    elif event.key == pygame.K_u and (mode == 'hud' or mode == 'upgrade'):
                        self.hud_mode = False
                        self.upgrade_mode = False
                        self.speak("Exiting menu.")
                    elif event.key == pygame.K_UP:
                        if mode in ['starmap', 'rift'] and len(self.starmap_items if mode == 'starmap' else self.rift_items) > 1:
                            index_attr = 'starmap_index' if mode == 'starmap' else 'rift_selection_index'
                            items_attr = 'starmap_items' if mode == 'starmap' else 'rift_items'
                            setattr(self, index_attr, (getattr(self, index_attr) - 1) % len(getattr(self, items_attr)))
                            if mode == 'starmap':
                                self.speak_starmap_item()
                            else:
                                self.speak_rift_item()
                        elif mode not in ['starmap', 'rift'] and len(self.hud_items) > 1:
                            self.hud_index = (self.hud_index - 1) % len(self.hud_items)
                            self.speak_hud_item()
                    elif event.key == pygame.K_DOWN:
                        if mode in ['starmap', 'rift'] and len(self.starmap_items if mode == 'starmap' else self.rift_items) > 1:
                            index_attr = 'starmap_index' if mode == 'starmap' else 'rift_selection_index'
                            items_attr = 'starmap_items' if mode == 'starmap' else 'rift_items'
                            setattr(self, index_attr, (getattr(self, index_attr) + 1) % len(getattr(self, items_attr)))
                            if mode == 'starmap':
                                self.speak_starmap_item()
                            else:
                                self.speak_rift_item()
                        elif mode not in ['starmap', 'rift'] and len(self.hud_items) > 1:
                            self.hud_index = (self.hud_index + 1) % len(self.hud_items)
                            self.speak_hud_item()
                    elif event.key == pygame.K_LEFT or event.key == pygame.K_RIGHT:
                        pass  # Future group cycle
                    if mode == 'upgrade' and event.key == pygame.K_RETURN:
                        self.apply_upgrade()
                    if mode == 'starmap' and event.key == pygame.K_RETURN:
                        self.lock_on_starmap_item()
                    if mode == 'rift' and event.key == pygame.K_RETURN:
                        self.lock_on_rift_item()
                    # First-letter navigation for starmap
                    if mode == 'starmap' and pygame.K_a <= event.key <= pygame.K_z:
                        char = chr(event.key).lower()
                        for idx, item in enumerate(self.starmap_items):
                            if item['label'].lower().startswith(char):
                                self.starmap_index = idx
                                self.speak_starmap_item()
                                break
            return

        # Detect modifier keys for volume adjustments
        shift_pressed = keys[pygame.K_LSHIFT] or keys[pygame.K_RSHIFT]
        ctrl_pressed = keys[pygame.K_LCTRL] or keys[pygame.K_RCTRL]
        alt_pressed = keys[pygame.K_LALT] or keys[pygame.K_RALT]
        # Process key down events
        for event in events:
            if event.type == pygame.KEYDOWN:
                # Number keys with modifiers: Frequency Preset System
                number_keys = {
                    pygame.K_1: 1, pygame.K_2: 2, pygame.K_3: 3,
                    pygame.K_4: 4, pygame.K_5: 5, pygame.K_6: 6,
                    pygame.K_7: 7, pygame.K_8: 8, pygame.K_9: 9
                }
                if event.key in number_keys:
                    slot = number_keys[event.key]
                    if ctrl_pressed:
                        # Ctrl+1-9: Save current frequencies to preset slot
                        # Check if slot already has a preset and needs confirmation
                        if slot in self.frequency_presets:
                            # Check if this is a confirmation (same slot pressed within 3 seconds)
                            if self.pending_preset_overwrite == slot and (self.simulation_time - self.pending_preset_time) < 3.0:
                                # Confirmed - overwrite the preset
                                self.frequency_presets[slot] = self.r_drive[:]
                                freqs_str = ", ".join([f"{f:.0f}" for f in self.r_drive])
                                self.speak(f"Preset {slot} overwritten. Frequencies: {freqs_str} hertz.")
                                self.pending_preset_overwrite = None
                            else:
                                # First press - warn and wait for confirmation
                                self.pending_preset_overwrite = slot
                                self.pending_preset_time = self.simulation_time
                                self.speak(f"Preset {slot} already exists. Press Control plus {slot} again to overwrite.")
                        else:
                            # Slot is empty - save directly
                            self.frequency_presets[slot] = self.r_drive[:]
                            freqs_str = ", ".join([f"{f:.0f}" for f in self.r_drive])
                            self.speak(f"Preset {slot} saved. Frequencies: {freqs_str} hertz.")
                            self.pending_preset_overwrite = None
                    elif shift_pressed:
                        # Shift+1-9: Recall preset (instantly set drive frequencies)
                        if slot in self.frequency_presets:
                            self.r_drive = self.frequency_presets[slot][:]
                            freqs_str = ", ".join([f"{f:.0f}" for f in self.r_drive])
                            self.speak(f"Preset {slot} recalled. Frequencies set to: {freqs_str} hertz.")
                        else:
                            self.speak(f"Preset {slot} is empty. Use Control plus {slot} to save current frequencies.")
                    else:
                        # No modifier: Dimension selection (1-5 only)
                        if slot <= 5:
                            dim_names = ["x", "y", "z", "higher dimension one", "higher dimension two"]
                            self.selected_dim = slot - 1
                            self.speak(f"Tuning {dim_names[slot - 1]} dimension.")
                            self.approaching_lock_announced = False
                # Toggle tuning mode
                elif event.key == pygame.K_j and not self.tuning_mode_toggled:
                    self.tuning_mode = not self.tuning_mode
                    mode_name = "Resonance tuning mode" if self.tuning_mode else "Manual mode"
                    self.speak(f"Toggled to {mode_name}.")
                    self.tuning_mode_toggled = True
                # Toggle verbosity
                elif event.key == pygame.K_v and not self.verbose_toggled:
                    self.verbose_mode = (self.verbose_mode + 1) % 3
                    modes = ["Low", "Medium", "High"]
                    self.speak(f"Verbosity mode: {modes[self.verbose_mode]}.")
                    self.verbose_toggled = True
                # Cycle Tuaoi Crystal mode (G key for Golden Crystal)
                elif event.key == pygame.K_g and self.simulation_time - self.last_tuaoi_switch > TUAOI_MODE_SWITCH_COOLDOWN:
                    mode_names = list(TUAOI_MODES.keys())
                    self.tuaoi_mode_index = (self.tuaoi_mode_index + 1) % len(mode_names)
                    self.tuaoi_mode = mode_names[self.tuaoi_mode_index]
                    mode_info = TUAOI_MODES[self.tuaoi_mode]
                    self.speak(f"Tuaoi Crystal: {self.tuaoi_mode.capitalize()} mode. {mode_info['desc']}")
                    self.last_tuaoi_switch = self.simulation_time
                # Toggle starmap
                elif event.key == pygame.K_m:
                    self.starmap_mode = not self.starmap_mode
                    if self.starmap_mode:
                        self.update_starmap_items(stars, planets, nebulae)
                        self.starmap_index = 0
                        self.speak_starmap_item()  # First item provides context
                    else:
                        self.speak("Exiting starmap.")
                # Toggle high contrast
                elif event.key == pygame.K_c and not self.contrast_toggled:
                    self.high_contrast = not self.high_contrast
                    self.speak(f"High contrast mode: {'on' if self.high_contrast else 'off'}.")
                    self.contrast_toggled = True
                # Quick query target frequency
                elif event.key == pygame.K_q:
                    quick = f"Target in selected dim: {self.f_target[self.selected_dim]:.2f} Hz."
                    self.speak(quick)
                # Initiate landing
                elif event.key == pygame.K_l and not self.landed_mode:
                    avg_res = np.mean(self.resonance_levels)
                    # Apply exoplanet difficulty to landing threshold
                    landing_threshold = LANDING_THRESHOLD
                    if self.nearest_body and self.nearest_body['type'] == 'planet':
                        difficulty = self.nearest_body.get('difficulty', 1.0)
                        landing_threshold *= difficulty  # Harder planets need higher resonance

                    if self.near_object and avg_res > landing_threshold and self.nearest_body and self.nearest_body['type'] == 'planet':
                        self.landing_timer = LANDING_TIME
                        exoplanet_type = self.nearest_body.get('exoplanet_type', 'super_earth')
                        exoplanet_desc = EXOPLANET_TYPES[exoplanet_type]['desc']
                        self.speak(f"Initiating anchoring sequence on {exoplanet_desc}.")
                    else:
                        self.resonance_integrity -= 0.01
                        if not self.near_object:
                            self.speak("No celestial body nearby for anchoring. Minor integrity loss.")
                        elif avg_res <= landing_threshold:
                            self.speak("Harmonic alignment too low for anchoring. Minor integrity loss.")
                        else:
                            self.speak("Cannot anchor on this object. Minor integrity loss.")
                # Takeoff from planet (Ascension)
                elif event.key == pygame.K_t and self.landed_mode:
                    self.landed_mode = False
                    self.landed_planet = None
                    self.landed_planet_body = None
                    if self.biome_sound:
                        if self.biome_sound  in self.audio_system.active_sound_effects:
                            self.audio_system.active_sound_effects.remove(self.biome_sound)
                        self.biome_sound = None
                    self.speak("Ascending from planet. Light vehicle disengaged.")
                # Read full status
                elif event.key == pygame.K_r:
                    status = f"Position: {self.position.round(2)}. Velocity: {self.velocity.round(2)}. Resonance levels: {self.resonance_levels.round(2)}. View rotation: {self.view_rotation:.2f} radians. {'Landed on planet.' if self.landed_mode else 'In space.'} Integrity: {self.resonance_integrity:.2f}. Crystals: {self.crystals_collected}. Power levels: {self.resonance_power.round(2)}."
                    self.speak(status)
                # Toggle HUD or upgrade menu
                elif event.key == pygame.K_u:
                    if self.landed_mode and len(self.locked_crystals) == self.crystal_count:
                        self.upgrade_mode = True
                        self.hud_index = 0
                        self.update_hud_items(upgrade=True)
                        self.speak(f"Attunement menu. {self.crystals_collected} crystals available.")
                        self.speak_hud_item()
                    else:
                        self.hud_mode = True
                        self.hud_index = 0
                        self.update_hud_items()
                        self.speak_hud_item()  # First item announces the menu context
                # Text size adjustment flag
                elif event.key == pygame.K_t:
                    self.text_size_adjusted = True
                # Increase text size
                elif event.key == pygame.K_EQUALS and self.text_size_adjusted:
                    self.hud_text_size += 2
                    self.hud_text_size = max(12, min(48, self.hud_text_size))
                    font = pygame.font.SysFont(None, self.hud_text_size)
                    self.speak(f"Text size increased to {self.hud_text_size}.")
                # Decrease text size
                elif event.key == pygame.K_MINUS and self.text_size_adjusted:
                    self.hud_text_size -= 2
                    self.hud_text_size = max(12, min(48, self.hud_text_size))
                    font = pygame.font.SysFont(None, self.hud_text_size)
                    self.speak(f"Text size decreased to {self.hud_text_size}.")
                # Open instructions (README.md)
                elif event.key == pygame.K_F1 and not self.instructions_opened:
                    os.startfile('README.md')
                    self.speak("Documentation opened.")
                    self.instructions_opened = True
                # Rift interaction: Charge/entry or toggle selection
                elif event.key == pygame.K_e and not self.landed_mode:
                    if self.locked_is_rift and self.locked_target is not None:
                        dist = np.linalg.norm(self.position - self.locked_target)
                        avg_res = np.mean(self.resonance_levels)
                        if dist < RIFT_ALIGNMENT_TOLERANCE and avg_res > RIFT_ENTRY_RES_THRESHOLD:
                            # New: Skip charge if perfect
                            if self.locked_rift:
                                self.enter_rift(self.locked_rift)
                        elif dist < RIFT_ALIGNMENT_TOLERANCE and avg_res > RIFT_ENTRY_RES_THRESHOLD / 2:
                            self.rift_charge_timer = RIFT_CHARGE_TIME  # Start charge sequence
                            self.speak("Initiating rift charge sequence.")
                        else:
                            self.speak("Approach closer or increase resonance to charge.")
                    else:
                        if len(self.rifts) > 0:
                            self.rift_selection_mode = True
                            self.update_rift_items()
                            self.rift_selection_index = 0
                            self.speak_rift_item()  # First item provides context
                        else:
                            self.speak("No Harmonic Chambers detected.")
                # Toggle speed mode in manual mode
                elif event.key == pygame.K_z and not self.tuning_mode and not self.speed_mode_toggled:
                    self.speed_mode = (self.speed_mode + 1) % len(SPEED_FACTORS)
                    self.speak(f"Speed mode toggled to {SPEED_MODE_NAMES[self.speed_mode]}.")
                    self.speed_mode_toggled = True
                # New: Toggle sing mode
                elif event.key == pygame.K_h and not self.sing_toggled:
                    self.sing_mode = not self.sing_mode
                    self.sing_active = self.sing_mode
                    self.speak(f"Sing mode {'activated' if self.sing_mode else 'deactivated'}.")
                    if self.sing_mode:
                        if self.pitch_thread is None or not self.pitch_thread.is_alive():
                            self.pitch_thread = threading.Thread(target=self.continuous_pitch_detection, daemon=True)
                            self.pitch_thread.start()
                    self.sing_toggled = True
                # New: Save/load
                elif event.key == pygame.K_s and ctrl_pressed:
                    self.save_game()
                elif event.key == pygame.K_l and ctrl_pressed:
                    self.load_game()
                # New: Toggle autosave
                elif event.key == pygame.K_a and ctrl_pressed:
                    self.autosave_enabled = not self.autosave_enabled
                    self.speak(f"Autosave {'enabled' if self.autosave_enabled else 'disabled'}.")

                # ===== NEW ATLANTEAN KEY HANDLERS =====
                # Portal Anchor: P to create, Shift+P to use
                elif event.key == pygame.K_p and not shift_pressed and not self.landed_mode:
                    self.create_portal_anchor()
                elif event.key == pygame.K_p and shift_pressed and not self.landed_mode:
                    self.use_portal_anchor()

                # Astral Projection: B to enter/exit
                elif event.key == pygame.K_b and not self.landed_mode:
                    if self.astral_mode:
                        self.exit_astral_mode()
                    else:
                        self.enter_astral_mode()

                # Intention-Based Navigation: I to activate (hold)
                elif event.key == pygame.K_i and not self.landed_mode:
                    if not self.intention_active:
                        self.start_intention_navigation()

                # Landed-mode specific inputs
                if self.landed_mode:
                    if event.key == pygame.K_f:
                        self.scan_nearest_crystal()
                        self.approaching_lock_announced = False  # Reset on scan
                    if event.key == pygame.K_x:
                        self.collect_crystal()
                    moved = False
                    if event.key == pygame.K_w:
                        self.cursor_pos[1] += 1
                        moved = True
                    if event.key == pygame.K_s:
                        self.cursor_pos[1] -= 1
                        moved = True
                    if event.key == pygame.K_a:
                        self.cursor_pos[0] -= 1
                        moved = True
                    if event.key == pygame.K_d:
                        self.cursor_pos[0] += 1
                        moved = True
                    if moved:
                        self.cursor_pos = np.clip(self.cursor_pos, -GRID_SIZE, GRID_SIZE)
                        if self.simulation_time - self.last_cursor_speak_time > CURSOR_SPEECH_COOLDOWN:
                            self.speak(f"Cursor at {self.cursor_pos.round(2)}.")
                            self.last_cursor_speak_time = self.simulation_time

                # Volume controls
                if event.key == pygame.K_EQUALS:
                    if alt_pressed:
                        self.audio_system.drive_volume = min(1.0, self.audio_system.drive_volume + 0.01)
                        self.speak(f"Drive volume at {int(self.audio_system.drive_volume * 100)} percent.")
                    elif shift_pressed:
                        self.audio_system.beep_volume = min(1.0, self.audio_system.beep_volume + 0.01)
                        self.speak(f"Beep volume at {int(self.audio_system.beep_volume * 100)} percent.")
                    elif ctrl_pressed:
                        self.audio_system.effect_volume = min(1.0, self.audio_system.effect_volume + 0.01)
                        self.speak(f"Effect volume at {int(self.audio_system.effect_volume * 100)} percent.")
                    else:
                        self.audio_system.master_volume = min(1.0, self.audio_system.master_volume + 0.01)
                        self.speak(f"Master volume at {int(self.audio_system.master_volume * 100)} percent.")
                if event.key == pygame.K_MINUS:
                    if alt_pressed:
                        self.audio_system.drive_volume = max(0.0, self.audio_system.drive_volume - 0.01)
                        self.speak(f"Drive volume at {int(self.audio_system.drive_volume * 100)} percent.")
                    elif shift_pressed:
                        self.audio_system.beep_volume = max(0.0, self.audio_system.beep_volume - 0.01)
                        self.speak(f"Beep volume at {int(self.audio_system.beep_volume * 100)} percent.")
                    elif ctrl_pressed:
                        self.audio_system.effect_volume = max(0.0, self.audio_system.effect_volume - 0.01)
                        self.speak(f"Effect volume at {int(self.audio_system.effect_volume * 100)} percent.")
                    else:
                        self.audio_system.master_volume = max(0.0, self.audio_system.master_volume - 0.01)
                        self.speak(f"Master volume at {int(self.audio_system.master_volume * 100)} percent.")

                # New: Water blessing mode - start timer on spacebar press
                if event.key == pygame.K_SPACE:
                    self.spacebar_pressed = True
                    self.spacebar_hold_timer = 0.0

            # Process key up events for debounce
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_j: self.tuning_mode_toggled = False
                if event.key == pygame.K_v: self.verbose_toggled = False
                if event.key == pygame.K_c: self.contrast_toggled = False
                if event.key == pygame.K_t: self.text_size_adjusted = False
                if event.key == pygame.K_F1: self.instructions_opened = False
                if event.key == pygame.K_z: self.speed_mode_toggled = False
                if event.key == pygame.K_h: self.sing_toggled = False
                # New: Water blessing - reset on release
                if event.key == pygame.K_SPACE:
                    self.spacebar_pressed = False
                    self.spacebar_hold_timer = 0.0

        # New: Update water blessing timer if held
        if self.spacebar_pressed:
            self.spacebar_hold_timer += DT
            if self.spacebar_hold_timer >= WATER_BLESSING_HOLD_TIME and all(r > WATER_BLESSING_RES_THRESHOLD for r in self.resonance_levels):
                self.generate_gift_wav()
                self.spacebar_pressed = False  # Prevent repeat
                self.spacebar_hold_timer = 0.0

        # Determine tuning rate, adjusted for landed mode and proximity to crystals
        rate = TUNING_RATE
        if self.landed_mode:
            rate = TUNING_RATE_PLANET
            # Dynamic tuning rate when landed
            if self.crystal_positions:
                dists = [np.linalg.norm(self.cursor_pos - pos) if idx not in self.locked_crystals else float('inf') for idx, pos in enumerate(self.crystal_positions)]
                nearest = np.argmin(dists)
                if dists[nearest] != float('inf'):
                    crystal_data = self.crystal_freqs[nearest]
                    crystal_freq_list = crystal_data['freqs'] if isinstance(crystal_data, dict) else crystal_data
                    delta = abs(self.r_drive[self.selected_dim] - crystal_freq_list[self.selected_dim])
                    rate = TUNING_RATE_PLANET * (delta / 50.0 + 0.1)
                    rate = max(1.0, min(TUNING_RATE_PLANET, rate))
                    if delta < APPROACHING_LOCK_THRESHOLD:
                        if not self.approaching_lock_announced:
                            self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.approaching_beep_waveform, pan=0.0, volume=self.audio_system.beep_volume))
                            self.approaching_lock_announced = True
                        if self.simulation_time - self.last_approaching_beep_time > 1.0:  # Play mid beeps every second while approaching
                            self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.approaching_beep_waveform, pan=0.0, volume=self.audio_system.beep_volume))
                            self.last_approaching_beep_time = self.simulation_time
                    elif delta > 15.0:
                        self.approaching_lock_announced = False
                    else:
                        pass  # Keep flag if still close

        # Conditional tuning based on mode
        allow_tuning = self.tuning_mode or self.selected_dim >= 3  # Allow if resonance mode or higher dim
        if allow_tuning:
            if keys[pygame.K_UP]:
                self.r_drive[self.selected_dim] += rate * DT
                self.r_drive[self.selected_dim] = min(self.r_drive[self.selected_dim], FREQUENCY_RANGE[1])
            if keys[pygame.K_DOWN]:
                self.r_drive[self.selected_dim] -= rate * DT
                self.r_drive[self.selected_dim] = max(self.r_drive[self.selected_dim], FREQUENCY_RANGE[0])
        else:
            if keys[pygame.K_UP] or keys[pygame.K_DOWN]:
                self.speak("Spatial dimension tuning locked in manual mode. Toggle with J for full access.")

        # Disable rotation on planet
        if self.landed_mode:
            self.rotating_left = False
            self.rotating_right = False
            return

        # Handle view rotation with arrows
        self.rotating_left = keys[pygame.K_LEFT]
        self.rotating_right = keys[pygame.K_RIGHT]
        if self.rotating_left:
            self.view_rotation -= ROTATION_SPEED * DT
        if self.rotating_right:
            self.view_rotation += ROTATION_SPEED * DT

        # Wrap rotation angle to [0, 2π] to prevent overflow
        self.view_rotation %= (2 * np.pi)

        # Play rotation sound repeatedly while rotating
        if (self.rotating_left or self.rotating_right) and self.simulation_time - self.last_rotation_sound_time > ROTATION_SOUND_DURATION:
            pan = -1.0 if self.rotating_left else 1.0
            self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.rotation_waveform, pan=pan, volume=self.audio_system.effect_volume))
            self.last_rotation_sound_time = self.simulation_time

        # Manual navigation in manual mode
        if not self.tuning_mode:
            # Direct manual navigation using r_drive offsets for spatial dims
            desired_vel = np.zeros(3)  # Only spatial dims: x(0), y(1), z(2)
            thrust = self.max_velocity * SPEED_FACTORS[self.speed_mode]  # Adjust thrust based on speed mode
            if keys[pygame.K_w]:
                desired_vel[1] += thrust  # Forward +y
            if keys[pygame.K_s]:
                desired_vel[1] -= thrust  # Backward -y
            if keys[pygame.K_a]:
                desired_vel[0] -= thrust  # Left -x
            if keys[pygame.K_d]:
                desired_vel[0] += thrust  # Right +x
            if keys[pygame.K_PAGEDOWN]:
                desired_vel[2] += thrust  # Ascent +z
            if keys[pygame.K_PAGEUP]:
                desired_vel[2] -= thrust  # Descent -z

            # Apply offsets to r_drive for each spatial dim
            for i in range(3):  # Dims 0,1,2
                if desired_vel[i] != 0:
                    target_res = min(0.999, abs(desired_vel[i]) / self.max_velocity)  # Approach 1 but avoid exact 1 (vel=0 issue)
                    if target_res > 0:
                        d_over_w = np.sqrt(1 / target_res - 1)
                        delta = self.resonance_width * d_over_w
                        delta_f = np.sign(desired_vel[i]) * delta
                        self.r_drive[i] = self.f_target[i] + delta_f
                else:
                    self.r_drive[i] = self.f_target[i]  # Reset to stop

    # New: Generate gift.wav
    def generate_gift_wav(self):
        t = np.linspace(0, WATER_BLESSING_DURATION, int(WATER_BLESSING_DURATION * SAMPLE_RATE), endpoint=False)
        signal = np.sin(2 * np.pi * WATER_BLESSING_FREQ * t)
        signal = np.int16(signal * 32767)  # 16-bit PCM
        with wave.open(os.path.expanduser("~/Desktop/gift.wav"), 'wb') as wav_file:
            wav_file.setnchannels(1)  # Mono
            wav_file.setsampwidth(2)  # 16-bit
            wav_file.setframerate(SAMPLE_RATE)
            wav_file.writeframes(signal.tobytes())

    # Update HUD items list
    def update_hud_items(self, upgrade=False):
        # Populate HUD items based on upgrade mode or standard status
        if upgrade:
            self.hud_items = [f"{u['name']}: {u['desc']} Cost: {u['cost']}" for u in self.upgrades]
        else:
            self.hud_items = [
                f"Selected Realm: {self.selected_dim + 1}",
                f"Drive Freq: {self.r_drive[self.selected_dim]:.2f} Hz",
                f"Target Freq: {self.f_target[self.selected_dim]:.2f} Hz",
                f"Harmonic Alignment: {self.resonance_levels[self.selected_dim]:.2f}",
                f"Speed: {np.linalg.norm(self.velocity):.2f} u/s",
                f"Vol: {int(self.audio_system.master_volume * 100)}%",
                f"Integrity: {self.resonance_integrity:.2f}",
                f"Atlantean Crystals: {self.crystals_collected}",
                f"Status: {'Anchored' if self.landed_mode else 'In Flight'}",
                f"Power: {np.mean(self.resonance_power):.2f}",
                f"Tuaoi Mode: {self.tuaoi_mode.capitalize()}",
                f"Merkaba: {'Active' if self.merkaba_active else 'Inactive'}",
                f"Temple Resonance: {'Active' if self.in_temple_resonance else 'Inactive'}",
                f"Tuning Mode: {'Resonance (all realms)' if self.tuning_mode else 'Manual (higher realms only)'}",
                f"Speed Mode: {SPEED_MODE_NAMES[self.speed_mode]}" if not self.tuning_mode else ""
            ]
            if self.landed_mode:
                self.hud_items += [f"Cursor Pos: {self.cursor_pos.round(2)}", f"Crystals Left: {self.crystal_count - len(self.locked_crystals)}", f"Sing Mode: {'On' if self.sing_mode else 'Off'}"]

    # Speak current HUD item
    def speak_hud_item(self):
        # Speak the selected HUD item, with optional verbose detail
        item = self.hud_items[self.hud_index]
        self.speak(item)
        if self.verbose_mode > 1:
            self.speak("High verbosity detail: Explore the golden spiral for harmony.")

    # Update starmap items list (now includes rifts)
    def update_starmap_items(self, stars, planets, nebulae):
        # Populate starmap with nearby bodies and rifts, sorted by distance
        self.starmap_items = []
        if self.locked_target is not None and not self.locked_is_rift:
            self.starmap_items.append({'label': "Unlock target", 'pos': None, 'type': None, 'rift': None})
        # Collect items with distances
        items = []
        # Add stars
        for i, body in enumerate(stars):
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi
                stellar_type = body.get('stellar_type', 'main_sequence')
                stellar_desc = STELLAR_TYPES[stellar_type]['desc']
                label = f"Star {i+1} ({stellar_desc}) at dist {dist:.1f}, angle {angle:.1f} degrees (unlandable)"
                items.append((dist, label, body['pos'], 'star', None))
        # Add planets
        for i, body in enumerate(planets):
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi
                exoplanet_type = body.get('exoplanet_type', 'super_earth')
                exoplanet_desc = EXOPLANET_TYPES[exoplanet_type]['desc']
                label = f"Planet {i+1} ({exoplanet_desc}) at dist {dist:.1f}, angle {angle:.1f} degrees"
                items.append((dist, label, body['pos'], 'planet', None))
        # Add nebulae
        for i, body in enumerate(nebulae):
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi
                nebula_type = body.get('nebula_type', 'emission')
                nebula_desc = NEBULA_TYPES[nebula_type]['desc']
                label = f"Nebula {i+1} ({nebula_desc}) at dist {dist:.1f}, angle {angle:.1f} degrees (unlandable)"
                items.append((dist, label, body['pos'], 'nebula', None))
        # Add rifts
        for i, rift in enumerate(self.rifts):
            dist = np.linalg.norm(self.position - rift['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(rift['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi
                label = f"Rift {i+1} ({rift['type']}) at dist {dist:.1f}, angle {angle:.1f} degrees"
                items.append((dist, label, rift['pos'], 'rift', rift))
        # Sort by distance
        items.sort(key=lambda x: x[0])
        for dist, label, pos, body_type, rift in items:
            self.starmap_items.append({'label': label, 'pos': pos, 'type': body_type, 'rift': rift})
        if not self.starmap_items:
            self.starmap_items.append({'label': "No objects in scanner range.", 'pos': None, 'type': None, 'rift': None})

    # Speak current starmap item
    def speak_starmap_item(self):
        # Speak the selected starmap item
        item = self.starmap_items[self.starmap_index]['label']
        self.speak(item)

    # Lock on to starmap item
    def lock_on_starmap_item(self):
        # Lock or unlock target from starmap selection
        selected = self.starmap_items[self.starmap_index]
        if selected['label'] == "Unlock target":
            self.locked_target = None
            self.locked_is_rift = False
            self.approached_rift_announced = False
            if self.lock_sound:
                if self.lock_sound  in self.audio_system.active_sound_effects:
                    self.audio_system.active_sound_effects.remove(self.lock_sound)
                self.lock_sound = None
            self.speak("Target unlocked.")
            return
        if selected['pos'] is None:
            return
        self.locked_target = selected['pos']
        self.locked_is_rift = (selected['type'] == 'rift')
        self.locked_rift = selected['rift'] if self.locked_is_rift else None
        waveform = self.audio_system.rift_beep_waveform if self.locked_is_rift else self.audio_system.beep_waveform
        self.lock_sound = SoundEffect(waveform, loop=True, volume=self.audio_system.beep_volume)
        self.audio_system.active_sound_effects.append(self.lock_sound)
        self.approached_rift_announced = False
        self.speak(f"Locked on to {selected['label'].split(' at')[0]}.")

    # Update rift items list
    def update_rift_items(self):
        # Populate rift selection menu, sorted by distance
        self.rift_items = []
        if self.locked_rift is not None:
            self.rift_items.append({'label': "Unlock rift", 'pos': None, 'type': None, 'rift': None})
        # Collect items with distances
        items = []
        for i, rift in enumerate(self.rifts):
            dist = np.linalg.norm(self.position - rift['pos'])
            projected_pos = project_to_2d(rift['pos'] - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi
            label = f"Rift {i+1} ({rift['type']}) at dist {dist:.1f}, angle {angle:.1f} degrees"
            items.append((dist, label, rift['pos'], rift['type'], rift))
        # Sort by distance
        items.sort(key=lambda x: x[0])
        for dist, label, pos, rift_type, rift in items:
            self.rift_items.append({'label': label, 'pos': pos, 'type': rift_type, 'rift': rift})
        if not self.rift_items:
            self.rift_items.append({'label': "No rifts detected.", 'pos': None, 'type': None, 'rift': None})

    # Speak current rift item
    def speak_rift_item(self):
        # Speak the selected rift item
        item = self.rift_items[self.rift_selection_index]['label']
        self.speak(item)

    # Lock on to rift item
    def lock_on_rift_item(self):
        # Lock or unlock rift from selection
        selected = self.rift_items[self.rift_selection_index]
        if selected['label'] == "Unlock rift":
            self.locked_rift = None
            self.locked_target = None
            self.locked_is_rift = False
            self.approached_rift_announced = False
            if self.lock_sound:
                if self.lock_sound  in self.audio_system.active_sound_effects:
                    self.audio_system.active_sound_effects.remove(self.lock_sound)
                self.lock_sound = None
            self.speak("Rift unlocked.")
            return
        if selected['pos'] is None:
            return
        self.locked_rift = selected['rift']
        self.locked_target = self.locked_rift['pos']
        self.locked_is_rift = True
        self.lock_sound = SoundEffect(self.audio_system.rift_beep_waveform, loop=True, volume=self.audio_system.beep_volume)
        self.audio_system.active_sound_effects.append(self.lock_sound)
        self.approached_rift_announced = False
        self.speak(f"Locked on to {selected['label'].split(' at')[0]} for beeping and navigation.")

    # Scan nearest crystal on planet
    def scan_nearest_crystal(self):
        # Find and announce nearest crystal, with auto-snap if close
        if not self.crystal_positions:
            return
        dists = [np.linalg.norm(self.cursor_pos - pos) if idx not in self.locked_crystals else float('inf') for idx, pos in enumerate(self.crystal_positions)]
        nearest = np.argmin(dists)
        if dists[nearest] == float('inf'):
            self.speak("No more crystals to scan on this planet.")
            return

        # Get crystal data (now a dict with 'freqs' and optional 'atlantean_type')
        crystal_data = self.crystal_freqs[nearest]
        if isinstance(crystal_data, dict):
            crystal_freqs = crystal_data['freqs']
            is_special = crystal_data.get('special', False)
            atlantean_type = crystal_data.get('atlantean_type')
        else:
            # Legacy format support
            crystal_freqs = crystal_data
            is_special = False
            atlantean_type = None

        # Compute resonance against crystal
        temp_res = np.zeros(N_DIMENSIONS)
        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - crystal_freqs[i]
            temp_res[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
        if np.mean(temp_res) > AUTO_SNAP_THRESHOLD:
            for i in range(N_DIMENSIONS):
                self.r_drive[i] = crystal_freqs[i]
            self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.lock_beep_waveform, pan=0.0, volume=self.audio_system.beep_volume))
        freq = crystal_freqs[self.selected_dim]
        dx, dy = self.crystal_positions[nearest] - self.cursor_pos
        direction = ""
        if dy > 0: direction += "north "
        elif dy < 0: direction += "south "
        if dx > 0: direction += "east"
        elif dx < 0: direction += "west"

        crystal_type_msg = ""
        if is_special and atlantean_type:
            crystal_type_msg = f" Special {atlantean_type.replace('_', ' ').title()} crystal!"
        self.speak(f"Nearest crystal {dists[nearest]:.1f} units {direction}. Target freq in dim {self.selected_dim+1}: {freq:.2f} Hz.{crystal_type_msg}")
        angle = np.arctan2(dy, dx)
        pan = np.cos(angle)
        self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.beep_waveform, pan=pan, volume=self.audio_system.beep_volume))

    # Collect crystal on planet
    def collect_crystal(self):
        # Check resonance and collect if sufficient
        dists = [np.linalg.norm(self.cursor_pos - pos) for pos in self.crystal_positions]
        nearest = np.argmin(dists)
        if dists[nearest] > 1 or nearest in self.locked_crystals:
            self.speak("No collectable crystal nearby.")
            return
        # Get crystal data (now a dict with 'freqs' and optional 'atlantean_type')
        crystal_data = self.crystal_freqs[nearest]
        if isinstance(crystal_data, dict):
            crystal_freqs = crystal_data['freqs']
            is_special = crystal_data.get('special', False)
            atlantean_type = crystal_data.get('atlantean_type')
        else:
            # Legacy format support
            crystal_freqs = crystal_data
            is_special = False
            atlantean_type = None

        # Use crystal freq as target for resonance check
        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - crystal_freqs[i]
            self.resonance_levels[i] = 1 / (1 + (delta_f / self.resonance_width)**2)

        if np.mean(self.resonance_levels) > CRYSTAL_COLLECTION_THRESHOLD:
            self.locked_crystals.add(nearest)

            # Track pattern progress for sacred geometry bonus
            self.pattern_progress.append(nearest)

            # Calculate crystal value multiplier
            crystal_value = 1
            if is_special and atlantean_type and atlantean_type in ATLANTEAN_CRYSTAL_TYPES:
                crystal_info = ATLANTEAN_CRYSTAL_TYPES[atlantean_type]
                crystal_value = int(crystal_info['mult'])
                self.speak(f"Ancient {atlantean_type.replace('_', ' ').title()} crystal collected! {crystal_info['desc']}. Value: {crystal_value} crystals.")
                # Apply special crystal effect
                self.apply_atlantean_crystal_effect(atlantean_type)
            else:
                # Regular crystal type based on frequency
                crystal_freq = np.mean(crystal_freqs)
                crystal_type, crystal_info = self.get_crystal_type(crystal_freq)
                self.speak(f"Atlantean {crystal_type.capitalize()} crystal collected. {crystal_info['chakra'].capitalize()} chakra resonance. Harmony increases.")

            self.crystals_collected += crystal_value
            self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.lock_beep_waveform, pan=0.0, volume=self.audio_system.beep_volume))

            if random.random() < 0.2:
                self.speak("Ancient echo: The spiral binds all realms in golden eternity.")

            # Check for sacred geometry pattern completion
            if len(self.locked_crystals) == self.crystal_count:
                if self.current_pattern:
                    pattern_info = SACRED_PATTERNS.get(self.current_pattern, {})
                    bonus = pattern_info.get('bonus', 'unknown')
                    mult = pattern_info.get('mult', 1.0)
                    self.pattern_bonus_timer = 30.0  # 30 second pattern bonus
                    # Apply pattern bonus crystals
                    bonus_crystals = int(self.crystal_count * (mult - 1))
                    if bonus_crystals > 0:
                        self.crystals_collected += bonus_crystals
                    self.speak(f"All crystals collected! Sacred {self.current_pattern.replace('_', ' ').title()} pattern completed. {bonus.replace('_', ' ').title()} bonus activated. {bonus_crystals} bonus crystals. Press U for attunement.")
                else:
                    self.speak("All crystals collected. Press U for attunement menu.")

            if self.crystals_collected >= ASCENSION_CRYSTAL_THRESHOLD:
                self.ascend()
            self.approaching_lock_announced = False  # Reset after collection
        else:
            self.speak("Resonance too low to collect. Tune to crystal frequencies.")

    def apply_atlantean_crystal_effect(self, crystal_type):
        """Apply the special effect of an Atlantean crystal type."""
        if crystal_type not in ATLANTEAN_CRYSTAL_TYPES:
            return
        effect = ATLANTEAN_CRYSTAL_TYPES[crystal_type]['effect']

        if effect == 'velocity_burst':
            # Temporary speed boost
            self.max_velocity *= 1.5
            self.speak("Fire crystal energy surges through your light vehicle!")
        elif effect == 'shield_boost':
            # Integrity boost
            self.resonance_integrity = min(1.0, self.resonance_integrity + 0.2)
            self.speak("Aquamarine protective field strengthens your hull.")
        elif effect == 'communication':
            # Enhanced scan range (temporary)
            self.speak("Larimar stone awakens ancient wisdom within you.")
        elif effect == 'transformation':
            # Consciousness boost
            self.consciousness_value = min(1.0, self.consciousness_value + 0.1)
            self.speak("Moldavite accelerates your spiritual evolution!")
        elif effect == 'memory_unlock':
            # Reveal nearby crystals/rifts
            self.speak("Lemurian seed crystal shares memories of forgotten ages.")
        elif effect == 'purification':
            # Remove negative effects
            self.dissonance_timer = 0.0
            self.speak("Black tourmaline cleanses all dissonance from your field.")
        elif effect == 'angelic_connection':
            # Enhanced rift detection
            self.speak("Celestite opens channels to higher dimensional beings.")

    # ===== PORTAL ANCHOR SYSTEM =====
    def create_portal_anchor(self):
        """Create a portal anchor at current position using crystals."""
        if len(self.portal_anchors) >= MAX_PORTAL_ANCHORS:
            self.speak(f"Maximum {MAX_PORTAL_ANCHORS} portal anchors reached. Use an existing anchor first.")
            return
        if self.crystals_collected < PORTAL_ANCHOR_COST:
            self.speak(f"Insufficient crystals. Need {PORTAL_ANCHOR_COST} to create portal anchor.")
            return

        self.crystals_collected -= PORTAL_ANCHOR_COST
        anchor_name = f"Anchor {len(self.portal_anchors) + 1}"
        anchor = {
            'pos': self.position.copy(),
            'name': anchor_name,
            'freq': np.mean(self.r_drive)  # Store current frequency as signature
        }
        self.portal_anchors.append(anchor)
        self.speak(f"Portal anchor '{anchor_name}' created. {len(self.portal_anchors)}/{MAX_PORTAL_ANCHORS} anchors set.")

    def use_portal_anchor(self):
        """Teleport to a saved portal anchor."""
        if not self.portal_anchors:
            self.speak("No portal anchors set. Create one with P key.")
            return
        if self.simulation_time - self.last_portal_use < PORTAL_COOLDOWN:
            remaining = int(PORTAL_COOLDOWN - (self.simulation_time - self.last_portal_use))
            self.speak(f"Portal cooldown active. {remaining} seconds remaining.")
            return
        if np.mean(self.resonance_levels) < PORTAL_TRAVEL_RESONANCE:
            self.speak("Insufficient resonance for portal travel. Tune frequencies higher.")
            return

        # For now, teleport to first anchor (could be enhanced with selection menu)
        anchor = self.portal_anchors[0]
        self.position = anchor['pos'].copy()
        self.last_portal_use = self.simulation_time
        self.speak(f"Portal activated. Teleported to {anchor['name']}.")

    # ===== ASTRAL PROJECTION MODE =====
    def enter_astral_mode(self):
        """Enter astral projection mode for out-of-body exploration."""
        if np.mean(self.resonance_levels) < ASTRAL_PROJECTION_RESONANCE:
            self.speak("Insufficient resonance for astral projection. Achieve 90% resonance in all realms.")
            return
        if self.simulation_time - self.last_astral_return < ASTRAL_COOLDOWN:
            remaining = int(ASTRAL_COOLDOWN - (self.simulation_time - self.last_astral_return))
            self.speak(f"Astral cooldown active. {remaining} seconds remaining.")
            return

        self.astral_mode = True
        self.astral_body_pos = self.position.copy()
        self.astral_timer = ASTRAL_DURATION
        self.speak("Astral projection initiated. Your consciousness expands beyond your light vehicle. Press B to return.")

    def exit_astral_mode(self):
        """Return from astral projection to physical form."""
        if not self.astral_mode:
            return
        self.position = self.astral_body_pos.copy()
        self.astral_mode = False
        self.astral_body_pos = None
        self.last_astral_return = self.simulation_time
        self.speak("Returning to physical form. Astral projection complete.")

    # ===== INTENTION-BASED NAVIGATION =====
    def start_intention_navigation(self):
        """Begin intention-based navigation by focusing on a target."""
        if np.mean(self.resonance_levels) < INTENTION_RESONANCE_THRESHOLD:
            self.speak("Insufficient resonance for intention navigation. Focus your mind and tune higher.")
            return
        self.intention_active = True
        self.intention_timer = 0.0
        self.intention_target = None
        self.speak("Intention navigation activated. Focus your intention on your destination...")

    def update_intention_navigation(self, dt):
        """Update intention navigation state."""
        if not self.intention_active:
            return

        self.intention_timer += dt

        if self.intention_timer >= INTENTION_ACTIVATION_TIME:
            # Intention navigation complete - find nearest target
            if self.locked_target is not None:
                # Teleport toward locked target
                direction = self.locked_target - self.position
                distance = np.linalg.norm(direction)
                travel_distance = min(distance * INTENTION_PRECISION, INTENTION_RANGE)
                if distance > 0:
                    self.position += (direction / distance) * travel_distance
                self.speak(f"Intention manifested. Traveled {travel_distance:.1f} units toward target.")
            else:
                self.speak("No target locked. Intention dissipates without focus.")
            self.intention_active = False
            self.intention_timer = 0.0

    # ===== TEMPLE AND LEY LINE DETECTION =====
    def check_temple_proximity(self, temples):
        """Check for nearby temples and collect keys."""
        if self.simulation_time - self.last_temple_check < 1.0:
            return

        self.near_temple = None
        scan_range = self.get_effective_scan_range()
        for temple in temples:
            dist = np.linalg.norm(self.position - temple['pos'])
            if dist < scan_range:
                self.near_temple = temple
                key_index = temple['key_index']

                if key_index >= 0 and key_index not in self.temple_keys:
                    # Check resonance at temple frequency
                    temple_freq = temple['freq']
                    res_at_freq = 0
                    for i in range(N_DIMENSIONS):
                        delta = abs(self.r_drive[i] - temple_freq)
                        res_at_freq += 1 / (1 + (delta / self.resonance_width)**2)
                    res_at_freq /= N_DIMENSIONS

                    if res_at_freq > 0.7:  # Need 70% resonance to collect key
                        self.temple_keys.add(key_index)
                        self.temple_nearby_announced = False  # Reset for future visits
                        if len(self.temple_keys) == MINOR_TEMPLE_COUNT:
                            self.speak(f"{temple['key_name']} key acquired! All twelve temple keys collected. The Halls of Amenti now await your arrival.")
                        else:
                            self.speak(f"Temple of {temple['key_name']} visited. {temple['key_name']} key acquired! {len(self.temple_keys)}/{MINOR_TEMPLE_COUNT} keys collected.")
                    elif not self.temple_nearby_announced:
                        self.speak(f"Temple of {temple['key_name']} nearby. Tune to {temple_freq:.1f} Hz to receive the key.")
                        self.temple_nearby_announced = True

                elif key_index == -1:  # Halls of Amenti
                    if len(self.temple_keys) >= MASTER_TEMPLE_UNLOCK_KEYS and self.consciousness_name in ['enlightened', 'ascended']:
                        if not self.visited_amenti:
                            self.speak("The Halls of Amenti open before you. Ancient wisdom floods your consciousness.")
                            self.visited_amenti = True
                            self.amenti_blessing_active = True
                            # Apply Amenti rewards
                            self.resonance_width *= AMENTI_REWARDS['permanent_resonance_boost']
                            self.consciousness_value = 1.0  # Unlock ascended
                            self.consciousness_name = 'ascended'
                    elif not self.amenti_sealed_announced:
                        missing = MINOR_TEMPLE_COUNT - len(self.temple_keys)
                        self.speak(f"The Halls of Amenti remain sealed. {missing} more temple keys needed, or consciousness level insufficient.")
                        self.amenti_sealed_announced = True
                break
        else:
            # Not near any temple - reset announcement flags
            self.temple_nearby_announced = False
            self.amenti_sealed_announced = False

        self.last_temple_check = self.simulation_time

    def check_ley_line_proximity(self, ley_lines):
        """Check if ship is on a ley line for speed boost."""
        was_on_ley_line = self.on_ley_line
        self.on_ley_line = False
        self.current_ley_line = None

        for ley_line in ley_lines:
            # Calculate distance to line segment
            start = ley_line['start']
            end = ley_line['end']
            line_vec = end - start
            line_len = np.linalg.norm(line_vec)
            if line_len < 1e-6:
                continue

            # Project position onto line
            t = np.dot(self.position - start, line_vec) / (line_len ** 2)
            t = np.clip(t, 0, 1)
            closest_point = start + t * line_vec
            dist_to_line = np.linalg.norm(self.position - closest_point)

            if dist_to_line < LEY_LINE_WIDTH:
                self.on_ley_line = True
                self.current_ley_line = ley_line
                break

        if self.on_ley_line and not was_on_ley_line and not self.ley_line_announced:
            self.speak(f"Entering {self.current_ley_line['name']}. Speed enhanced.")
            self.ley_line_announced = True
        elif not self.on_ley_line and was_on_ley_line:
            self.speak("Leaving ley line. Normal speed resumed.")
            self.ley_line_announced = False

    def check_pyramid_proximity(self, pyramids):
        """Check for nearby pyramids for enhanced healing."""
        was_near_pyramid = self.near_pyramid is not None
        self.near_pyramid = None

        scan_range = self.get_effective_scan_range()
        for pyramid in pyramids:
            dist = np.linalg.norm(self.position - pyramid['pos'])
            if dist < scan_range:
                self.near_pyramid = pyramid
                break

        if self.near_pyramid and not was_near_pyramid and not self.pyramid_announced:
            self.speak(f"Entering {self.near_pyramid['name']}. Resonance chamber at 118 Hz activated.")
            self.pyramid_announced = True
        elif not self.near_pyramid and was_near_pyramid:
            self.speak("Leaving pyramid resonance chamber.")
            self.pyramid_announced = False

    # ===== CONSCIOUSNESS LEVEL SYSTEM =====
    def update_consciousness(self, dt):
        """Update consciousness level based on resonance state."""
        avg_res = np.mean(self.resonance_levels)

        # Gain consciousness at high resonance, decay at low
        if avg_res > 0.8:
            self.consciousness_value = min(1.0, self.consciousness_value + CONSCIOUSNESS_GAIN_RATE * dt)
        elif avg_res < 0.3:
            self.consciousness_value = max(0.0, self.consciousness_value - CONSCIOUSNESS_DECAY_RATE * dt)

        # Apply pyramid boost
        if self.near_pyramid:
            pyramid_freq_match = any(
                PYRAMID_RESONANCE_RANGE[0] <= self.r_drive[i] <= PYRAMID_RESONANCE_RANGE[1]
                for i in range(N_DIMENSIONS)
            )
            if pyramid_freq_match:
                self.consciousness_value = min(1.0, self.consciousness_value + CONSCIOUSNESS_GAIN_RATE * PYRAMID_CONSCIOUSNESS_BOOST * dt)

        # Determine consciousness level name
        old_name = self.consciousness_name
        for level_name, level_info in CONSCIOUSNESS_LEVELS.items():
            if self.consciousness_value >= level_info['threshold']:
                self.consciousness_name = level_name

        # Announce level changes
        if self.consciousness_name != old_name and not self.consciousness_announced:
            level_info = CONSCIOUSNESS_LEVELS[self.consciousness_name]
            self.speak(f"Consciousness level: {self.consciousness_name.capitalize()}. {level_info['desc']}.")
            self.consciousness_announced = True
        elif self.consciousness_name == old_name:
            self.consciousness_announced = False

    # ===== BRAINWAVE STATE DETECTION =====
    def detect_brainwave_state(self):
        """Detect current brainwave state based on Schumann resonance proximity."""
        # Check if any drive frequency is near brainwave ranges
        for state_name, state_info in BRAINWAVE_STATES.items():
            freq_min, freq_max = state_info['freq_range']
            # Scale up to audible range (multiply by 50 for 0.5-100 Hz to 25-5000 Hz mapping)
            scaled_min = freq_min * 50
            scaled_max = freq_max * 50

            for i in range(N_DIMENSIONS):
                drive_freq = self.r_drive[i]
                # Also check if drive divided by factor is in brainwave range
                if freq_min <= drive_freq / 50 <= freq_max or freq_min * 50 <= drive_freq <= freq_max * 50:
                    if self.consciousness_level != state_name:
                        self.consciousness_level = state_name
                        self.speak(f"Brainwave state: {state_name.capitalize()}. {state_info['state'].replace('_', ' ').capitalize()} mode.")
                        # Apply brainwave effect
                        effect = state_info['effect']
                        if effect == 'auto_repair':
                            self.resonance_integrity = min(1.0, self.resonance_integrity + 0.05)
                        elif effect == 'rift_vision':
                            self.speak("Enhanced rift perception activated.")
                        elif effect == 'fast_tuning':
                            # Already handled in update loop
                            pass
                    return
        # Default to beta if no match
        if self.consciousness_level != 'beta':
            self.consciousness_level = 'beta'

    # ===== ASTRAL MODE UPDATE =====
    def update_astral_mode(self, dt):
        """Update astral projection state and timer."""
        if not self.astral_mode:
            return

        self.astral_timer -= dt
        if self.astral_timer <= 0:
            self.speak("Astral projection time limit reached. Returning to body.")
            self.exit_astral_mode()
            return

        # Check distance from body
        dist_from_body = np.linalg.norm(self.position - self.astral_body_pos)
        was_too_far = getattr(self, 'astral_too_far', False)
        if dist_from_body > ASTRAL_PROJECTION_RANGE:
            if not was_too_far:
                self.speak("Warning: Astral form too far from body. Connection weakening.")
                self.astral_too_far = True
            # Pull back toward body
            direction = self.astral_body_pos - self.position
            self.position += direction * 0.1
        else:
            self.astral_too_far = False

        # Astral form moves faster
        self.velocity *= ASTRAL_SPEED_MULT

    def detect_harmonic_relationships(self):
        """
        Detect harmonic relationships between dimensions based on frequency ratios.

        Returns dict of detected harmonics with their effects.
        """
        detected = {}

        # Check all pairs of dimensions
        for i in range(N_DIMENSIONS):
            for j in range(i + 1, N_DIMENSIONS):
                freq_i = self.r_drive[i]
                freq_j = self.r_drive[j]

                # Calculate ratio (always higher/lower to get ratio >= 1)
                if freq_i == 0 or freq_j == 0:
                    continue

                ratio = max(freq_i, freq_j) / min(freq_i, freq_j)

                # Check against known harmonic ratios
                for harmonic_name, target_ratio in HARMONIC_RATIOS.items():
                    tolerance = target_ratio * HARMONIC_TOLERANCE
                    if abs(ratio - target_ratio) < tolerance:
                        key = f"{harmonic_name}_d{i+1}_d{j+1}"
                        detected[key] = {
                            'name': harmonic_name,
                            'dimensions': (i, j),
                            'ratio': ratio,
                            'target_ratio': target_ratio
                        }
                        break  # Only detect one harmonic per pair

        return detected

    def apply_harmonic_bonuses(self, harmonics):
        """
        Apply bonuses based on detected harmonic relationships.

        Args:
            harmonics: Dict of detected harmonics from detect_harmonic_relationships()
        """
        if not harmonics:
            return

        # Track newly detected harmonics
        new_harmonics = []

        for key, harmonic in harmonics.items():
            name = harmonic['name']
            dims = harmonic['dimensions']

            # Check if this is a new harmonic (not currently active)
            if key not in self.active_harmonics:
                new_harmonics.append((name, dims))

            # Update or add to active harmonics with expiry time
            self.active_harmonics[key] = (dims, self.simulation_time + HARMONIC_BONUS_DURATION)

        # Announce new harmonics and play chimes
        for name, dims in new_harmonics:
            dim_names = [f"dimension {d+1}" for d in dims]
            self.speak(f"{name.replace('_', ' ').title()} harmonic detected between {' and '.join(dim_names)}.")

            # Play appropriate chime
            chime_map = {
                'octave': self.audio_system.octave_chime,
                'perfect_fifth': self.audio_system.fifth_chime,
                'golden': self.audio_system.golden_chime,
                'perfect_fourth': self.audio_system.fourth_chime,
                'major_third': self.audio_system.major_third_chime,
                'minor_third': self.audio_system.minor_third_chime,
                'major_sixth': self.audio_system.major_sixth_chime,
                'minor_sixth': self.audio_system.minor_sixth_chime,
                'tritone': self.audio_system.tritone_chime,
            }
            if name in chime_map:
                self.audio_system.active_sound_effects.append(
                    SoundEffect(chime_map[name], pan=0.0, volume=self.audio_system.effect_volume)
                )

        # Apply bonuses based on active harmonics
        for key, (dims, expiry) in list(self.active_harmonics.items()):
            if self.simulation_time > expiry:
                # Harmonic expired
                del self.active_harmonics[key]
                continue

            # Extract harmonic type from key
            harmonic_type = key.split('_d')[0]

            # Apply effects based on harmonic type
            if harmonic_type == 'octave':
                # Velocity boost in both dimensions
                for dim in dims:
                    self.velocity[dim] *= 1.1

            elif harmonic_type == 'perfect_fifth':
                # Stability bonus - reduce dissonance timer
                self.dissonance_timer = max(0, self.dissonance_timer - 0.1)

            elif harmonic_type == 'golden':
                # Enhanced rift detection (handled in update method)
                # Crystal bonus on collection
                pass

            elif harmonic_type == 'perfect_fourth':
                # Integrity regeneration
                self.resonance_integrity = min(1.0, self.resonance_integrity + 0.001)

            elif harmonic_type == 'major_third':
                # Resonance width expansion - easier tuning
                # Temporary bonus during harmonic duration
                pass  # Handled via HARMONIC_BONUS_MULTIPLIER

            elif harmonic_type == 'minor_third':
                # Enhanced vibrato depth - richer sound (already in audio system)
                pass

            elif harmonic_type == 'major_sixth':
                # Power buildup acceleration
                for dim in dims:
                    self.resonance_power[dim] += 0.05

            elif harmonic_type == 'minor_sixth':
                # Crystal detection range boost (subtle effect)
                pass

            elif harmonic_type == 'tritone':
                # Devil's interval - chaotic effect!
                # Small velocity perturbation for dramatic effect
                for dim in dims:
                    self.velocity[dim] += random.uniform(-0.2, 0.2)

    # Ascension logic when threshold reached
    def ascend(self):
        # Trigger ascension, reset position, and regenerate universe
        self.speak("Ascension achieved! Warping to harmonious new universe.")
        self.position = np.zeros(N_DIMENSIONS)
        self.activate_golden_harmony()
        # Note: Universe regeneration should be handled by main module
        # Set a flag that main can check to regenerate celestial bodies
        self.needs_universe_regeneration = True
        # New: Clear rifts and sounds
        self.rifts.clear()
        self.audio_system.active_sound_effects.clear()

    # Apply selected upgrade
    def apply_upgrade(self):
        # Apply upgrade if enough crystals
        upgrade = self.upgrades[self.hud_index]
        if self.crystals_collected >= upgrade['cost']:
            upgrade['effect']()
            self.crystals_collected -= upgrade['cost']
            self.speak(f"{upgrade['name']} upgraded. Cost: {upgrade['cost']} crystals.")
        else:
            self.speak("Insufficient crystals.")

    def enter_rift(self, rift):
        self.position += np.random.uniform(-20, 20, N_DIMENSIONS) * PHI
        self.speak(f"Entering {rift['type']} rift—golden warp activated.")
        if rift['type'] == 'crystal':
            self.crystals_collected += 1
        elif rift['type'] == 'hazard':
            self.resonance_integrity -= 0.1
        elif rift['type'] == 'perfect_fifth':
            self.crystal_bonus += 1
            self.speak("Perfect fifth rift grants eternal crystal bounty.")
        if rift['sound']  in self.audio_system.active_sound_effects:
            self.audio_system.active_sound_effects.remove(rift['sound'])
        self.rifts = [r for r in self.rifts if r is not rift]
        self.locked_rift = None
        self.locked_target = None
        self.locked_is_rift = False
        self.approached_rift_announced = False
        if self.lock_sound:
            if self.lock_sound  in self.audio_system.active_sound_effects:
                self.audio_system.active_sound_effects.remove(self.lock_sound)
            self.lock_sound = None

    # New: Save game
    def save_game(self):
        state = {
            'position': self.position,
            'velocity': self.velocity,
            'r_drive': self.r_drive,
            'base_f_target': self.base_f_target,
            'resonance_integrity': self.resonance_integrity,
            'crystals_collected': self.crystals_collected,
            'resonance_width': self.resonance_width,
            'max_velocity': self.max_velocity,
            'crystal_bonus': self.crystal_bonus,
            'golden_harmony_active': self.golden_harmony_active,
            'frequency_presets': self.frequency_presets,
            'stars': self.stars,
            'planets': self.planets,
            'nebulae': self.nebulae,
            'rifts': self.rifts  # Note: sounds can't be pickled, but we can recreate
        }
        with open('savegame.pkl', 'wb') as f:
            pickle.dump(state, f)
        self.speak("Game saved.")

    # New: Load game
    def load_game(self):
        try:
            with open('savegame.pkl', 'rb') as f:
                state = pickle.load(f)
            self.position = state['position']
            self.velocity = state['velocity']
            self.r_drive = state['r_drive']
            self.base_f_target = state['base_f_target']
            self.resonance_integrity = state['resonance_integrity']
            self.crystals_collected = state['crystals_collected']
            self.resonance_width = state['resonance_width']
            self.max_velocity = state['max_velocity']
            self.crystal_bonus = state['crystal_bonus']
            self.golden_harmony_active = state['golden_harmony_active']
            self.frequency_presets = state.get('frequency_presets', {})  # Backwards compatible
            # Store loaded celestial bodies in ship (main.py will read these)
            self.stars = state['stars']
            self.planets = state['planets']
            self.nebulae = state['nebulae']
            self.rifts = state['rifts']
            # Recreate rift sounds
            for rift in self.rifts:
                hum_waveform = self.audio_system.rift_hum_waveform.copy()
                sound = SoundEffect(hum_waveform, loop=True, volume=0.0)
                self.audio_system.active_sound_effects.append(sound)
                rift['sound'] = sound
            # Signal main.py to reload celestial bodies from ship
            self.needs_universe_regeneration = True
            self.speak("Game loaded.")
        except:
            self.speak("No save file found.")

    # Update ship state
    def update(self, dt, celestial_bodies, keys, temples=None, ley_lines=None, pyramids=None):
        # No global variables needed - using instance variables
        # Skip updates in menu modes
        if self.hud_mode or self.upgrade_mode or self.starmap_mode or self.rift_selection_mode:
            return

        # ===== ATLANTEAN STRUCTURE PROXIMITY CHECKS =====
        if temples:
            self.check_temple_proximity(temples)
        if ley_lines:
            self.check_ley_line_proximity(ley_lines)
        if pyramids:
            self.check_pyramid_proximity(pyramids)

        # New: Idle mode check
        if time.time() - self.last_input_time > IDLE_TIME_THRESHOLD and not self.idle_mode:
            self.idle_mode = True
            self.speak("Entering cosmic meditation mode.")

        if self.idle_mode:
            # Slowly auto-tune
            for i in range(N_DIMENSIONS):
                self.r_drive[i] += (self.f_target[i] - self.r_drive[i]) * 0.01
            # Play evolving chord
            if not any(np.array_equal(e.waveform, self.audio_system.chord_waveform) for e  in self.audio_system.active_sound_effects):
                self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.chord_waveform, loop=True, volume=self.audio_system.effect_volume * 0.3))

        # Handle landed mode: Zero velocity, shift targets based on biome
        if self.landed_mode:
            self.velocity = np.zeros(N_DIMENSIONS)
            shift = 10 * dt if self.planet_biome == 'dissonant' else 1 * dt
            self.f_target = [f + random.uniform(-shift, shift) for f in self.f_target]
            self.f_target = [max(FREQUENCY_RANGE[0], min(FREQUENCY_RANGE[1], f)) for f in self.f_target]
            for i in range(N_DIMENSIONS):
                delta_f = self.r_drive[i] - self.f_target[i]
                self.resonance_levels[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
            return

        # Calculate environmental influence on targets from nearby bodies (exclude locked target to avoid feedback loop)
        env_influence = np.zeros(N_DIMENSIONS)
        for body in celestial_bodies:
            if self.locked_target is not None and np.all(body['pos'] == self.locked_target):
                continue  # Skip influence from the locked target itself
            dists = np.abs(self.position - body['pos'])
            close_dims = dists < INTERACTION_DISTANCE
            if np.any(close_dims):
                body_freq = body['freq']
                for d in range(N_DIMENSIONS):
                    if close_dims[d]:
                        env_influence[d] += (INTERACTION_DISTANCE - dists[d]) / INTERACTION_DISTANCE * body_freq * PHI**d
        self.f_target = [self.base_f_target[i] + env_influence[i] for i in range(N_DIMENSIONS)]
        self.f_target = [max(FREQUENCY_RANGE[0], min(FREQUENCY_RANGE[1], f)) for f in self.f_target]

        # Autopilot to locked target (refined with global slowdown)
        if self.locked_target is not None:
            dir_vec = self.locked_target - self.position
            norm = np.linalg.norm(dir_vec)
            if norm < 1e-6:
                norm = 1e-6  # Avoid zero division
            stop_dist = RIFT_ALIGNMENT_TOLERANCE if self.locked_is_rift else 1.0
            if norm < stop_dist:
                for i in range(N_DIMENSIONS):
                    self.r_drive[i] = self.f_target[i]  # Reset to stop
                self.velocity = np.zeros(N_DIMENSIONS)  # Force zero velocity
                if self.locked_is_rift and not self.approached_rift_announced:
                    self.speak("Approached rift - ready for entry.")
                    self.approached_rift_announced = True
                elif not self.locked_is_rift:
                    self.locked_target = None
                    self.locked_is_rift = False
                    if self.lock_sound:
                        if self.lock_sound  in self.audio_system.active_sound_effects:
                            self.audio_system.active_sound_effects.remove(self.lock_sound)
                        self.lock_sound = None
                    self.speak("Target reached.")
            else:
                slowdown_factor = min(1.0, norm / SLOWDOWN_DIST)
                for i in range(N_DIMENSIONS):
                    dir_i = dir_vec[i]
                    desired_vel_i = (dir_i / norm) * self.max_velocity * slowdown_factor
                    target_res = min(0.999, abs(desired_vel_i) / self.max_velocity) if abs(desired_vel_i) > 0.01 else 0
                    if target_res > 0:
                        d_over_w = np.sqrt(1 / target_res - 1)
                        delta = self.resonance_width * d_over_w
                        delta_f = np.sign(desired_vel_i) * delta
                        target_drive = self.f_target[i] + delta_f
                    else:
                        target_drive = self.f_target[i]
                    if norm < SLOWDOWN_DIST / 2:
                        self.r_drive[i] = target_drive  # Snap when close to avoid oscillation
                    else:
                        # Navigation mode boosts autopilot interpolation rate
                        autopilot_rate = 0.1
                        if self.tuaoi_mode == 'navigation':
                            autopilot_rate *= TUAOI_MODES['navigation']['rate']  # 1.5x faster
                        self.r_drive[i] += (target_drive - self.r_drive[i]) * autopilot_rate
                # Update lock sound based on alignment
                projected_pos = project_to_2d(dir_vec, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2)
                self.lock_sound.pan = np.sin(angle)
                misalignment = abs(angle)
                self.lock_sound.pitch = 1.0 + misalignment / 180.0
                self.lock_sound.waveform = (self.audio_system.beep_waveform if not self.locked_is_rift else self.audio_system.rift_beep_waveform) * self.lock_sound.pitch
                self.lock_sound.volume = self.audio_system.beep_volume

        # Auto-rotate view to center locked target horizontally (for all locked targets)
        if self.locked_target is not None:
            dir_vec = self.locked_target - self.position
            norm = np.linalg.norm(dir_vec)
            if norm > 1.0:  # Stop rotating when very close to avoid jitter
                if np.linalg.norm(dir_vec[[0,3]]) > 1e-6:  # Avoid division by zero
                    target_r = np.arctan2(dir_vec[3], dir_vec[0])
                    projected_x = dir_vec[0] * np.cos(target_r) + dir_vec[3] * np.sin(target_r)
                    if projected_x < 0:
                        target_r += np.pi
                else:
                    target_r = self.view_rotation
                # Shortest angle adjustment
                delta_r = target_r - self.view_rotation
                if delta_r > np.pi:
                    delta_r -= 2 * np.pi
                elif delta_r < -np.pi:
                    delta_r += 2 * np.pi
                self.view_rotation += delta_r * 0.5  # Interpolate

        # Calculate resonance and velocity per dimension
        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - self.f_target[i]
            # Transcendence mode boosts resonance width for higher dimensions (dims 4 & 5)
            effective_width = self.resonance_width
            if self.tuaoi_mode == 'transcendence' and i >= 3:
                effective_width *= TUAOI_MODES['transcendence']['rate']  # 1.4x easier tuning
            self.resonance_levels[i] = 1 / (1 + (delta_f / effective_width)**2)
            if self.resonance_levels[i] > PERFECT_RESONANCE_THRESHOLD and self.prev_resonance_levels[i] <= PERFECT_RESONANCE_THRESHOLD:
                self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.ping_waveform, pan=0.0, volume=self.audio_system.effect_volume))
            if self.resonance_levels[i] > POWER_BUILD_THRESHOLD:
                self.resonance_power[i] += dt
            else:
                self.resonance_power[i] = 0
            boost = 1 + (self.resonance_power[i] / POWER_BUILD_TIME) * PHI
            self.velocity[i] = self.max_velocity * self.resonance_levels[i] * np.sign(delta_f) * boost

        # Apply ley line speed boost
        if self.on_ley_line:
            self.velocity *= LEY_LINE_SPEED_MULT

        # Apply Merkaba velocity boost
        if self.merkaba_active:
            self.velocity *= MERKABA_VELOCITY_BOOST

        # Apply Tuaoi power mode velocity boost
        if self.tuaoi_mode == 'power':
            self.velocity *= TUAOI_MODES['power']['rate']

        # Apply pyramid healing
        if self.near_pyramid:
            pyramid_freq_match = any(
                PYRAMID_RESONANCE_RANGE[0] <= self.r_drive[i] <= PYRAMID_RESONANCE_RANGE[1]
                for i in range(N_DIMENSIONS)
            )
            if pyramid_freq_match:
                self.resonance_integrity = min(1.0, self.resonance_integrity + PYRAMID_HEALING_MULT * 0.01 * dt)

        # Check for harmonic relationships between dimensions
        if self.simulation_time - self.last_harmonic_check > HARMONIC_DETECTION_INTERVAL:
            harmonics = self.detect_harmonic_relationships()
            self.apply_harmonic_bonuses(harmonics)
            self.last_harmonic_check = self.simulation_time

        # ===== ATLANTEAN FEATURE DETECTION =====

        # Solfeggio frequency detection
        if self.simulation_time - self.last_solfeggio_check > 0.5:
            for freq, info in SOLFEGGIO_FREQUENCIES.items():
                for i in range(N_DIMENSIONS):
                    if abs(self.r_drive[i] - freq) < SOLFEGGIO_TOLERANCE:
                        if freq not in self.active_solfeggio:
                            self.speak(f"Solfeggio {info['name']} frequency detected. {info['desc'].capitalize()}.")
                        self.active_solfeggio[freq] = (info['effect'], self.simulation_time + 2.0)
            # Clean up expired solfeggio
            self.active_solfeggio = {f: (e, t) for f, t in self.active_solfeggio.items() for e, t in [(self.active_solfeggio[f][0], self.active_solfeggio[f][1])] if t > self.simulation_time}
            self.last_solfeggio_check = self.simulation_time

        # Merkaba activation check (all 5 dimensions above threshold)
        all_above_threshold = all(res > MERKABA_ACTIVATION_THRESHOLD for res in self.resonance_levels)
        if all_above_threshold and not self.merkaba_active:
            self.merkaba_active = True
            if not self.merkaba_announced:
                self.speak("Merkaba activated. Light vehicle field engaged. All realms in harmonic alignment.")
                self.merkaba_announced = True
        elif not all_above_threshold and self.merkaba_active:
            self.merkaba_active = False
            self.merkaba_announced = False
            self.speak("Merkaba field collapsed. Realign frequencies.")

        # Temple resonance check (110 Hz - ancient healing frequency)
        temple_resonance_active = any(
            TEMPLE_RESONANCE_RANGE[0] <= self.r_drive[i] <= TEMPLE_RESONANCE_RANGE[1]
            for i in range(N_DIMENSIONS)
        )
        if temple_resonance_active and not self.in_temple_resonance:
            self.in_temple_resonance = True
            if not self.temple_announced:
                self.speak("Temple resonance detected. Ancient healing frequency 110 hertz active.")
                self.temple_announced = True
        elif not temple_resonance_active and self.in_temple_resonance:
            self.in_temple_resonance = False
            self.temple_announced = False

        # Apply Tuaoi Crystal mode effects
        tuaoi_info = TUAOI_MODES[self.tuaoi_mode]
        if self.tuaoi_mode == 'healing':
            # Slow integrity regeneration
            self.resonance_integrity = min(1.0, self.resonance_integrity + tuaoi_info['rate'] * dt)
        elif self.tuaoi_mode == 'power':
            # Velocity boost handled in velocity calculation
            pass
        elif self.tuaoi_mode == 'regeneration' and self.in_temple_resonance:
            # Enhanced healing in temple resonance
            self.resonance_integrity = min(1.0, self.resonance_integrity + TEMPLE_HEALING_RATE * dt)

        # Apply Merkaba bonuses
        if self.merkaba_active:
            # Reduce integrity damage (shield effect)
            pass  # Applied when damage is calculated

        # ===== EXTENDED ATLANTEAN FEATURE UPDATES =====
        # Update consciousness level
        self.update_consciousness(dt)

        # Detect brainwave state
        self.detect_brainwave_state()

        # Update astral projection mode
        self.update_astral_mode(dt)

        # Update intention navigation
        self.update_intention_navigation(dt)

        # Decay pattern bonus timer
        if self.pattern_bonus_timer > 0:
            self.pattern_bonus_timer -= dt

        # Handle dissonance if average resonance low
        avg_res = np.mean(self.resonance_levels)
        if avg_res < DISSONANCE_THRESHOLD:
            self.dissonance_timer += dt
            if self.dissonance_timer > DISSONANCE_DURATION:
                self.velocity += np.random.uniform(-1, 1, N_DIMENSIONS) * 0.5
                self.speak("Dissonance detected—retune!")
                self.dissonance_timer = 0.0
        else:
            self.dissonance_timer = 0.0

        # Verbose alerts for resonance changes
        for i in range(N_DIMENSIONS):
            change = abs(self.resonance_levels[i] - self.prev_resonance_levels[i])
            if self.verbose_mode > 0 and change > 0.1:
                self.speak(f"Alert: Resonance in dim {i+1} now {self.resonance_levels[i]:.2f}.")
            if self.verbose_mode == 2 and self.simulation_time % 5 < DT:
                hud_status = f"Selected Dim: {self.selected_dim + 1}. Drive Freq: {self.r_drive[self.selected_dim]:.2f} Hz. Target Freq: {self.f_target[self.selected_dim]:.2f} Hz. Resonance: {self.resonance_levels[self.selected_dim]:.2f}. Speed: {np.linalg.norm(self.velocity):.2f} u/s. Volume: {int(self.audio_system.master_volume * 100)} percent. Integrity: {self.resonance_integrity:.2f}. Crystals: {self.crystals_collected}. Status: {'Landed' if self.landed_mode else 'In Flight'}."
                self.speak(hud_status)
        self.prev_resonance_levels = self.resonance_levels.copy()

        # New: Easter egg check
        if all(abs(rd - EASTER_EGG_FREQ) < EASTER_EGG_TOLERANCE for rd in self.r_drive):
            if not self.easter_egg_announced:
                self.speak("You are the universe experiencing itself.")
                self.easter_egg_announced = True
        else:
            self.easter_egg_announced = False

        # Random rift generation if high resonance
        if random.random() < 0.001 and avg_res > 0.9:
            rift_pos = self.position + np.random.uniform(-15, 15, N_DIMENSIONS)
            rift_pos[3] = rift_pos[0] * PHI
            rift_pos[4] = rift_pos[1] * PHI
            rift_type = random.choice(['boost', 'crystal', 'hazard'])
            hum_waveform = self.audio_system.rift_hum_waveform.copy()
            sound = SoundEffect(hum_waveform, loop=True, volume=0.0)
            self.audio_system.active_sound_effects.append(sound)
            self.rifts.append({'pos': rift_pos, 'timer': RIFT_FADE_TIME, 'type': rift_type, 'sound': sound, 'self.last_beep_time': self.simulation_time})
            projected_pos = project_to_2d(rift_pos - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
            dir_str = "left" if angle < 0 else "right"
            self.speak(f"{rift_type.capitalize()} Harmonic Chamber detected at {abs(angle):.1f} degrees {dir_str}.")
        # New: Super-rare perfect fifth rift
        if all(abs(self.r_drive[i] - self.f_target[i]) < PERFECT_FIFTH_TOLERANCE for i in range(N_DIMENSIONS)) and random.random() < PERFECT_FIFTH_PROB:
            rift_pos = self.position + np.random.uniform(-15, 15, N_DIMENSIONS)
            rift_pos[3] = rift_pos[0] * PHI
            rift_pos[4] = rift_pos[1] * PHI
            rift_type = 'perfect_fifth'
            hum_waveform = self.audio_system.rift_hum_waveform.copy()
            sound = SoundEffect(hum_waveform, loop=True, volume=0.0)
            self.audio_system.active_sound_effects.append(sound)
            self.rifts.append({'pos': rift_pos, 'timer': RIFT_FADE_TIME, 'type': rift_type, 'sound': sound, 'self.last_beep_time': self.simulation_time})
            projected_pos = project_to_2d(rift_pos - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
            dir_str = "left" if angle < 0 else "right"
            self.speak(f"Mythical Perfect Fifth Harmonic Chamber materialized at {abs(angle):.1f} degrees {dir_str}!")

        # Update rifts: Fade timers, sounds, and beeps
        to_remove = []
        for i, rift in enumerate(self.rifts):
            rift['timer'] -= dt
            if rift['timer'] <= 0:
                if rift is self.locked_rift:
                    self.locked_rift = None
                    self.locked_target = None
                    self.locked_is_rift = False
                    if self.lock_sound:
                        if self.lock_sound  in self.audio_system.active_sound_effects:
                            self.audio_system.active_sound_effects.remove(self.lock_sound)
                        self.lock_sound = None
                    self.speak("Locked rift faded into the void.")
                else:
                    self.speak("Rift faded into the void.")
                if rift['sound']  in self.audio_system.active_sound_effects:
                    self.audio_system.active_sound_effects.remove(rift['sound'])
                to_remove.append(i)
                continue
            if avg_res > 0.9:
                rift['timer'] += dt * PHI
            projected_pos = project_to_2d(rift['pos'] - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
            rift['sound'].pan = np.sin(angle * np.pi / 180)
            dist = np.linalg.norm(self.position - rift['pos'])
            rift['sound'].volume = max(0, self.audio_system.effect_volume * (1 - dist / RIFT_MAX_DIST)) * avg_res
            if rift is self.locked_rift:
                pan = np.sin(angle * np.pi / 180)
                centered_factor = 1 - abs(pan)  # High when aligned horizontally (|pan| ≈ 0)
                interval = 2.0 - 1.8 * centered_factor  # Faster beeps when aligned
                if self.simulation_time - rift['self.last_beep_time'] > interval:
                    self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.rift_beep_waveform, pan=pan, volume=self.audio_system.beep_volume))
                    rift['self.last_beep_time'] = self.simulation_time
            if dist < RIFT_ALIGNMENT_TOLERANCE:
                if avg_res <= RIFT_ENTRY_RES_THRESHOLD:
                    self.velocity += np.random.uniform(-1, 1, N_DIMENSIONS) * 0.5
                    self.speak("Dissonance prevents rift entry.")
        for i in sorted(to_remove, reverse=True):
            del self.rifts[i]

        # Update position with wrap-around
        self.position += self.velocity * dt
        self.position = (self.position + 100) % 200 - 100

        # Rift charge sequence logic
        if self.rift_charge_timer > 0:
            self.rift_charge_timer -= dt
            if self.locked_rift:
                dir_vec = self.locked_target - self.position
                projected_pos = project_to_2d(dir_vec, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
                vertical_error = abs(abs(angle) - 90)  # Error from ideal vertical
                if vertical_error > RIFT_ENTRY_ALIGNMENT_ANGLE / 2:
                    nudge = np.sign(angle - 90) * RIFT_NUDGE_RATE * dt
                    self.position[1] += nudge
                    self.position[2] += nudge * PHI
                if np.mean(self.resonance_levels) < RIFT_ENTRY_RES_THRESHOLD:
                    self.rift_charge_timer = 0
                    self.speak("Charge aborted—resonance too low. Retune.")
                elif self.rift_charge_timer <= 0:
                    # Success: Enter rift
                    if self.locked_rift:
                        self.enter_rift(self.locked_rift)
        else:
            # Guidance while locked but not charging
            if self.locked_is_rift and self.simulation_time - self.last_guidance_time > 10.0:  # Increased to 10s
                dist = np.linalg.norm(self.position - self.locked_target)
                avg_res = np.mean(self.resonance_levels) * 100
                dir_vec = self.locked_target - self.position
                if np.linalg.norm(dir_vec[[0,3]]) > 1e-6:
                    target_r = np.arctan2(dir_vec[3], dir_vec[0])
                    projected_x = dir_vec[0] * np.cos(target_r) + dir_vec[3] * np.sin(target_r)
                    if projected_x < 0:
                        target_r += np.pi
                else:
                    target_r = self.view_rotation
                delta_r = target_r - self.view_rotation
                if delta_r > np.pi:
                    delta_r -= 2 * np.pi
                elif delta_r < -np.pi:
                    delta_r += 2 * np.pi
                projected_pos = project_to_2d(dir_vec, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
                pan = np.sin(angle * np.pi / 180)
                align_pct = (1 - abs(pan)) * 100
                if abs(dist - self.prev_rift_dist) > 5 or abs(align_pct - self.prev_rift_align) > 10 or abs(avg_res - self.prev_rift_res) > 10:  # Only speak if changed significantly
                    self.speak(f"Rift status: Distance {dist:.1f}, alignment {align_pct:.0f}%, resonance {avg_res:.0f}%.")
                    if align_pct < 50:
                        dir = "right" if delta_r > 0 else "left"
                        self.speak(f"Rotate {dir} to center.")
                    self.prev_rift_dist = dist
                    self.prev_rift_align = align_pct
                    self.prev_rift_res = avg_res
                    self.last_guidance_time = self.simulation_time

        # Detect nearby celestial bodies
        scan_range = self.get_effective_scan_range()
        self.nearest_body = None
        min_dist = float('inf')
        near_any = False
        for body in celestial_bodies:
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < scan_range:
                near_any = True
                if dist < min_dist:
                    min_dist = dist
                    self.nearest_body = body
        if near_any and not self.near_object:
            self.near_object = True
            self.speak("Approaching celestial object. Resonance influenced.")
        elif not near_any and self.near_object:
            self.near_object = False
            self.speak("Leaving object vicinity. Base targets restored.")

        # Type-specific proximity ambient audio (if enabled)
        if self.ambient_sounds_enabled and self.nearest_body is not None:
            dist = np.linalg.norm(self.position - self.nearest_body['pos'])
            body_type = self.nearest_body['type']

            # Calculate pan for spatial audio
            projected_pos = project_to_2d(self.nearest_body['pos'] - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2)
            pan = np.sin(angle)

            # Play periodic beep for navigation
            if self.near_object and self.simulation_time - self.last_beep_time > 1.0:
                self.audio_system.active_sound_effects.append(SoundEffect(self.audio_system.beep_waveform, pan=pan, volume=self.audio_system.beep_volume))
                self.last_beep_time = self.simulation_time

            # Play type-specific ambient sounds based on proximity
            if body_type == 'star' and dist < STAR_HARMONY_RADIUS:
                # Star ambient sound
                stellar_type = self.nearest_body.get('stellar_type', 'main_sequence')
                volume = self.audio_system.effect_volume * (1.0 - dist / STAR_HARMONY_RADIUS) * 0.3

                # Map stellar type to waveform
                waveform_map = {
                    'red_giant': self.audio_system.red_giant_pulse,
                    'white_dwarf': self.audio_system.white_dwarf_whine,
                    'brown_dwarf': self.audio_system.brown_dwarf_rumble,
                    'main_sequence': None  # Main sequence stars are quieter
                }
                waveform = waveform_map.get(stellar_type)

                if waveform is not None:
                    # Stop old star sound if different type
                    if self.star_sound and self.star_sound in self.audio_system.active_sound_effects:
                        if self.star_sound.waveform is not waveform:
                            self.audio_system.active_sound_effects.remove(self.star_sound)
                            self.star_sound = None

                    # Start new star sound if not playing
                    if self.star_sound is None:
                        self.star_sound = SoundEffect(waveform, loop=True, pan=pan, volume=volume)
                        self.audio_system.active_sound_effects.append(self.star_sound)
                    else:
                        # Update existing sound
                        self.star_sound.pan = pan
                        self.star_sound.volume = volume

            elif body_type == 'nebula' and dist < NEBULA_DISSONANCE_RADIUS:
                # Nebula ambient sound
                nebula_type = self.nearest_body.get('nebula_type', 'emission')
                volume = self.audio_system.effect_volume * (1.0 - dist / NEBULA_DISSONANCE_RADIUS) * 0.4

                # Map nebula type to waveform
                waveform_map = {
                    'emission': self.audio_system.emission_nebula_drone,
                    'reflection': self.audio_system.reflection_nebula_shimmer,
                    'planetary': self.audio_system.planetary_nebula_layers,
                    'supernova_remnant': self.audio_system.supernova_remnant_chaos
                }
                waveform = waveform_map.get(nebula_type)

                if waveform is not None:
                    # Stop old nebula sound if different type
                    if self.nebula_sound and self.nebula_sound in self.audio_system.active_sound_effects:
                        if self.nebula_sound.waveform is not waveform:
                            self.audio_system.active_sound_effects.remove(self.nebula_sound)
                            self.nebula_sound = None

                    # Start new nebula sound if not playing
                    if self.nebula_sound is None:
                        self.nebula_sound = SoundEffect(waveform, loop=True, pan=pan, volume=volume)
                        self.audio_system.active_sound_effects.append(self.nebula_sound)
                    else:
                        # Update existing sound
                        self.nebula_sound.pan = pan
                        self.nebula_sound.volume = volume

            elif body_type == 'planet' and dist < INTERACTION_DISTANCE:
                # Planet ambient sound
                exoplanet_type = self.nearest_body.get('exoplanet_type', 'super_earth')
                volume = self.audio_system.effect_volume * (1.0 - dist / INTERACTION_DISTANCE) * 0.3

                # Map exoplanet type to waveform
                waveform_map = {
                    'hot_jupiter': self.audio_system.hot_jupiter_roar,
                    'super_earth': self.audio_system.super_earth_tone,
                    'ocean_world': self.audio_system.ocean_world_flow,
                    'rogue_planet': self.audio_system.rogue_planet_ominous,
                    'ice_giant': self.audio_system.ice_giant_chime
                }
                waveform = waveform_map.get(exoplanet_type)

                if waveform is not None:
                    # Stop old planet sound if different type
                    if self.planet_sound and self.planet_sound in self.audio_system.active_sound_effects:
                        if self.planet_sound.waveform is not waveform:
                            self.audio_system.active_sound_effects.remove(self.planet_sound)
                            self.planet_sound = None

                    # Start new planet sound if not playing
                    if self.planet_sound is None:
                        self.planet_sound = SoundEffect(waveform, loop=True, pan=pan, volume=volume)
                        self.audio_system.active_sound_effects.append(self.planet_sound)
                    else:
                        # Update existing sound
                        self.planet_sound.pan = pan
                        self.planet_sound.volume = volume

        # Stop ambient sounds when leaving vicinity or if disabled
        if (not self.near_object or self.nearest_body is None) or not self.ambient_sounds_enabled:
            if self.star_sound and self.star_sound in self.audio_system.active_sound_effects:
                self.audio_system.active_sound_effects.remove(self.star_sound)
                self.star_sound = None
            if self.nebula_sound and self.nebula_sound in self.audio_system.active_sound_effects:
                self.audio_system.active_sound_effects.remove(self.nebula_sound)
                self.nebula_sound = None
            if self.planet_sound and self.planet_sound in self.audio_system.active_sound_effects:
                self.audio_system.active_sound_effects.remove(self.planet_sound)
                self.planet_sound = None

        # Apply nebula dissonance effects (if enabled)
        nebula_dissonance_active = False
        if self.nebula_dissonance_enabled and self.nearest_body is not None and self.nearest_body['type'] == 'nebula':
            dist = np.linalg.norm(self.position - self.nearest_body['pos'])
            if dist < NEBULA_DISSONANCE_RADIUS:
                dissonance = self.nearest_body.get('dissonance', 0.5)
                # Dissonance strength based on proximity and nebula type
                dissonance_strength = dissonance * (1.0 - dist / NEBULA_DISSONANCE_RADIUS)

                # Apply frequency drift to targets (makes tuning harder)
                freq_drift_amount = dissonance_strength * 15.0 * dt  # Up to 15 Hz/sec drift
                for i in range(N_DIMENSIONS):
                    # Random walk drift
                    drift = (random.random() - 0.5) * freq_drift_amount
                    self.f_target[i] = np.clip(self.f_target[i] + drift, *FREQUENCY_RANGE)

                # Apply turbulent velocity jitter (chaotic motion)
                if dissonance > 0.6:  # Only for high-dissonance nebulae
                    turbulence = dissonance_strength * 0.5  # Scale turbulence
                    for i in range(N_DIMENSIONS):
                        jitter = (random.random() - 0.5) * turbulence
                        self.velocity[i] += jitter

                nebula_dissonance_active = True

        # Store dissonance state for UI/feedback
        if nebula_dissonance_active and not hasattr(self, 'nebula_dissonance_announced'):
            self.speak("Warning: Entering nebula dissonance field. Frequencies unstable.")
            self.nebula_dissonance_announced = True
        elif not nebula_dissonance_active and hasattr(self, 'nebula_dissonance_announced') and self.nebula_dissonance_announced:
            self.speak("Nebula dissonance field cleared. Frequencies stable.")
            self.nebula_dissonance_announced = False

        # Announce landmarks in view during rotation
        self.prev_view_rotation = self.view_rotation
        if self.rotating_left or self.rotating_right:
            for body in celestial_bodies:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
                if abs(angle) < VIEW_LANDMARK_THRESHOLD and self.simulation_time - self.last_landmark_speak_time > LANDMARK_SPEECH_COOLDOWN:
                    self.speak(f"Object in view at {angle:.1f} degrees.")
                    self.last_landmark_speak_time = self.simulation_time

        # Handle landing timer
        if self.landing_timer > 0:
            self.landing_timer -= dt
            if self.landing_timer <= 0:
                # Apply exoplanet difficulty to landing success check
                landing_threshold = LANDING_THRESHOLD
                if self.nearest_body and self.nearest_body['type'] == 'planet':
                    difficulty = self.nearest_body.get('difficulty', 1.0)
                    landing_threshold *= difficulty

                if np.mean(self.resonance_levels) > landing_threshold and self.nearest_body and self.nearest_body['type'] == 'planet':
                    self.landed_mode = True
                    self.landed_planet = self.nearest_body['pos']
                    self.landed_planet_body = self.nearest_body  # Store full planet data
                    self.generate_crystals()  # This speaks the landing confirmation
                else:
                    self.resonance_integrity -= 0.1
                    if self.nearest_body and self.nearest_body['type'] != 'planet':
                        self.speak("Cannot anchor on this celestial body.")
                    else:
                        difficulty = self.nearest_body.get('difficulty', 1.0)
                        if difficulty > 1.0:
                            self.speak(f"Anchoring failed. This world requires exceptionally high harmonic alignment. Integrity reduced.")
                        else:
                            self.speak("Anchoring failed due to dissonance. Integrity reduced.")
                    if self.resonance_integrity < 0.5:
                        self.speak("Warning: Low integrity—repair needed.")

        # New: Autosave check
        if self.autosave_enabled and self.simulation_time - self.last_autosave_time > AUTOSAVE_INTERVAL:
            self.save_game()
            self.last_autosave_time = self.simulation_time

        # New: Check for sing mode silence and heartbeat
        if self.sing_mode and time.time() - self.last_sing_time > SING_SILENCE_THRESHOLD:
            # Fade to heartbeat pulse
            heartbeat_freq = self.last_detected_rhythm / 60.0  # BPM to Hz
            # Adjust drive signals to pulse (this would require modifying audio_callback logic, but for simplicity, add a pulse sound
            if not any(e.loop and e.volume == HEARTBEAT_VOLUME for e  in self.audio_system.active_sound_effects):
                heartbeat_wave = np.sin(2 * np.pi * heartbeat_freq * np.linspace(0, 1 / heartbeat_freq, int(SAMPLE_RATE / heartbeat_freq)))
                self.audio_system.active_sound_effects.append(SoundEffect(heartbeat_wave, loop=True, volume=HEARTBEAT_VOLUME))

