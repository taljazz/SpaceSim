"""
Main entry point for the Golden Spiral Spaceship Simulator.

This module initializes all game systems and runs the main game loop.
"""

import asyncio
import pygame
import numpy as np
import configparser
from cytolk import tolk

from constants import *
from audio_system import AudioSystem, SoundEffect
from celestial import generate_all_celestial_bodies, generate_complete_universe
from ship import Ship
from utils import project_to_2d


# Load config if exists
config = configparser.ConfigParser()
config.read('config.ini')

# Initialize Pygame and Tolk for screen and speech
pygame.init()
tolk.load()
tolk.speak("Welcome to the Golden Spiral Spaceship Simulator. Resonance propulsion engaged. Harmonize with the universe.")

# Set up display
screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
pygame.display.set_caption("Golden Spiral Spaceship Simulator")
clock = pygame.time.Clock()
font = pygame.font.SysFont(None, HUD_TEXT_SIZE_BASE)

# Initialize audio system
audio_system = AudioSystem(config)

# Generate complete Atlantean universe
stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids = generate_complete_universe()

# Initialize ship
ship = Ship(config, audio_system)
# Store celestial body references in ship for save/load
ship.stars = stars
ship.planets = planets
ship.nebulae = nebulae
audio_system.set_ship(ship)  # Set ship reference for audio callback

# Start audio stream
audio_system.start()

# Game state
next_click_time = 0.0


