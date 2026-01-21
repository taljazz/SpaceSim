import asyncio
import platform
import pygame
import numpy as np
import sounddevice as sd
import random
import os
from cytolk import tolk

# Constants
N_DIMENSIONS = 5  # 3 spatial + 2 higher dimensions
SCREEN_WIDTH, SCREEN_HEIGHT = 800, 600
FPS = 60
DT = 1.0 / FPS
MAX_VELOCITY_BASE = 10.0  # Units/s for all dims, upgradable
RESONANCE_WIDTH_BASE = 10.0  # Hz, upgradable
FREQUENCY_RANGE = (200.0, 800.0)  # Hz
SAMPLE_RATE = 44100  # Audio sample rate
PHI = (1 + np.sqrt(5)) / 2  # Golden ratio
N_STARS = 200  # Increased for more procedural content
N_PLANETS_PER_STAR = 3  # Number of planets per star for grouping
N_NEBULAE = 10  # New: Added nebulae for more procedural content
ORBIT_RADIUS = 5.0  # Radius for planets orbiting stars
PLANET_RADIUS = 10.0
INTERACTION_DISTANCE = 15.0  # For dim interactions
N_FIBONACCI = 8  # For PHI-based generation
FIB_SEQ = [0, 1]
for _ in range(N_FIBONACCI - 2):
    FIB_SEQ.append(FIB_SEQ[-1] + FIB_SEQ[-2])
SCALE_FACTOR = 100.0 / FIB_SEQ[-1]  # For positioning
SPEECH_COOLDOWN = 0.5  # Seconds between repeats of the same message
VIEW_LANDMARK_THRESHOLD = 10.0  # Degrees for audible landmarks
ROTATION_SOUND_DURATION = 0.2  # For whoosh sound
LANDING_THRESHOLD = 0.8  # Avg resonance for landing
LANDING_TIME = 3.0  # Seconds to charge landing
CRYSTAL_COUNT_BASE = 3  # Per planet, upgradable
POWER_BUILD_THRESHOLD = 0.8  # Resonance for power buildup
POWER_BUILD_TIME = 5.0  # Seconds for full boost
DISSONANCE_THRESHOLD = 0.2  # Avg resonance for turbulence
DISSONANCE_DURATION = 10.0  # Seconds low resonance to trigger
RIFT_ALIGNMENT_TOLERANCE = 10.0  # Units for rift entry, increased for easier alignment
RIFT_FADE_TIME = 30.0  # Seconds before rift fades
RIFT_ENTRY_RES_THRESHOLD = 0.7  # Required avg resonance for entry
HUD_TEXT_SIZE_BASE = 24  # Font size, adjustable
HIGH_CONTRAST = False  # Toggle for inverted colors
CLICK_INTERVAL = 0.5  # Seconds between velocity tone plays
GRID_SIZE = 10  # For planet exploration grid
CRYSTAL_COLLECTION_THRESHOLD = 0.9  # Resonance to lock crystal
UPGRADE_COSTS = [1, 1, 2, 3, 5, 8, 13, 21]  # Fibonacci for tiered upgrades
ASCENSION_CRYSTAL_THRESHOLD = 21  # For golden ascension ending
RIFT_MAX_DIST = 20.0  # For volume modulation
TUNING_RATE = 100.0
TUNING_RATE_PLANET = 20.0  # Slower for precision on planets
SCANNER_RANGE = 50.0  # Increased to spot more objects on the starmap
SLOWDOWN_DIST = 20.0  # New: Distance within which to slow down approach
AUTO_SNAP_THRESHOLD = 0.6  # Increased for easier snapping

