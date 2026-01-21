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

    # Draw stars
    for body in stars:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation)
        if ship.high_contrast:
            color = (0, 0, 0)
        else:
            stellar_type = body.get('stellar_type', 'main_sequence')
            color = STELLAR_TYPES[stellar_type]['color']
        pygame.draw.circle(screen, color, pos_2d, 2)

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

    # Draw rifts
    for rift in ship.rifts:
        pos_2d = project_to_2d(rift['pos'], ship.view_rotation)
        pygame.draw.circle(screen, (255, 0, 255), pos_2d, 5)

    # Draw temples (golden triangles)
    for temple in temples:
        pos_2d = project_to_2d(temple['pos'], ship.view_rotation)
        if temple['temple_type'] == 'master':
            # Halls of Amenti - large golden triangle
            color = (255, 215, 0) if not ship.high_contrast else (0, 0, 0)
            size = 15
        else:
            # Minor temples - smaller triangles with key collected indicator
            if temple['key_index'] in ship.temple_keys:
                color = (0, 255, 128)  # Green if key collected
            else:
                color = (255, 200, 100) if not ship.high_contrast else (0, 0, 0)
            size = 8
        # Draw triangle
        points = [
            (pos_2d[0], pos_2d[1] - size),  # Top
            (pos_2d[0] - size, pos_2d[1] + size),  # Bottom left
            (pos_2d[0] + size, pos_2d[1] + size)  # Bottom right
        ]
        pygame.draw.polygon(screen, color, points)

    # Draw pyramids (golden squares)
    for pyramid in pyramids:
        pos_2d = project_to_2d(pyramid['pos'], ship.view_rotation)
        color = (218, 165, 32) if not ship.high_contrast else (0, 0, 0)  # Golden rod
        size = 10
        rect = pygame.Rect(pos_2d[0] - size, pos_2d[1] - size, size * 2, size * 2)
        pygame.draw.rect(screen, color, rect)

    # Draw ley lines (faint golden lines)
    for ley_line in ley_lines:
        start_2d = project_to_2d(ley_line['start'], ship.view_rotation)
        end_2d = project_to_2d(ley_line['end'], ship.view_rotation)
        if ley_line.get('amenti_path'):
            color = (255, 215, 0, 100)  # Bright gold for Amenti paths
        elif ley_line.get('major'):
            color = (200, 180, 0, 80)  # Darker gold for major lines
        else:
            color = (150, 130, 0, 50)  # Dim gold for minor lines
        pygame.draw.line(screen, color[:3], start_2d, end_2d, 1)

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

        # Draw engine points on spiral
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