def update_loop():
    """Main game update loop."""
    global next_click_time, stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids

    dt = clock.tick(FPS) / 1000.0
    ship.simulation_time += dt

    # Handle events
    events = pygame.event.get()
    for event in events:
        if event.type == pygame.QUIT or (event.type == pygame.KEYDOWN and event.key == pygame.K_ESCAPE):
            ship.speak("Shutting down.")
            # Save config before quitting
            with open('config.ini', 'w') as configfile:
                if 'Audio' not in config:
                    config['Audio'] = {}
                config['Audio']['master_volume'] = str(audio_system.master_volume)
                config['Audio']['beep_volume'] = str(audio_system.beep_volume)
                config['Audio']['effect_volume'] = str(audio_system.effect_volume)
                config['Audio']['drive_volume'] = str(audio_system.drive_volume)
                if 'Settings' not in config:
                    config['Settings'] = {}
                config['Settings']['verbose_mode'] = str(ship.verbose_mode)
                config['Settings']['high_contrast'] = str(ship.high_contrast)
                config['Settings']['hud_text_size'] = str(ship.hud_text_size)
                config['Settings']['autosave_enabled'] = str(ship.autosave_enabled)
                config['Settings']['ambient_sounds_enabled'] = str(ship.ambient_sounds_enabled)
                config['Settings']['nebula_dissonance_enabled'] = str(ship.nebula_dissonance_enabled)
                config.write(configfile)
            pygame.quit()
            audio_system.stop()
            tolk.unload()
            exit()

    # Get keys and update ship
    keys = pygame.key.get_pressed()
    ship.handle_input(keys, events, stars, planets, nebulae)
    ship.update(dt, celestial_bodies, keys, temples, ley_lines, pyramids)

    # Check if universe needs regeneration (after ascension or game load)
    if ship.needs_universe_regeneration:
        # Check if this is a load (ship has celestial data) vs regeneration (needs new data)
        if ship.stars and len(ship.stars) > 0:
            # Load from save - use ship's stored celestial bodies
            stars = ship.stars
            planets = ship.planets
            nebulae = ship.nebulae
            celestial_bodies = stars + planets + nebulae
            # Note: temples/ley_lines/pyramids are regenerated (not saved)
            # This is intentional - they're procedurally generated from constants
        else:
            # Ascension - generate new universe
            stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids = generate_complete_universe()
            # Update ship's references
            ship.stars = stars
            ship.planets = planets
            ship.nebulae = nebulae
        ship.needs_universe_regeneration = False

    # Add periodic click sound based on resonance (only when not landed)
    if not ship.landed_mode:
        avg_resonance = np.mean(ship.resonance_levels)
        click_interval = max(0.1, 1.0 - avg_resonance)
        current_time = pygame.time.get_ticks() / 1000.0
        if current_time > next_click_time:
            audio_system.active_sound_effects.append(
                SoundEffect(audio_system.click_waveform, pan=0.0, volume=audio_system.effect_volume)
            )
            next_click_time = current_time + click_interval

    # Render screen
    bg_color = (0, 0, 0) if not ship.high_contrast else (255, 255, 255)
    text_color = (255, 255, 255) if not ship.high_contrast else (0, 0, 0)
    screen.fill(bg_color)

    # Animation time for dynamic effects
    anim_time = pygame.time.get_ticks() / 1000.0

    # Draw stars with twinkling effect
    for idx, body in enumerate(stars):
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)
        if ship.high_contrast:
            color = (0, 0, 0)
        else:
            stellar_type = body.get('stellar_type', 'main_sequence')
            base_color = STELLAR_TYPES[stellar_type]['color']
            # Twinkle effect - each star has unique phase based on index
            twinkle = 0.7 + 0.3 * np.sin(anim_time * 3 + idx * 0.7)
            color = tuple(int(c * twinkle) for c in base_color)
        # Pulsing size for red giants
        size = 2
        if body.get('stellar_type') == 'red_giant':
            size = int(3 + np.sin(anim_time * 0.5 + idx) * 1.5)
        elif body.get('stellar_type') == 'white_dwarf':
            size = 1  # Small but bright
        pygame.draw.circle(screen, color, pos_2d, size)

    # Draw planets
    for body in planets:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)
        hue = (((body['pos'][3] + body['pos'][4]) / 200 * 360) % 360 + 360) % 360
        color = pygame.Color(0)
        color.hsva = (hue, 100, 100, 100) if not ship.high_contrast else (0, 0, 0, 100)
        # Apply size multiplier from exoplanet type
        size_mult = body.get('size_mult', 1.0)
        radius = int(PLANET_RADIUS * size_mult)
        pygame.draw.circle(screen, color, pos_2d, radius)

    # Draw nebulae
    for body in nebulae:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)
        if ship.high_contrast:
            color = (128, 128, 128, 128)  # Gray with transparency
        else:
            nebula_type = body.get('nebula_type', 'emission')
            color = NEBULA_TYPES[nebula_type]['color']
        pygame.draw.circle(screen, color, pos_2d, 15)

    # Draw rifts with pulsing dimensional effect
    for idx, rift in enumerate(ship.rifts):
        pos_2d = project_to_2d(rift['pos'], ship.view_rotation)
        # Pulsing size and color
        pulse = 0.5 + 0.5 * np.sin(anim_time * 4 + idx)
        size = int(5 + 3 * pulse)
        # Shifting purple/cyan colors for dimensional effect
        r = int(200 + 55 * np.sin(anim_time * 3))
        g = int(50 + 50 * np.sin(anim_time * 2 + 1))
        b = int(200 + 55 * np.cos(anim_time * 3))
        pygame.draw.circle(screen, (r, g, b), pos_2d, size)
        # Inner glow
        pygame.draw.circle(screen, (255, 255, 255), pos_2d, max(2, size // 2))

    # Draw temples (golden triangles) with pulsing glow
    for idx, temple in enumerate(temples):
        pos_2d = project_to_2d(temple['pos'], ship.view_rotation)
        pulse = 0.7 + 0.3 * np.sin(anim_time * 2 + idx * 0.3)

        if temple['temple_type'] == 'master':
            # Halls of Amenti - large golden triangle with radiant glow
            base_color = (255, 215, 0) if not ship.high_contrast else (0, 0, 0)
            size = int(15 + 3 * np.sin(anim_time * 1.5))
            # Draw outer glow rings
            for ring in range(3, 0, -1):
                glow_alpha = int(100 / ring)
                glow_color = (255, 215, 0)
                pygame.draw.circle(screen, glow_color, pos_2d, size + ring * 5, 1)
        else:
            # Minor temples - smaller triangles with key collected indicator
            if temple['key_index'] in ship.temple_keys:
                base_color = (0, 255, 128)  # Green if key collected
            else:
                base_color = (255, 200, 100) if not ship.high_contrast else (0, 0, 0)
            size = 8

        color = tuple(int(c * pulse) for c in base_color)

        # Draw triangle
        points = [
            (pos_2d[0], pos_2d[1] - size),  # Top
            (pos_2d[0] - size, pos_2d[1] + size),  # Bottom left
            (pos_2d[0] + size, pos_2d[1] + size)  # Bottom right
        ]
        pygame.draw.polygon(screen, color, points)

        # Draw inner glow for uncollected temples
        if temple['temple_type'] != 'master' and temple['key_index'] not in ship.temple_keys:
            inner_points = [
                (pos_2d[0], pos_2d[1] - size // 2),
                (pos_2d[0] - size // 2, pos_2d[1] + size // 2),
                (pos_2d[0] + size // 2, pos_2d[1] + size // 2)
            ]
            inner_color = tuple(min(255, int(c * 1.3)) for c in color)
            pygame.draw.polygon(screen, inner_color, inner_points)

    # Draw pyramids (golden squares)
    for pyramid in pyramids:
        pos_2d = project_to_2d(pyramid['pos'], ship.view_rotation)
        color = (218, 165, 32) if not ship.high_contrast else (0, 0, 0)  # Golden rod
        size = 10
        rect = pygame.Rect(pos_2d[0] - size, pos_2d[1] - size, size * 2, size * 2)
        pygame.draw.rect(screen, color, rect)

    # Draw ley lines with energy flow effect
    for idx, ley_line in enumerate(ley_lines):
        start_2d = project_to_2d(ley_line['start'], ship.view_rotation)
        end_2d = project_to_2d(ley_line['end'], ship.view_rotation)

        # Pulsing brightness based on time
        pulse = 0.6 + 0.4 * np.sin(anim_time * 2 + idx * 0.5)

        if ley_line.get('amenti_path'):
            base_color = (255, 215, 0)  # Bright gold for Amenti paths
            width = 2
        elif ley_line.get('major'):
            base_color = (200, 180, 0)  # Darker gold for major lines
            width = 2
        else:
            base_color = (150, 130, 0)  # Dim gold for minor lines
            width = 1

        color = tuple(int(c * pulse) for c in base_color)
        pygame.draw.line(screen, color, start_2d, end_2d, width)

        # Draw energy particles flowing along the line (if on this ley line, show more)
        if ship.on_ley_line and ship.current_ley_line is ley_line:
            # More visible energy dots when player is on this ley line
            for i in range(5):
                t = (anim_time * 0.3 + i * 0.2) % 1.0
                particle_x = int(start_2d[0] + (end_2d[0] - start_2d[0]) * t)
                particle_y = int(start_2d[1] + (end_2d[1] - start_2d[1]) * t)
                pygame.draw.circle(screen, (255, 255, 200), (particle_x, particle_y), 3)

    # Draw planet grid if landed
    if ship.landed_mode:
        for pos in ship.crystal_positions:
            screen_x = int(SCREEN_WIDTH / 2 + pos[0] * 20)
            screen_y = int(SCREEN_HEIGHT / 2 + pos[1] * 20)
            pygame.draw.circle(screen, (0, 255, 0), (screen_x, screen_y), 5)
        cursor_x = int(SCREEN_WIDTH / 2 + ship.cursor_pos[0] * 20)
        cursor_y = int(SCREEN_HEIGHT / 2 + ship.cursor_pos[1] * 20)
        pygame.draw.line(screen, (255, 0, 0), (cursor_x - 5, cursor_y), (cursor_x + 5, cursor_y))
        pygame.draw.line(screen, (255, 0, 0), (cursor_x, cursor_y - 5), (cursor_x, cursor_y + 5))
    else:
        # Draw golden spiral for ship visualization
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

        # Draw engine points on spiral with dynamic glow based on velocity
        theta_engines = np.array([theta_max - i * (np.pi / PHI) for i in range(3)])
        r_engines = a * PHI ** (2 * theta_engines / np.pi)
        x_engines = r_engines * np.cos(theta_engines + ship.heading)
        y_engines = r_engines * np.sin(theta_engines + ship.heading)
        engine_points = np.tile(ship.position, (3, 1))
        engine_points[:, 0] += x_engines
        engine_points[:, 1] += y_engines
        screen_engine_points = [project_to_2d(p, ship.view_rotation) for p in engine_points]

        # Engine glow intensity based on velocity
        velocity_mag = np.linalg.norm(ship.velocity)
        glow_intensity = min(1.0, velocity_mag / ship.max_velocity)
        engine_pulse = 0.7 + 0.3 * np.sin(anim_time * 8)  # Fast pulse

        for ep in screen_engine_points:
            # Outer glow based on velocity
            if glow_intensity > 0.1:
                glow_size = int(8 + 6 * glow_intensity * engine_pulse)
                glow_color = (255, int(100 + 100 * (1 - glow_intensity)), 0)
                pygame.draw.circle(screen, glow_color, ep, glow_size)
            # Core engine point
            pygame.draw.circle(screen, (255, 0, 0) if not ship.high_contrast else (0, 255, 0), ep, 5)

        # Draw resonance rings around ship (5 rings for 5 dimensions)
        ship_center = project_to_2d(ship.position, ship.view_rotation)
        for i in range(N_DIMENSIONS):
            res_level = ship.resonance_levels[i]
            ring_radius = 30 + i * 12
            # Ring color based on dimension and resonance
            hue = (i * 72) % 360  # Different hue for each dimension
            # Base brightness 40-100 based on resonance, with pulsing effect
            pulse_factor = 0.7 + 0.3 * np.sin(anim_time * 3 + i)
            brightness = int((40 + 60 * res_level) * pulse_factor)
            brightness = max(10, min(100, brightness))  # Clamp to valid HSVA range
            ring_color = pygame.Color(0)
            ring_color.hsva = (hue, 80, brightness, 100)
            # Ring thickness based on resonance
            thickness = 1 if res_level < 0.5 else (2 if res_level < 0.8 else 3)
            pygame.draw.circle(screen, ring_color, ship_center, int(ring_radius * (0.8 + 0.2 * res_level)), thickness)

        # Draw Merkaba overlay when active (rotating star tetrahedron)
        if ship.merkaba_active:
            merkaba_size = 50
            # Two triangles rotating in opposite directions
            angle1 = anim_time * 0.5  # Slow rotation
            angle2 = -anim_time * 0.5  # Counter-rotation

            # Upward triangle
            tri1_points = []
            for j in range(3):
                a = angle1 + j * (2 * np.pi / 3)
                px = ship_center[0] + merkaba_size * np.cos(a)
                py = ship_center[1] + merkaba_size * np.sin(a)
                tri1_points.append((px, py))

            # Downward triangle (inverted)
            tri2_points = []
            for j in range(3):
                a = angle2 + j * (2 * np.pi / 3) + np.pi / 3  # Offset by 60 degrees
                px = ship_center[0] + merkaba_size * np.cos(a)
                py = ship_center[1] + merkaba_size * np.sin(a)
                tri2_points.append((px, py))

            # Draw with golden/white glow
            merkaba_pulse = 0.7 + 0.3 * np.sin(anim_time * 2)
            merkaba_color = (int(255 * merkaba_pulse), int(215 * merkaba_pulse), int(100 * merkaba_pulse))
            pygame.draw.polygon(screen, merkaba_color, tri1_points, 2)
            pygame.draw.polygon(screen, merkaba_color, tri2_points, 2)

            # Inner star pattern
            for p1 in tri1_points:
                for p2 in tri2_points:
                    pygame.draw.line(screen, (255, 255, 200, 100), p1, p2, 1)

    # Render menu or HUD text
    if ship.hud_mode or ship.upgrade_mode or ship.starmap_mode or ship.rift_selection_mode:
        if ship.rift_selection_mode:
            items = [item['label'] for item in ship.rift_items]
            index = ship.rift_selection_index
        elif ship.starmap_mode:
            items = [item['label'] for item in ship.starmap_items]
            index = ship.starmap_index
        else:
            items = ship.hud_items
            index = ship.hud_index
        for i, item in enumerate(items):
            color = (0, 255, 0) if i == index else text_color
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
    """Async main loop."""
    while True:
        update_loop()
        await asyncio.sleep(1.0 / FPS)


if __name__ == "__main__":
    asyncio.run(main())