# Instructions text (updated with new features)
INSTRUCTIONS = """
Golden Spiral Spaceship Simulator Instructions

Controls:
- 1-5: Select dimension to tune (1: x, 2: y, 3: z, 4: higher1, 5: higher2)
- Up/Down Arrow: Increase/Decrease drive frequency in selected dim; In HUD mode: Navigate items
- Left/Right Arrow: Rotate view left/right (in flight); In HUD/Upgrade: Cycle groups/options
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
- =/-: Adjust master volume
- Shift + =/-: Adjust beep volume (planets/rifts/locks)
- Ctrl + =/-: Adjust effect volume (clicks/rotations/chords/hums)
- Alt + =/-: Adjust drive signal volume
- F1: Open this instructions file
- ESC: Quit

Resonance System:
- Tune r_drive close to f_target per dim for velocity (magnitude by resonance level, direction by sign).
- Power buildup: Sustain >0.8 resonance for boosts.
- Dissonance: Low resonance triggers turbulence jitter.
- Upgrades: Collect crystals on planets for tiered upgrades (width, velocity, etc.).

Viewing System:
- Rotate to mix higher dims into 2D projection—scan for rifts/objects with panned audio.

Rifts:
- Detected nearby with panned hum; rotate to center (pan=0) for entry.
- Requires high resonance; rewards warp boosts or bonuses.

Landing/Exploration:
- Near planet with high resonance, press L to land.
- On planet: Press W/A/S/D to move cursor on grid, scan/tune to crystals, collect with X.
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

# Initialize Pygame and Tolk
pygame.init()
tolk.load()
tolk.speak("Welcome to the Golden Spiral Spaceship Simulator. Resonance propulsion engaged. Harmonize with the universe.")

# Set up display
screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
pygame.display.set_caption("Golden Spiral Spaceship Simulator")
clock = pygame.time.Clock()
font = pygame.font.SysFont(None, HUD_TEXT_SIZE_BASE)

# SoundEffect class (extended for looping and dynamic pitch/pan)
class SoundEffect:
    def __init__(self, waveform, pan=0.0, pitch=1.0, loop=False, volume=1.0):
        self.waveform = waveform * pitch
        self.position = 0
        self.pan = pan
        self.loop = loop
        self.volume = volume

# Ship class with enhanced features
class Ship:
    def __init__(self):
        self.position = np.zeros(N_DIMENSIONS)
        self.velocity = np.zeros(N_DIMENSIONS)
        self.heading = 0.0
        self.r_drive = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]
        self.base_f_target = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]  # New: Fixed base for stability
        self.f_target = self.base_f_target[:]  # Initial copy
        self.selected_dim = 0
        self.near_object = False
        self.resonance_levels = np.zeros(N_DIMENSIONS)
        self.view_rotation = 0.0
        self.rotating_left = False
        self.rotating_right = False
        self.prev_view_rotation = 0.0
        self.landed_mode = False
        self.landed_planet = None
        self.landing_timer = 0.0
        self.resonance_integrity = 1.0
        self.crystals_collected = 0
        self.resonance_power = np.zeros(N_DIMENSIONS)
        self.dissonance_timer = 0.0
        self.verbose_mode = 1
        self.hud_text_size = HUD_TEXT_SIZE_BASE
        self.high_contrast = HIGH_CONTRAST
        self.resonance_width = RESONANCE_WIDTH_BASE
        self.max_velocity = MAX_VELOCITY_BASE
        self.crystal_count = CRYSTAL_COUNT_BASE
        self.prev_resonance_levels = np.zeros(N_DIMENSIONS)
        self.rifts = []  # List of dicts: {'pos': np.array, 'timer': float, 'type': str, 'sound': SoundEffect}
        self.last_click_time = [0.0] * N_DIMENSIONS
        self.verbose_toggled = False
        self.contrast_toggled = False
        self.text_size_adjusted = False
        self.instructions_opened = False
        # HUD dialog
        self.hud_mode = False
        self.hud_index = 0
        self.hud_items = []
        # Planet exploration
        self.cursor_pos = np.array([0, 0])  # 2D grid
        self.crystal_positions = []  # List of np.array [x,y]
        self.crystal_freqs = []  # List of lists [f1,f2,f3,f4,f5]
        self.locked_crystals = set()  # Indices collected
        self.planet_biome = 'harmonic'  # Affects difficulty
        self.approaching_lock_announced = False  # New: Flag for announcement
        # Upgrades
        self.upgrade_mode = False
        self.upgrades = [
            {'name': 'Resonance Width', 'cost': UPGRADE_COSTS[0], 'effect': self.upgrade_width, 'desc': 'Increases tuning tolerance by golden increment.'},
            {'name': 'Integrity Repair', 'cost': UPGRADE_COSTS[1], 'effect': self.upgrade_integrity, 'desc': 'Restores ship harmony.'},
            {'name': 'Max Velocity', 'cost': UPGRADE_COSTS[2], 'effect': self.upgrade_velocity, 'desc': 'Boosts top speed with divine proportion.'},
            {'name': 'Auto-Tune Helper', 'cost': UPGRADE_COSTS[3], 'effect': self.auto_tune, 'desc': 'Subtly aligns frequencies automatically.'},
            {'name': 'Crystal Growth', 'cost': UPGRADE_COSTS[4], 'effect': self.upgrade_crystal_count, 'desc': 'Increases crystals per planet.'},
            {'name': 'Golden Harmony Mode', 'cost': UPGRADE_COSTS[5], 'effect': self.activate_golden_harmony, 'desc': 'Permanent PHI multiplier to all stats for ascension prep.'}
        ]
        self.golden_harmony_active = False
        # Starmap
        self.starmap_mode = False
        self.starmap_index = 0
        self.starmap_items = []
        self.locked_target = None  # np.array pos or None
        self.lock_sound = None  # SoundEffect for lock alignment
        self.locked_is_rift = False  # New: Flag to indicate if locked target is a rift
        self.last_cursor_pos = np.array([0, 0])  # For detecting changes
        self.last_cursor_speak_time = 0.0  # For debouncing speech
        self.nearest_body = None  # To track the nearest celestial body

    def upgrade_width(self):
        self.resonance_width += PHI * 0.5

    def upgrade_integrity(self):
        self.resonance_integrity = min(1.0, self.resonance_integrity + PHI * 0.2)

    def upgrade_velocity(self):
        self.max_velocity *= PHI

    def auto_tune(self):
        for i in range(N_DIMENSIONS):
            self.r_drive[i] += (self.f_target[i] - self.r_drive[i]) * 0.1

    def upgrade_crystal_count(self):
        self.crystal_count += 1

    def activate_golden_harmony(self):
        self.golden_harmony_active = True
        self.max_velocity *= PHI
        self.resonance_width *= PHI
        speak_with_cooldown("Golden Harmony activated. The universe sings in perfect proportion.")

    def generate_crystals(self):
        self.crystal_positions = []
        self.crystal_freqs = []
        self.locked_crystals = set()
        self.planet_biome = random.choice(['harmonic', 'dissonant'])
        speak_with_cooldown(f"Landed on {self.planet_biome} biome planet.")
        for i in range(self.crystal_count):
            theta = i * 2 * np.pi * PHI
            r = FIB_SEQ[i % len(FIB_SEQ)] * (SCALE_FACTOR / 10)
            pos = np.array([r * np.cos(theta), r * np.sin(theta)])
            self.crystal_positions.append(pos)
            freqs = [random.uniform(*FREQUENCY_RANGE) for _ in range(N_DIMENSIONS)]
            self.crystal_freqs.append(freqs)
        freq_str = ', '.join([f"{f[0]:.2f}" for f in self.crystal_freqs])
        speak_with_cooldown(f"Crystals detected at frequencies: {freq_str} Hz in primary dim.")
        self.approaching_lock_announced = False  # Reset on new generation

    def handle_input(self, keys, events):
        global beep_volume, effect_volume, master_volume, drive_volume
        if self.hud_mode or self.upgrade_mode or self.starmap_mode:
            mode = 'hud' if self.hud_mode else 'upgrade' if self.upgrade_mode else 'starmap'
            for event in events:
                if event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_m and self.starmap_mode:
                        self.starmap_mode = False
                        speak_with_cooldown("Exiting starmap.")
                    elif event.key == pygame.K_u and (self.hud_mode or self.upgrade_mode):
                        self.hud_mode = False
                        self.upgrade_mode = False
                        speak_with_cooldown("Exiting menu.")
                    elif event.key == pygame.K_UP:
                        if mode == 'starmap' and len(self.starmap_items) > 1:
                            self.starmap_index = (self.starmap_index - 1) % len(self.starmap_items)
                            self.speak_starmap_item()
                        elif mode != 'starmap' and len(self.hud_items) > 1:
                            self.hud_index = (self.hud_index - 1) % len(self.hud_items)
                            self.speak_hud_item()
                    elif event.key == pygame.K_DOWN:
                        if mode == 'starmap' and len(self.starmap_items) > 1:
                            self.starmap_index = (self.starmap_index + 1) % len(self.starmap_items)
                            self.speak_starmap_item()
                        elif mode != 'starmap' and len(self.hud_items) > 1:
                            self.hud_index = (self.hud_index + 1) % len(self.hud_items)
                            self.speak_hud_item()
                    elif event.key == pygame.K_LEFT or event.key == pygame.K_RIGHT:
                        pass  # Group cycle if implemented
                    if self.upgrade_mode and event.key == pygame.K_RETURN:
                        self.apply_upgrade()
                    if self.starmap_mode and event.key == pygame.K_RETURN:
                        self.lock_on_starmap_item()
            return

        shift_pressed = keys[pygame.K_LSHIFT] or keys[pygame.K_RSHIFT]
        ctrl_pressed = keys[pygame.K_LCTRL] or keys[pygame.K_RCTRL]
        alt_pressed = keys[pygame.K_LALT] or keys[pygame.K_RALT]
        for event in events:
            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_1: self.selected_dim = 0; speak_with_cooldown("Tuning x dimension."); self.approaching_lock_announced = False
                elif event.key == pygame.K_2: self.selected_dim = 1; speak_with_cooldown("Tuning y dimension."); self.approaching_lock_announced = False
                elif event.key == pygame.K_3: self.selected_dim = 2; speak_with_cooldown("Tuning z dimension."); self.approaching_lock_announced = False
                elif event.key == pygame.K_4: self.selected_dim = 3; speak_with_cooldown("Tuning higher dimension one."); self.approaching_lock_announced = False
                elif event.key == pygame.K_5: self.selected_dim = 4; speak_with_cooldown("Tuning higher dimension two."); self.approaching_lock_announced = False
                elif event.key == pygame.K_v and not self.verbose_toggled:
                    self.verbose_mode = (self.verbose_mode + 1) % 3
                    modes = ["Low", "Medium", "High"]
                    speak_with_cooldown(f"Verbosity mode: {modes[self.verbose_mode]}.")
                    self.verbose_toggled = True
                elif event.key == pygame.K_m:
                    self.starmap_mode = not self.starmap_mode
                    if self.starmap_mode:
                        self.update_starmap_items()
                        self.starmap_index = 0
                        speak_with_cooldown("Entering starmap.")
                        self.speak_starmap_item()
                    else:
                        speak_with_cooldown("Exiting starmap.")
                elif event.key == pygame.K_c and not self.contrast_toggled:
                    self.high_contrast = not self.high_contrast
                    speak_with_cooldown(f"High contrast mode: {'on' if self.high_contrast else 'off'}.")
                    self.contrast_toggled = True
                elif event.key == pygame.K_q:
                    quick = f"Target in selected dim: {self.f_target[self.selected_dim]:.2f} Hz."
                    speak_with_cooldown(quick)
                elif event.key == pygame.K_l and not self.landed_mode and self.near_object and np.mean(self.resonance_levels) > LANDING_THRESHOLD and self.nearest_body and self.nearest_body['type'] == 'planet':
                    self.landing_timer = LANDING_TIME
                    speak_with_cooldown("Initiating landing sequence.")
                elif event.key == pygame.K_t and self.landed_mode:
                    self.landed_mode = False
                    self.landed_planet = None
                    speak_with_cooldown("Taking off from planet.")
                elif event.key == pygame.K_r:
                    status = f"Position: {self.position.round(2)}. Velocity: {self.velocity.round(2)}. Resonance levels: {self.resonance_levels.round(2)}. View rotation: {self.view_rotation:.2f} radians. {'Landed on planet.' if self.landed_mode else 'In space.'} Integrity: {self.resonance_integrity:.2f}. Crystals: {self.crystals_collected}. Power levels: {self.resonance_power.round(2)}."
                    speak_with_cooldown(status)
                elif event.key == pygame.K_u:
                    if self.landed_mode and len(self.locked_crystals) == self.crystal_count:
                        self.upgrade_mode = True
                        self.hud_index = 0
                        self.update_hud_items(upgrade=True)
                        speak_with_cooldown("Entering upgrade menu.")
                        self.speak_hud_item()
                    else:
                        self.hud_mode = True
                        self.hud_index = 0
                        self.update_hud_items()
                        speak_with_cooldown("Entering HUD dialog.")
                        self.speak_hud_item()
                elif event.key == pygame.K_t:
                    self.text_size_adjusted = True
                elif event.key == pygame.K_EQUALS and self.text_size_adjusted:
                    self.hud_text_size += 2
                    self.hud_text_size = max(12, min(48, self.hud_text_size))
                    speak_with_cooldown(f"Text size increased to {self.hud_text_size}.")
                elif event.key == pygame.K_MINUS and self.text_size_adjusted:
                    self.hud_text_size -= 2
                    self.hud_text_size = max(12, min(48, self.hud_text_size))
                    speak_with_cooldown(f"Text size decreased to {self.hud_text_size}.")
                elif event.key == pygame.K_F1 and not self.instructions_opened:
                    with open('instructions.txt', 'w') as f:
                        f.write(INSTRUCTIONS)
                    os.startfile('instructions.txt')
                    speak_with_cooldown("Instructions file opened.")
                    self.instructions_opened = True
                elif self.landed_mode and event.key == pygame.K_f:
                    self.scan_nearest_crystal()
                    self.approaching_lock_announced = False  # Reset on scan
                elif self.landed_mode and event.key == pygame.K_x:
                    self.collect_crystal()
                # Discrete movement on planet
                elif self.landed_mode:
                    moved = False
                    if event.key == pygame.K_w:
                        self.cursor_pos[1] += 1
                        moved = True
                    elif event.key == pygame.K_s:
                        self.cursor_pos[1] -= 1
                        moved = True
                    elif event.key == pygame.K_a:
                        self.cursor_pos[0] -= 1
                        moved = True
                    elif event.key == pygame.K_d:
                        self.cursor_pos[0] += 1
                        moved = True
                    if moved:
                        self.cursor_pos = np.clip(self.cursor_pos, -GRID_SIZE, GRID_SIZE)
                        speak_with_cooldown(f"Cursor at {self.cursor_pos.round(2)}.")
                # New volume controls
                elif event.key == pygame.K_EQUALS:
                    if alt_pressed:
                        drive_volume = min(1.0, drive_volume + 0.05)
                        speak_with_cooldown(f"Drive volume at {int(drive_volume * 100)} percent.")
                    elif shift_pressed:
                        beep_volume = min(1.0, beep_volume + 0.05)
                        speak_with_cooldown(f"Beep volume at {int(beep_volume * 100)} percent.")
                    elif ctrl_pressed:
                        effect_volume = min(1.0, effect_volume + 0.05)
                        speak_with_cooldown(f"Effect volume at {int(effect_volume * 100)} percent.")
                    else:
                        master_volume = min(1.0, master_volume + 0.05)
                        speak_with_cooldown(f"Master volume at {int(master_volume * 100)} percent.")
                elif event.key == pygame.K_MINUS:
                    if alt_pressed:
                        drive_volume = max(0.0, drive_volume - 0.05)
                        speak_with_cooldown(f"Drive volume at {int(drive_volume * 100)} percent.")
                    elif shift_pressed:
                        beep_volume = max(0.0, beep_volume - 0.05)
                        speak_with_cooldown(f"Beep volume at {int(beep_volume * 100)} percent.")
                    elif ctrl_pressed:
                        effect_volume = max(0.0, effect_volume - 0.05)
                        speak_with_cooldown(f"Effect volume at {int(effect_volume * 100)} percent.")
                    else:
                        master_volume = max(0.0, master_volume - 0.05)
                        speak_with_cooldown(f"Master volume at {int(master_volume * 100)} percent.")
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_v: self.verbose_toggled = False
                if event.key == pygame.K_c: self.contrast_toggled = False
                if event.key == pygame.K_t: self.text_size_adjusted = False
                if event.key == pygame.K_F1: self.instructions_opened = False

        rate = TUNING_RATE
        if self.landed_mode:
            rate = TUNING_RATE_PLANET
            # Dynamic tuning rate when landed
            if self.crystal_positions:
                dists = [np.linalg.norm(self.cursor_pos - pos) if idx not in self.locked_crystals else float('inf') for idx, pos in enumerate(self.crystal_positions)]
                nearest = np.argmin(dists)
                if dists[nearest] != float('inf'):
                    delta = abs(self.r_drive[self.selected_dim] - self.crystal_freqs[nearest][self.selected_dim])
                    rate = TUNING_RATE_PLANET * (delta / 50.0 + 0.1)
                    rate = max(1.0, min(TUNING_RATE_PLANET, rate))
                    if delta < 10:
                        if not self.approaching_lock_announced:
                            speak_with_cooldown("Approaching resonance lock.")
                            self.approaching_lock_announced = True
                    else:
                        self.approaching_lock_announced = False

        if keys[pygame.K_UP]:
            self.r_drive[self.selected_dim] += rate * DT
            self.r_drive[self.selected_dim] = min(self.r_drive[self.selected_dim], FREQUENCY_RANGE[1])
        if keys[pygame.K_DOWN]:
            self.r_drive[self.selected_dim] -= rate * DT
            self.r_drive[self.selected_dim] = max(self.r_drive[self.selected_dim], FREQUENCY_RANGE[0])

        if self.landed_mode:
            self.rotating_left = False  # Disable rotation on planet
            self.rotating_right = False
            return

        self.rotating_left = keys[pygame.K_LEFT]
        self.rotating_right = keys[pygame.K_RIGHT]
        if self.rotating_left:
            self.view_rotation -= 0.1 * DT
        if self.rotating_right:
            self.view_rotation += 0.1 * DT

    def update_hud_items(self, upgrade=False):
        if upgrade:
            self.hud_items = [f"{u['name']}: {u['desc']} Cost: {u['cost']}" for u in self.upgrades]
        else:
            self.hud_items = [
                f"Selected Dim: {self.selected_dim + 1}",
                f"Drive Freq: {self.r_drive[self.selected_dim]:.2f} Hz",
                f"Target Freq: {self.f_target[self.selected_dim]:.2f} Hz",
                f"Resonance: {self.resonance_levels[self.selected_dim]:.2f}",
                f"Speed: {np.linalg.norm(self.velocity):.2f} u/s",
                f"Vol: {int(master_volume * 100)}%",
                f"Integrity: {self.resonance_integrity:.2f}",
                f"Crystals: {self.crystals_collected}",
                f"Status: {'Landed' if self.landed_mode else 'In Flight'}",
                f"Power: {np.mean(self.resonance_power):.2f}",
                f"Verbosity: {self.verbose_mode}",
                f"Rotation: {self.view_rotation:.2f}"
            ]
            if self.landed_mode:
                self.hud_items += [f"Cursor Pos: {self.cursor_pos.round(2)}", f"Crystals Left: {self.crystal_count - len(self.locked_crystals)}"]

    def speak_hud_item(self):
        item = self.hud_items[self.hud_index]
        speak_with_cooldown(item)
        if self.verbose_mode > 1:
            speak_with_cooldown("High verbosity detail: Explore the golden spiral for harmony.")

    def update_starmap_items(self):
        self.starmap_items = []
        if self.locked_target is not None:
            self.starmap_items.append("Unlock target")
        # Add stars to starmap for more content
        for i, body in enumerate(stars):
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi  # Simplified
                self.starmap_items.append(f"Star {i+1} at dist {dist:.1f}, angle {angle:.1f} degrees (unlandable)")
        for i, body in enumerate(planets):
            dist = np.linalg.norm(self.position - body['pos'])  # Updated for dict pos
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi  # Simplified
                self.starmap_items.append(f"Planet {i+1} at dist {dist:.1f}, angle {angle:.1f} degrees")
        # Add nebulae to starmap for more procedural content
        for i, body in enumerate(nebulae):
            dist = np.linalg.norm(self.position - body['pos'])
            if dist < SCANNER_RANGE:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi  # Simplified
                self.starmap_items.append(f"Nebula {i+1} at dist {dist:.1f}, angle {angle:.1f} degrees (unlandable)")
        for i, rift in enumerate(self.rifts):
            if rift['type'] in ['boost', 'crystal']:  # Only beneficial rifts
                dist = np.linalg.norm(self.position - rift['pos'])
                if dist < SCANNER_RANGE:
                    projected_pos = project_to_2d(rift['pos'] - self.position, self.view_rotation)
                    angle = np.arctan2(projected_pos[1], projected_pos[0]) * 180 / np.pi  # Simplified
                    self.starmap_items.append(f"Rift {i+1} ({rift['type']}) at dist {dist:.1f}, angle {angle:.1f} degrees")
        if not self.starmap_items:
            self.starmap_items.append("No objects in scanner range.")

    def speak_starmap_item(self):
        item = self.starmap_items[self.starmap_index]
        speak_with_cooldown(item)

    def lock_on_starmap_item(self):
        if self.starmap_items[self.starmap_index] == "Unlock target":
            self.locked_target = None
            self.locked_is_rift = False
            if self.lock_sound:
                if self.lock_sound in active_sound_effects:
                    active_sound_effects.remove(self.lock_sound)
                self.lock_sound = None
            speak_with_cooldown("Target unlocked.")
            return
        # Parse item to find pos
        item = self.starmap_items[self.starmap_index]
        if "Star" in item:
            idx = int(item.split(" ")[1]) - 1
            target_pos = stars[idx]['pos']
            self.locked_is_rift = False
        elif "Planet" in item:
            idx = int(item.split(" ")[1]) - 1
            target_pos = planets[idx]['pos']  # Updated for dict
            self.locked_is_rift = False
        elif "Nebula" in item:
            idx = int(item.split(" ")[1]) - 1
            target_pos = nebulae[idx]['pos']
            self.locked_is_rift = False
        elif "Rift" in item:
            idx = int(item.split(" ")[1]) - 1
            target_pos = self.rifts[idx]['pos']
            self.locked_is_rift = True
        else:
            return
        self.locked_target = target_pos
        self.lock_sound = SoundEffect(beep_waveform, loop=True, volume=beep_volume)
        active_sound_effects.append(self.lock_sound)
        speak_with_cooldown(f"Locked on to {item.split(' at')[0]}.")

    def scan_nearest_crystal(self):
        if not self.crystal_positions:
            return
        dists = [np.linalg.norm(self.cursor_pos - pos) if idx not in self.locked_crystals else float('inf') for idx, pos in enumerate(self.crystal_positions)]
        nearest = np.argmin(dists)
        if dists[nearest] == float('inf'):
            speak_with_cooldown("No more crystals to scan on this planet.")
            return
        # Compute resonance against crystal
        temp_res = np.zeros(N_DIMENSIONS)
        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - self.crystal_freqs[nearest][i]
            temp_res[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
        if np.mean(temp_res) > AUTO_SNAP_THRESHOLD:
            for i in range(N_DIMENSIONS):
                self.r_drive[i] = self.crystal_freqs[nearest][i]
            speak_with_cooldown("Snapped to crystal frequencies—ready to collect.")
        freq = self.crystal_freqs[nearest][self.selected_dim]
        dx, dy = self.crystal_positions[nearest] - self.cursor_pos
        direction = ""
        if dy > 0: direction += "north "
        elif dy < 0: direction += "south "
        if dx > 0: direction += "east"
        elif dx < 0: direction += "west"
        speak_with_cooldown(f"Nearest crystal {dists[nearest]:.1f} units {direction}. Target freq in dim {self.selected_dim+1}: {freq:.2f} Hz.")
        angle = np.arctan2(dy, dx)
        pan = np.sin(angle)
        active_sound_effects.append(SoundEffect(beep_waveform, pan=pan, volume=beep_volume))

    def collect_crystal(self):
        dists = [np.linalg.norm(self.cursor_pos - pos) for pos in self.crystal_positions]
        nearest = np.argmin(dists)
        if dists[nearest] > 1 or nearest in self.locked_crystals:
            speak_with_cooldown("No collectable crystal nearby.")
            return
        # Use crystal freq as target for resonance check
        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - self.crystal_freqs[nearest][i]
            self.resonance_levels[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
        if np.mean(self.resonance_levels) > CRYSTAL_COLLECTION_THRESHOLD:
            self.locked_crystals.add(nearest)
            self.crystals_collected += 1
            speak_with_cooldown("Crystal collected. Harmony increases.")
            if random.random() < 0.2:
                speak_with_cooldown("Ancient echo: The spiral binds all dimensions in golden eternity.")
            if len(self.locked_crystals) == self.crystal_count:
                speak_with_cooldown("All crystals collected. Access upgrade menu with U.")
            if self.crystals_collected >= ASCENSION_CRYSTAL_THRESHOLD:
                self.ascend()
            self.approaching_lock_announced = False  # Reset after collection
        else:
            speak_with_cooldown("Resonance too low to collect. Tune to crystal frequencies.")

    def ascend(self):
        speak_with_cooldown("Ascension achieved! Warping to harmonious new universe.")
        self.position = np.zeros(N_DIMENSIONS)
        self.activate_golden_harmony()
        global celestial_bodies, planets, stars
        stars = generate_celestial(N_STARS, 'star')
        planets = []
        for star in stars:
            for _ in range(N_PLANETS_PER_STAR):
                pos = star['pos'] + np.random.uniform(-ORBIT_RADIUS, ORBIT_RADIUS, N_DIMENSIONS)
                freq = random.uniform(*FREQUENCY_RANGE)
                planets.append({'pos': pos, 'freq': freq, 'type': 'planet'})
        nebulae = generate_celestial(N_NEBULAE, 'nebula')
        celestial_bodies = stars + planets + nebulae

    def apply_upgrade(self):
        upgrade = self.upgrades[self.hud_index]
        if self.crystals_collected >= upgrade['cost']:
            upgrade['effect']()
            self.crystals_collected -= upgrade['cost']
            speak_with_cooldown(f"{upgrade['name']} upgraded. Cost: {upgrade['cost']} crystals.")
        else:
            speak_with_cooldown("Insufficient crystals.")

    def update(self, dt, celestial_bodies):
        global last_beep_time, simulation_time, active_sound_effects
        if self.hud_mode or self.upgrade_mode or self.starmap_mode:
            return

        if self.landed_mode:
            self.velocity = np.zeros(N_DIMENSIONS)
            shift = 10 * dt if self.planet_biome == 'dissonant' else 1 * dt
            self.f_target = [f + random.uniform(-shift, shift) for f in self.f_target]
            self.f_target = [max(FREQUENCY_RANGE[0], min(FREQUENCY_RANGE[1], f)) for f in self.f_target]
            for i in range(N_DIMENSIONS):
                delta_f = self.r_drive[i] - self.f_target[i]
                self.resonance_levels[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
            return

        env_influence = np.zeros(N_DIMENSIONS)
        for body in celestial_bodies:
            dists = np.abs(self.position - body['pos'])  # Updated for dict pos
            close_dims = dists < INTERACTION_DISTANCE
            if np.any(close_dims):
                body_freq = body['freq']  # Now fixed per body
                for d in range(N_DIMENSIONS):
                    if close_dims[d]:
                        env_influence[d] += (INTERACTION_DISTANCE - dists[d]) / INTERACTION_DISTANCE * body_freq * PHI**d
        self.f_target = [self.base_f_target[i] + env_influence[i] for i in range(N_DIMENSIONS)]  # New: Stable base + influence
        self.f_target = [max(FREQUENCY_RANGE[0], min(FREQUENCY_RANGE[1], f)) for f in self.f_target]

        if self.locked_target is not None:
            dir_vec = self.locked_target - self.position
            norm = np.linalg.norm(dir_vec)
            if norm < 1.0:
                for i in range(N_DIMENSIONS):
                    self.r_drive[i] = self.f_target[i]  # Reset to f_target to stop the ship
                self.locked_target = None
                self.locked_is_rift = False
                if self.lock_sound:
                    if self.lock_sound in active_sound_effects:
                        active_sound_effects.remove(self.lock_sound)
                    self.lock_sound = None
                speak_with_cooldown("Target reached.")
            else:
                for i in range(N_DIMENSIONS):
                    dir_i = dir_vec[i]
                    if abs(dir_i) > 0.1:
                        if self.locked_is_rift:
                            res_des = 0.99  # Maintain high resonance for rifts
                        else:
                            res_des = min(0.99, abs(dir_i) / SLOWDOWN_DIST)
                        delta_mag = self.resonance_width * np.sqrt(1 / res_des - 1) if res_des > 0 else 0
                        delta_f = np.sign(dir_i) * delta_mag
                        target_drive = self.f_target[i] + delta_f
                        self.r_drive[i] += (target_drive - self.r_drive[i]) * 0.1
                    else:
                        target_drive = self.f_target[i]
                        self.r_drive[i] += (target_drive - self.r_drive[i]) * 0.1
                # Lock sound update
                projected_pos = project_to_2d(dir_vec, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2)
                self.lock_sound.pan = np.sin(angle)
                misalignment = abs(angle)
                self.lock_sound.pitch = 1.0 + misalignment / 180.0  # Higher pitch if off-center
                self.lock_sound.waveform = beep_waveform * self.lock_sound.pitch  # Update waveform
                self.lock_sound.volume = beep_volume  # Apply adjustable volume

        for i in range(N_DIMENSIONS):
            delta_f = self.r_drive[i] - self.f_target[i]
            self.resonance_levels[i] = 1 / (1 + (delta_f / self.resonance_width)**2)
            if self.resonance_levels[i] > POWER_BUILD_THRESHOLD:
                self.resonance_power[i] += dt
            else:
                self.resonance_power[i] = 0
            boost = 1 + (self.resonance_power[i] / POWER_BUILD_TIME) * PHI
            self.velocity[i] = self.max_velocity * self.resonance_levels[i] * np.sign(delta_f) * boost

        avg_res = np.mean(self.resonance_levels)
        if avg_res < DISSONANCE_THRESHOLD:
            self.dissonance_timer += dt
            if self.dissonance_timer > DISSONANCE_DURATION:
                self.velocity += np.random.uniform(-1, 1, N_DIMENSIONS) * 0.5
                speak_with_cooldown("Dissonance detected—retune!")
                self.dissonance_timer = 0.0
        else:
            self.dissonance_timer = 0.0

        for i in range(N_DIMENSIONS):
            change = abs(self.resonance_levels[i] - self.prev_resonance_levels[i])
            if self.verbose_mode > 0 and change > 0.1:
                speak_with_cooldown(f"Alert: Resonance in dim {i+1} now {self.resonance_levels[i]:.2f}.")
            if self.verbose_mode == 2 and simulation_time % 5 < DT:
                hud_status = f"Selected Dim: {self.selected_dim + 1}. Drive Freq: {self.r_drive[self.selected_dim]:.2f} Hz. Target Freq: {self.f_target[self.selected_dim]:.2f} Hz. Resonance: {self.resonance_levels[self.selected_dim]:.2f}. Speed: {np.linalg.norm(self.velocity):.2f} u/s. Volume: {int(master_volume * 100)} percent. Integrity: {self.resonance_integrity:.2f}. Crystals: {self.crystals_collected}. Status: {'Landed' if self.landed_mode else 'In Flight'}."
                speak_with_cooldown(hud_status)
        self.prev_resonance_levels = self.resonance_levels.copy()

        if random.random() < 0.001 and avg_res > 0.9:
            rift_pos = self.position + np.random.uniform(-15, 15, N_DIMENSIONS)  # Tweaked for farther spawn (up to ~33.5 units)
            rift_pos[3] = rift_pos[0] * PHI
            rift_pos[4] = rift_pos[1] * PHI
            rift_type = random.choice(['boost', 'crystal', 'hazard'])
            hum_waveform = rift_hum_waveform.copy()  # Copy to avoid modification
            sound = SoundEffect(hum_waveform, loop=True, volume=0.0)  # Start at 0, update later
            active_sound_effects.append(sound)
            self.rifts.append({'pos': rift_pos, 'timer': RIFT_FADE_TIME, 'type': rift_type, 'sound': sound})
            projected_pos = project_to_2d(rift_pos - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
            dir_str = "left" if angle < 0 else "right"
            speak_with_cooldown(f"{rift_type.capitalize()} dimensional rift detected at {abs(angle):.1f} degrees {dir_str}.")
            # Play beep for rift detection
            pan = np.sin(angle * np.pi / 180)
            active_sound_effects.append(SoundEffect(beep_waveform, pan=pan, volume=beep_volume))

        to_remove = []
        for i, rift in enumerate(self.rifts):
            rift['timer'] -= dt
            if rift['timer'] <= 0:
                to_remove.append(i)
                speak_with_cooldown("Rift fading into the void.")
                continue
            if avg_res > 0.9:
                rift['timer'] += dt * PHI
            projected_pos = project_to_2d(rift['pos'] - self.position, self.view_rotation)
            angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2)
            rift['sound'].pan = np.sin(angle)
            dist = np.linalg.norm(self.position - rift['pos'])
            rift['sound'].volume = max(0, effect_volume * (1 - dist / RIFT_MAX_DIST)) * avg_res  # Use effect_volume
            if np.linalg.norm(self.position - rift['pos']) < RIFT_ALIGNMENT_TOLERANCE:
                if avg_res > RIFT_ENTRY_RES_THRESHOLD:
                    self.position += np.random.uniform(-20, 20, N_DIMENSIONS) * PHI
                    speak_with_cooldown(f"Entering {rift['type']} rift—golden warp activated.")
                    if rift['type'] == 'crystal':
                        self.crystals_collected += 1
                    elif rift['type'] == 'hazard':
                        self.resonance_integrity -= 0.1
                    to_remove.append(i)
                else:
                    self.velocity += np.random.uniform(-1, 1, N_DIMENSIONS) * 0.5
                    speak_with_cooldown("Dissonance prevents rift entry.")
        for i in sorted(to_remove, reverse=True):
            del self.rifts[i]

        self.position += self.velocity * dt
        self.position = (self.position + 100) % 200 - 100

        self.nearest_body = None
        min_dist = float('inf')
        near_any = False
        for body in celestial_bodies:
            dist = np.linalg.norm(self.position - body['pos'])  # Updated for dict pos
            if dist < INTERACTION_DISTANCE:
                near_any = True
                if dist < min_dist:
                    min_dist = dist
                    self.nearest_body = body
        if near_any and not self.near_object:
            self.near_object = True
            speak_with_cooldown("Approaching celestial object. Resonance influenced.")
        elif not near_any and self.near_object:
            self.near_object = False
            speak_with_cooldown("Leaving object vicinity.")

        delta_rotation = self.view_rotation - self.prev_view_rotation
        if abs(delta_rotation) > 0.01:
            for body in celestial_bodies:
                projected_pos = project_to_2d(body['pos'] - self.position, self.view_rotation)  # Updated for dict pos
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2) * 180 / np.pi
                if abs(angle) < VIEW_LANDMARK_THRESHOLD:
                    speak_with_cooldown(f"Object in view at {angle:.1f} degrees.")
        self.prev_view_rotation = self.view_rotation

        if self.near_object and simulation_time - last_beep_time > 1.0:
            if self.nearest_body is not None:
                projected_pos = project_to_2d(self.nearest_body['pos'] - self.position, self.view_rotation)
                angle = np.arctan2(projected_pos[1] - SCREEN_HEIGHT/2, projected_pos[0] - SCREEN_WIDTH/2)
                pan = np.sin(angle)
                active_sound_effects.append(SoundEffect(beep_waveform, pan=pan, volume=beep_volume))
            last_beep_time = simulation_time

        if self.landing_timer > 0:
            self.landing_timer -= dt
            if self.landing_timer <= 0:
                if np.mean(self.resonance_levels) > LANDING_THRESHOLD and self.nearest_body and self.nearest_body['type'] == 'planet':
                    self.landed_mode = True
                    self.landed_planet = self.nearest_body['pos']
                    speak_with_cooldown("Landing successful. Explore the planet.")
                    self.generate_crystals()
                else:
                    if self.nearest_body and self.nearest_body['type'] != 'planet':
                        speak_with_cooldown("Cannot land on this object.")
                    else:
                        self.resonance_integrity -= 0.1
                        speak_with_cooldown("Landing failed due to dissonance. Integrity reduced.")
                        if self.resonance_integrity < 0.5:
                            speak_with_cooldown("Warning: Low integrity—repair needed.")

# PHI-based procedural generation of celestial bodies
def generate_celestial(n, body_type='star'):
    bodies = []
    for i in range(n):
        theta = i * 2 * np.pi * PHI  # Golden angle for distribution
        r = FIB_SEQ[i % len(FIB_SEQ)] * SCALE_FACTOR
        pos = np.zeros(N_DIMENSIONS)
        pos[0] = r * np.cos(theta)
        pos[1] = r * np.sin(theta)
        for d in range(2, N_DIMENSIONS):
            pos[d] = pos[d-2] * PHI + random.uniform(-10, 10)  # Correlated higher dims
        freq = random.uniform(*FREQUENCY_RANGE)  # New: Fixed freq per body
        bodies.append({'pos': pos, 'freq': freq, 'type': body_type})
    return bodies

stars = generate_celestial(N_STARS, 'star')
planets = []
for star in stars:
    for _ in range(N_PLANETS_PER_STAR):
        pos = star['pos'] + np.random.uniform(-ORBIT_RADIUS, ORBIT_RADIUS, N_DIMENSIONS)
        freq = random.uniform(*FREQUENCY_RANGE)
        planets.append({'pos': pos, 'freq': freq, 'type': 'planet'})
nebulae = generate_celestial(N_NEBULAE, 'nebula')
celestial_bodies = stars + planets + nebulae  # Include nebulae in celestial bodies

# Precompute waveforms with PHI (increased amplitudes for audibility)
beep_duration = 0.1
beep_frequency = 440
beep_samples = int(beep_duration * SAMPLE_RATE)
beep_waveform = 0.1 * np.sin(2 * np.pi * beep_frequency * np.linspace(0, beep_duration, beep_samples))  # Increased amplitude

click_duration = 0.05
click_freq = 100 * PHI  # PHI tie-in
click_waveform = 0.2 * np.sin(2 * np.pi * click_freq * np.linspace(0, click_duration, int(click_duration * SAMPLE_RATE), endpoint=False))  # Increased amplitude

rotation_duration = ROTATION_SOUND_DURATION
rotation_freq = 200 * PHI
rotation_waveform = 0.1 * np.sin(2 * np.pi * rotation_freq * np.linspace(0, rotation_duration, int(rotation_duration * SAMPLE_RATE)))  # Increased amplitude

chord_duration = 0.5  # For power chord
chord_samples = int(chord_duration * SAMPLE_RATE)
t_chord = np.linspace(0, chord_duration, chord_samples)
chord_waveform = 0.1 * (np.sin(2 * np.pi * 440 * t_chord) + np.sin(2 * np.pi * 440 * PHI * t_chord) + np.sin(2 * np.pi * 440 * PHI**2 * t_chord))  # Increased amplitude

# Rift hum - refined for pleasing sound
rift_hum_duration = 1.0
rift_hum_base_freq = 220.0  # Lower for hum
t_rift = np.linspace(0, rift_hum_duration, int(rift_hum_duration * SAMPLE_RATE))
rift_hum_waveform = 0.02 * (np.sin(2 * np.pi * rift_hum_base_freq * t_rift) +
                             0.5 * np.sin(2 * np.pi * rift_hum_base_freq * PHI * t_rift) +
                             0.25 * np.sin(2 * np.pi * rift_hum_base_freq * PHI**2 * t_rift))  # Increased amplitude

# Audio setup
audio_time = 0.0
master_volume = 0.2
beep_volume = 0.3  # For beeps (planets, rifts, locks, crystals)
effect_volume = 0.2  # For clicks, rotations, chords, hums
drive_volume = 0.05  # For drive signals
active_sound_effects = []

def audio_callback(outdata, frames, time, status):
    global audio_time
    t = (np.arange(frames) / SAMPLE_RATE) + audio_time
    audio_time += frames / SAMPLE_RATE

    signals = np.zeros((N_DIMENSIONS, frames))
    for i in range(N_DIMENSIONS):
        base_freq = ship.r_drive[i] / 2  # Lowered by one octave
        for k in range(3):
            signals[i] += (drive_volume / (k+1)) * np.sin(2 * np.pi * (base_freq * PHI**k) * t)  # Adjustable amplitude
    left_signal = np.mean(signals[:3], axis=0) + signals[3] * 0.7 + signals[4] * 0.3
    right_signal = np.mean(signals[:3], axis=0) + signals[3] * 0.3 + signals[4] * 0.7

    modulation = 0.5 + 0.5 * np.sin(2 * np.pi * 0.1 * PHI * t)
    ambient_signal = 0.01 * modulation * np.sin(2 * np.pi * 30 * PHI * t)  # Increased amplitude
    left_signal += ambient_signal
    right_signal += ambient_signal

    if ship.rotating_left or ship.rotating_right:
        pan = -1.0 if ship.rotating_left else 1.0
        active_sound_effects.append(SoundEffect(rotation_waveform, pan=pan, volume=effect_volume))

    if any(ship.resonance_power[i] > POWER_BUILD_TIME - 1 for i in range(N_DIMENSIONS)):
        active_sound_effects.append(SoundEffect(chord_waveform, pan=0.0, volume=effect_volume))

    for effect in active_sound_effects[:]:
        if effect.position < len(effect.waveform):
            segment = effect.waveform[effect.position : effect.position + frames]
            if len(segment) < frames:
                segment = np.pad(segment, (0, frames - len(segment)), 'constant')
            left_volume = np.sqrt((1 - effect.pan) / 2) * effect.volume
            right_volume = np.sqrt((1 + effect.pan) / 2) * effect.volume
            left_signal += segment * left_volume
            right_signal += segment * right_volume
            effect.position += frames
        if effect.position >= len(effect.waveform):
            if effect.loop:
                effect.position = 0
            else:
                active_sound_effects.remove(effect)

    left_signal *= master_volume
    right_signal *= master_volume
    signal = np.stack((left_signal, right_signal), axis=1)
    signal = np.clip(signal, -1.0, 1.0)
    outdata[:] = signal

stream = sd.OutputStream(callback=audio_callback, channels=2, samplerate=SAMPLE_RATE)
stream.start()

# Main loop setup
ship = Ship()
simulation_time = 0.0
last_beep_time = -1.0
next_click_time = 0.0
last_spoken = {}

def speak_with_cooldown(msg):
    global simulation_time, last_spoken
    if msg not in last_spoken or simulation_time - last_spoken[msg] > SPEECH_COOLDOWN:
        tolk.speak(msg)
        last_spoken[msg] = simulation_time

def project_to_2d(pos, rotation):
    cos_r = np.cos(rotation)
    sin_r = np.sin(rotation)
    x = pos[0] * cos_r + pos[3] * sin_r
    y = pos[1] * cos_r + pos[4] * sin_r
    screen_x = (x + 100) / 200 * SCREEN_WIDTH
    screen_y = (y + 100) / 200 * SCREEN_HEIGHT
    return (int(screen_x), int(screen_y))

def update_loop():
    global simulation_time, last_beep_time, next_click_time, master_volume
    dt = clock.tick(FPS) / 1000.0
    simulation_time += dt

    events = pygame.event.get()
    for event in events:
        if event.type == pygame.QUIT or (event.type == pygame.KEYDOWN and event.key == pygame.K_ESCAPE):
            speak_with_cooldown("Shutting down.")
            pygame.quit()
            stream.stop()
            stream.close()
            tolk.unload()
            exit()

    keys = pygame.key.get_pressed()
    ship.handle_input(keys, events)
    ship.update(dt, celestial_bodies)

    avg_resonance = np.mean(ship.resonance_levels)
    click_interval = max(0.1, 1.0 - avg_resonance)
    current_time = pygame.time.get_ticks() / 1000.0
    if current_time > next_click_time:
        active_sound_effects.append(SoundEffect(click_waveform, pan=0.0, volume=effect_volume))
        next_click_time = current_time + click_interval

    bg_color = (0, 0, 0) if not ship.high_contrast else (255, 255, 255)
    text_color = (255, 255, 255) if not ship.high_contrast else (0, 0, 0)
    screen.fill(bg_color)

    for body in stars:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)  # Updated for dict pos
        hue = (((body['pos'][3] + body['pos'][4]) / 200 * 360) % 360 + 360) % 360  # Updated for dict
        color = pygame.Color(0)
        color.hsva = (hue, 100, 100, 100) if not ship.high_contrast else (0, 0, 0, 100)
        pygame.draw.circle(screen, color, pos_2d, 2)
    for body in planets:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)  # Updated for dict pos
        hue = (((body['pos'][3] + body['pos'][4]) / 200 * 360) % 360 + 360) % 360  # Updated for dict
        color = pygame.Color(0)
        color.hsva = (hue, 100, 100, 100) if not ship.high_contrast else (0, 0, 0, 100)
        pygame.draw.circle(screen, color, pos_2d, PLANET_RADIUS)
    # New: Visual for nebulae
    for body in nebulae:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)
        hue = (((body['pos'][3] + body['pos'][4]) / 200 * 360) % 360 + 360) % 360
        color = pygame.Color(0)
        color.hsva = (hue, 50, 100, 50) if not ship.high_contrast else (0, 0, 0, 50)
        pygame.draw.circle(screen, color, pos_2d, 15)  # Larger for nebulae

    # Rifts visual
    for rift in ship.rifts:
        pos_2d = project_to_2d(rift['pos'], ship.view_rotation)
        pygame.draw.circle(screen, (255, 0, 255), pos_2d, 5)

    if ship.landed_mode:
        # Simple planet grid visual
        for pos in ship.crystal_positions:
            screen_x = int(SCREEN_WIDTH / 2 + pos[0] * 20)
            screen_y = int(SCREEN_HEIGHT / 2 + pos[1] * 20)
            pygame.draw.circle(screen, (0, 255, 0), (screen_x, screen_y), 5)
        cursor_x = int(SCREEN_WIDTH / 2 + ship.cursor_pos[0] * 20)
        cursor_y = int(SCREEN_HEIGHT / 2 + ship.cursor_pos[1] * 20)
        pygame.draw.line(screen, (255, 0, 0), (cursor_x - 5, cursor_y), (cursor_x + 5, cursor_y))
        pygame.draw.line(screen, (255, 0, 0), (cursor_x, cursor_y - 5), (cursor_x, cursor_y + 5))
    else:
        # Ship spiral
        max_r = 20
        theta_max = 6 * np.pi
        a = max_r / (PHI ** (2 * theta_max / np.pi))
        theta = np.linspace(0, theta_max, 100)
        r = a * PHI ** (2 * theta / np.pi)
        x = r * np.cos(theta + ship.heading)
        y = r * np.sin(theta + ship.heading)
        spiral_points = np.tile(ship.position, (100, 1))
        spiral_points[:, 0] += x
        spiral_points[:, 1] += y
        screen_points = [project_to_2d(p, ship.view_rotation) for p in spiral_points]
        pygame.draw.lines(screen, (255, 255, 0) if not ship.high_contrast else (0, 0, 255), False, screen_points, 2)

        # Engines
        theta_engines = np.array([theta_max - i * (np.pi / PHI) for i in range(3)])
        r_engines = a * PHI ** (2 * theta_engines / np.pi)
        x_engines = r_engines * np.cos(theta_engines + ship.heading)
        y_engines = r_engines * np.sin(theta_engines + ship.heading)
        engine_points = np.tile(ship.position, (3, 1))
        engine_points[:, 0] += x_engines
        engine_points[:, 1] += y_engines
        screen_engine_points = [project_to_2d(p, ship.view_rotation) for p in engine_points]
        for ep in screen_engine_points:
            pygame.draw.circle(screen, (255, 0, 0) if not ship.high_contrast else (0, 255, 0), ep, 5)

    if ship.hud_mode or ship.upgrade_mode or ship.starmap_mode:
        items = ship.hud_items if (ship.hud_mode or ship.upgrade_mode) else ship.starmap_items
        for i, item in enumerate(items):
            color = (0, 255, 0) if i == (ship.hud_index if (ship.hud_mode or ship.upgrade_mode) else ship.starmap_index) else text_color
            text = font.render(item, True, color)
            screen.blit(text, (10, 10 + i * (ship.hud_text_size + 5)))
    else:
        ship.update_hud_items()
        hud_lines = ship.hud_items
        for i, line in enumerate(hud_lines):
            text = font.render(line, True, text_color)
            screen.blit(text, (10, 10 + i * (ship.hud_text_size + 5)))

    pygame.display.flip()

async def main():
    while True:
        update_loop()
        await asyncio.sleep(1.0 / FPS)

if __name__ == "__main__":
    asyncio.run(main())