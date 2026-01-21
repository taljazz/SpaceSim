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
from celestial import generate_all_celestial_bodies, generate_complete_universe, update_celestial_positions
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
fullscreen = False
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
zoom_level = 1.0  # 1.0 = normal, >1 = zoomed in, <1 = zoomed out
ZOOM_MIN = 0.2
ZOOM_MAX = 5.0
ZOOM_STEP = 0.1

# Camera orbit state (3D viewing of the ship)
camera_orbit_angle = 0.0  # Horizontal orbit around ship (radians, 0 = behind ship)
camera_pitch = 70.0  # Vertical angle in degrees (0 = level/behind, 90 = top-down)
CAMERA_ORBIT_SPEED = 2.0  # Radians per second for horizontal orbit
CAMERA_PITCH_SPEED = 60.0  # Degrees per second for vertical orbit
CAMERA_PITCH_MIN = 10.0  # Minimum pitch (almost level, from behind)
CAMERA_PITCH_MAX = 90.0  # Maximum pitch (top-down view)


def update_loop():
    """Main game update loop."""
    global next_click_time, stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids
    global fullscreen, screen, zoom_level, camera_orbit_angle, camera_pitch

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

        # F11 toggles fullscreen
        if event.type == pygame.KEYDOWN and event.key == pygame.K_F11:
            fullscreen = not fullscreen
            if fullscreen:
                screen = pygame.display.set_mode((0, 0), pygame.FULLSCREEN)
                ship.speak("Fullscreen enabled.")
            else:
                screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
                ship.speak("Windowed mode.")

        # Mouse wheel zoom
        if event.type == pygame.MOUSEWHEEL:
            if event.y > 0:  # Scroll up = zoom in
                zoom_level = min(ZOOM_MAX, zoom_level + ZOOM_STEP)
                ship.speak(f"Zoom {int(zoom_level * 100)} percent.")
            elif event.y < 0:  # Scroll down = zoom out
                zoom_level = max(ZOOM_MIN, zoom_level - ZOOM_STEP)
                ship.speak(f"Zoom {int(zoom_level * 100)} percent.")

        # Keyboard zoom: ] to zoom in, [ to zoom out, \ to reset
        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_RIGHTBRACKET:  # ] = zoom in
                zoom_level = min(ZOOM_MAX, zoom_level + ZOOM_STEP)
                ship.speak(f"Zoom {int(zoom_level * 100)} percent.")
            elif event.key == pygame.K_LEFTBRACKET:  # [ = zoom out
                zoom_level = max(ZOOM_MIN, zoom_level - ZOOM_STEP)
                ship.speak(f"Zoom {int(zoom_level * 100)} percent.")
            elif event.key == pygame.K_BACKSLASH:  # \ = reset
                zoom_level = 1.0
                ship.speak("Zoom reset to 100 percent.")

    # Get keys and update ship
    keys = pygame.key.get_pressed()

    # Camera orbit controls (continuous)
    # Home/End: Adjust camera pitch (vertical orbit)
    if keys[pygame.K_HOME]:
        camera_pitch = min(CAMERA_PITCH_MAX, camera_pitch + CAMERA_PITCH_SPEED * dt)
    if keys[pygame.K_END]:
        camera_pitch = max(CAMERA_PITCH_MIN, camera_pitch - CAMERA_PITCH_SPEED * dt)
    # Comma/Period: Horizontal orbit around ship
    if keys[pygame.K_COMMA]:
        camera_orbit_angle -= CAMERA_ORBIT_SPEED * dt
    if keys[pygame.K_PERIOD]:
        camera_orbit_angle += CAMERA_ORBIT_SPEED * dt
    # Keep orbit angle in 0-2π range
    camera_orbit_angle %= (2 * np.pi)

    ship.handle_input(keys, events, stars, planets, nebulae)
    ship.update(dt, celestial_bodies, keys, temples, ley_lines, pyramids)

    # Update celestial body positions (orbital mechanics)
    update_celestial_positions(stars, planets, nebulae, ship.simulation_time)

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

    # Get current screen size for proper scaling in fullscreen
    screen_size = screen.get_size()
    screen_w, screen_h = screen_size

    # Animation time for dynamic effects
    anim_time = pygame.time.get_ticks() / 1000.0

    # Calculate ship velocity for visual effects
    velocity_mag = np.linalg.norm(ship.velocity)
    speed_factor = min(1.0, velocity_mag / ship.max_velocity)

    # Camera shake based on velocity (subtle screen offset)
    if velocity_mag > 2.0 and not ship.landed_mode:
        shake_intensity = min(3.0, velocity_mag * 0.1)
        camera_offset_x = np.sin(anim_time * 30) * shake_intensity * speed_factor
        camera_offset_y = np.cos(anim_time * 25) * shake_intensity * speed_factor * 0.5
    else:
        camera_offset_x, camera_offset_y = 0, 0

    # Calculate velocity-based visual drift (objects move opposite to ship movement)
    # IMPORTANT: Must apply view_rotation to velocity to match the projection system
    if velocity_mag > 0.1 and not ship.landed_mode:
        # Apply view rotation to velocity (same formula as projection uses for positions)
        cos_r = np.cos(ship.view_rotation)
        sin_r = np.sin(ship.view_rotation)
        # Rotated velocity matches how positions are projected to screen
        vel_x_rotated = ship.velocity[0] * cos_r + ship.velocity[3] * sin_r
        vel_y_rotated = ship.velocity[1] * cos_r + ship.velocity[4] * sin_r
        vel_mag_rotated = np.sqrt(vel_x_rotated**2 + vel_y_rotated**2)

        # Visual drift in opposite direction of ROTATED velocity (creates sense of motion)
        drift_scale = 15.0 * speed_factor  # How much objects visually shift
        if vel_mag_rotated > 0.01:
            velocity_drift_x = -vel_x_rotated / (vel_mag_rotated + 0.1) * drift_scale
            velocity_drift_y = -vel_y_rotated / (vel_mag_rotated + 0.1) * drift_scale
        else:
            velocity_drift_x, velocity_drift_y = 0, 0
    else:
        velocity_drift_x, velocity_drift_y = 0, 0

    # Draw speed lines when moving fast (star streaming effect)
    if speed_factor > 0.3 and not ship.landed_mode:
        # Speed lines come FROM the direction we're heading (opposite of velocity = stars behind us)
        # IMPORTANT: Use rotated velocity to match projection system
        cos_r = np.cos(ship.view_rotation)
        sin_r = np.sin(ship.view_rotation)
        vel_x_rotated = ship.velocity[0] * cos_r + ship.velocity[3] * sin_r
        vel_y_rotated = ship.velocity[1] * cos_r + ship.velocity[4] * sin_r
        vel_angle = np.arctan2(vel_y_rotated, vel_x_rotated)
        # Lines stream from ahead toward center (we're flying into them)
        stream_angle = vel_angle  # Direction we're moving toward (in screen space)
        num_speed_lines = int(20 * speed_factor)
        for sl_i in range(num_speed_lines):
            random_seed = (sl_i * 7 + int(anim_time * 10)) % 1000
            np.random.seed(random_seed)

            # Lines appear ahead of us and stream toward/past center
            edge_angle = stream_angle + np.random.uniform(-0.6, 0.6)
            start_dist = screen_w * 0.7  # Start from edge
            end_dist = 50  # End near center

            cx, cy = screen_w // 2, screen_h // 2
            # Start position (ahead of us)
            start_x = cx + np.cos(edge_angle) * start_dist
            start_y = cy + np.sin(edge_angle) * start_dist
            # End position (behind/around us)
            end_x = cx + np.cos(edge_angle + np.pi) * end_dist
            end_y = cy + np.sin(edge_angle + np.pi) * end_dist

            # Animate line streaming toward us
            line_phase = (anim_time * 4 * speed_factor + sl_i * 0.15) % 1.0
            lerp_x = start_x + (end_x - start_x) * line_phase
            lerp_y = start_y + (end_y - start_y) * line_phase
            # Trail behind the point
            trail_x = start_x + (end_x - start_x) * max(0, line_phase - 0.15)
            trail_y = start_y + (end_y - start_y) * max(0, line_phase - 0.15)

            # Brighter near center
            brightness = int(100 + 155 * line_phase)
            line_color = (brightness, brightness, 255)
            pygame.draw.line(screen, line_color,
                           (int(trail_x), int(trail_y)),
                           (int(lerp_x), int(lerp_y)), 1)

    # Draw stars with twinkling effect and parallax
    for idx, body in enumerate(stars):
        pos_2d = project_to_2d(body['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        # Apply camera shake and velocity drift with parallax (distant stars move less)
        dist_to_ship = np.linalg.norm(body['pos'] - ship.position)
        parallax_factor = max(0.3, min(1.0, 50 / (dist_to_ship + 10)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

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
        pygame.draw.circle(screen, color, (draw_x, draw_y), size)

    # Draw planets with parallax and orbital motion visible
    for body in planets:
        pos_2d = project_to_2d(body['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        # Parallax effect based on distance
        dist_to_ship = np.linalg.norm(body['pos'] - ship.position)
        parallax_factor = max(0.5, min(1.0, 30 / (dist_to_ship + 5)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

        hue = (((body['pos'][3] + body['pos'][4]) / 200 * 360) % 360 + 360) % 360
        color = pygame.Color(0)
        color.hsva = (hue, 100, 100, 100) if not ship.high_contrast else (0, 0, 0, 100)
        # Apply size multiplier from exoplanet type
        size_mult = body.get('size_mult', 1.0)
        radius = int(PLANET_RADIUS * size_mult)
        pygame.draw.circle(screen, color, (draw_x, draw_y), radius)

        # Draw faint orbital trail for nearby planets
        if dist_to_ship < 80 and not ship.landed_mode:
            orbit_radius = body.get('orbit_radius', 20)
            parent_star = stars[body.get('parent_star_idx', 0)]
            star_2d = project_to_2d(parent_star['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
            star_draw_x = int(star_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
            star_draw_y = int(star_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)
            # Scale orbit to screen (approximation)
            screen_orbit_radius = int(orbit_radius * 2)
            if screen_orbit_radius > 5:
                pygame.draw.circle(screen, (50, 50, 80), (star_draw_x, star_draw_y),
                                 screen_orbit_radius, 1)

    # Draw nebulae with swirling effect
    for idx, body in enumerate(nebulae):
        pos_2d = project_to_2d(body['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        dist_to_ship = np.linalg.norm(body['pos'] - ship.position)
        parallax_factor = max(0.4, min(1.0, 40 / (dist_to_ship + 10)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

        if ship.high_contrast:
            color = (128, 128, 128)
        else:
            nebula_type = body.get('nebula_type', 'emission')
            base_color = NEBULA_TYPES[nebula_type]['color']
            # Pulsing/swirling nebula effect
            pulse = 0.7 + 0.3 * np.sin(anim_time * body.get('rotation_speed', 0.03) * 50 + idx)
            color = tuple(int(c * pulse) for c in base_color)

        # Draw multiple layers for depth
        for layer in range(3):
            layer_size = 15 - layer * 3
            layer_alpha = 1.0 - layer * 0.25
            layer_color = tuple(int(c * layer_alpha) for c in color)
            layer_offset_x = int(np.sin(anim_time + layer) * 2)
            layer_offset_y = int(np.cos(anim_time + layer) * 2)
            pygame.draw.circle(screen, layer_color,
                             (draw_x + layer_offset_x, draw_y + layer_offset_y), layer_size)

    # Draw rifts with pulsing dimensional effect
    for idx, rift in enumerate(ship.rifts):
        pos_2d = project_to_2d(rift['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        # Parallax for rifts (they feel closer/more present)
        dist_to_ship = np.linalg.norm(rift['pos'] - ship.position)
        parallax_factor = max(0.6, min(1.0, 25 / (dist_to_ship + 5)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

        # Pulsing size and color
        pulse = 0.5 + 0.5 * np.sin(anim_time * 4 + idx)
        size = int(5 + 3 * pulse)
        # Shifting purple/cyan colors for dimensional effect
        r = int(200 + 55 * np.sin(anim_time * 3))
        g = int(50 + 50 * np.sin(anim_time * 2 + 1))
        b = int(200 + 55 * np.cos(anim_time * 3))
        pygame.draw.circle(screen, (r, g, b), (draw_x, draw_y), size)
        # Inner glow
        pygame.draw.circle(screen, (255, 255, 255), (draw_x, draw_y), max(2, size // 2))

    # Draw temples (golden triangles) with pulsing glow
    for idx, temple in enumerate(temples):
        pos_2d = project_to_2d(temple['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        # Parallax for temples
        dist_to_ship = np.linalg.norm(temple['pos'] - ship.position)
        parallax_factor = max(0.5, min(1.0, 35 / (dist_to_ship + 8)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

        pulse = 0.7 + 0.3 * np.sin(anim_time * 2 + idx * 0.3)

        if temple['temple_type'] == 'master':
            # Halls of Amenti - large golden triangle with radiant glow
            base_color = (255, 215, 0) if not ship.high_contrast else (0, 0, 0)
            size = int(15 + 3 * np.sin(anim_time * 1.5))
            # Draw outer glow rings
            for ring in range(3, 0, -1):
                glow_color = (255, 215, 0)
                pygame.draw.circle(screen, glow_color, (draw_x, draw_y), size + ring * 5, 1)
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
            (draw_x, draw_y - size),  # Top
            (draw_x - size, draw_y + size),  # Bottom left
            (draw_x + size, draw_y + size)  # Bottom right
        ]
        pygame.draw.polygon(screen, color, points)

        # Draw inner glow for uncollected temples
        if temple['temple_type'] != 'master' and temple['key_index'] not in ship.temple_keys:
            inner_points = [
                (draw_x, draw_y - size // 2),
                (draw_x - size // 2, draw_y + size // 2),
                (draw_x + size // 2, draw_y + size // 2)
            ]
            inner_color = tuple(min(255, int(c * 1.3)) for c in color)
            pygame.draw.polygon(screen, inner_color, inner_points)

    # Draw pyramids (golden squares) with parallax
    for pyramid in pyramids:
        pos_2d = project_to_2d(pyramid['pos'], ship.view_rotation, screen_size, zoom_level, ship.position)
        dist_to_ship = np.linalg.norm(pyramid['pos'] - ship.position)
        parallax_factor = max(0.5, min(1.0, 35 / (dist_to_ship + 8)))
        draw_x = int(pos_2d[0] + camera_offset_x * parallax_factor + velocity_drift_x * parallax_factor)
        draw_y = int(pos_2d[1] + camera_offset_y * parallax_factor + velocity_drift_y * parallax_factor)

        # Pulsing pyramid glow
        pulse = 0.8 + 0.2 * np.sin(anim_time * 1.5)
        base_color = (218, 165, 32) if not ship.high_contrast else (0, 0, 0)
        color = tuple(int(c * pulse) for c in base_color)
        size = 10
        rect = pygame.Rect(draw_x - size, draw_y - size, size * 2, size * 2)
        pygame.draw.rect(screen, color, rect)
        # Inner highlight
        pygame.draw.rect(screen, (255, 220, 100), pygame.Rect(draw_x - 3, draw_y - 3, 6, 6))

    # Draw ley lines with energy flow effect
    for idx, ley_line in enumerate(ley_lines):
        start_2d = project_to_2d(ley_line['start'], ship.view_rotation, screen_size, zoom_level, ship.position)
        end_2d = project_to_2d(ley_line['end'], ship.view_rotation, screen_size, zoom_level, ship.position)

        # Calculate average parallax for the ley line based on midpoint distance
        midpoint = (ley_line['start'] + ley_line['end']) / 2
        dist_to_ship = np.linalg.norm(midpoint - ship.position)
        parallax_factor = max(0.4, min(1.0, 45 / (dist_to_ship + 15)))

        # Apply velocity drift to both endpoints
        start_draw = (int(start_2d[0] + velocity_drift_x * parallax_factor),
                      int(start_2d[1] + velocity_drift_y * parallax_factor))
        end_draw = (int(end_2d[0] + velocity_drift_x * parallax_factor),
                    int(end_2d[1] + velocity_drift_y * parallax_factor))

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
        pygame.draw.line(screen, color, start_draw, end_draw, width)

        # Draw energy particles flowing along the line (if on this ley line, show more)
        if ship.on_ley_line and ship.current_ley_line is ley_line:
            # More visible energy dots when player is on this ley line
            for i in range(5):
                t = (anim_time * 0.3 + i * 0.2) % 1.0
                particle_x = int(start_draw[0] + (end_draw[0] - start_draw[0]) * t)
                particle_y = int(start_draw[1] + (end_draw[1] - start_draw[1]) * t)
                pygame.draw.circle(screen, (255, 255, 200), (particle_x, particle_y), 3)

    # Draw planet grid if landed
    if ship.landed_mode:
        for pos in ship.crystal_positions:
            screen_x = int(screen_w / 2 + pos[0] * 20)
            screen_y = int(screen_h / 2 + pos[1] * 20)
            pygame.draw.circle(screen, (0, 255, 0), (screen_x, screen_y), 5)
        cursor_x = int(screen_w / 2 + ship.cursor_pos[0] * 20)
        cursor_y = int(screen_h / 2 + ship.cursor_pos[1] * 20)
        pygame.draw.line(screen, (255, 0, 0), (cursor_x - 5, cursor_y), (cursor_x + 5, cursor_y))
        pygame.draw.line(screen, (255, 0, 0), (cursor_x, cursor_y - 5), (cursor_x, cursor_y + 5))
    else:
        # Ship is ALWAYS at screen center (you ARE the ship, looking out)
        ship_center = (screen_w // 2, screen_h // 2)

        # Calculate movement properties
        velocity_mag = np.linalg.norm(ship.velocity)
        glow_intensity = min(1.0, velocity_mag / ship.max_velocity)
        avg_resonance = np.mean(ship.resonance_levels)

        # === CALCULATE SHIP VISUAL ORIENTATION ===
        # Ship points in direction of travel (velocity in screen space)
        # Apply view_rotation to velocity to get screen-space direction
        cos_r = np.cos(ship.view_rotation)
        sin_r = np.sin(ship.view_rotation)
        vel_x_screen = ship.velocity[0] * cos_r + ship.velocity[3] * sin_r
        vel_y_screen = ship.velocity[1] * cos_r + ship.velocity[4] * sin_r

        # Ship orientation: point in velocity direction, or default forward if stationary
        if velocity_mag > 0.1:
            ship_heading_angle = np.arctan2(vel_y_screen, vel_x_screen)
        else:
            # When stationary, maintain last heading or default to "up" on screen
            ship_heading_angle = -np.pi / 2  # Point upward when stationary

        # === 3D CAMERA ORBIT PERSPECTIVE ===
        # camera_orbit_angle: horizontal orbit (0 = behind ship, π = in front)
        # camera_pitch: vertical angle (90 = top-down, lower = more from behind)

        # The visual angle of the ship on screen combines:
        # - ship_heading_angle: which way the ship is actually pointing
        # - camera_orbit_angle: which way we're viewing from
        ship_visual_angle = ship_heading_angle - camera_orbit_angle

        # Pitch affects vertical foreshortening (1.0 at 90°, 0 at 0°)
        pitch_rad = np.radians(camera_pitch)
        vertical_scale = np.sin(pitch_rad)  # 1.0 when top-down, 0 when level

        # Height offset - when viewing from lower angles, ship appears higher on screen
        height_offset = np.cos(pitch_rad) * 30  # Ship rises as we lower camera

        # === VISIBLE SHIP MODEL ===
        # Ship is a 3D vessel - we see different aspects based on camera angle
        ship_size = 30  # Base size of ship
        pulse = 0.85 + 0.15 * np.sin(anim_time * 3)  # Gentle pulse

        # Helper function to apply 3D perspective to a point
        def apply_perspective(x, y, center_x, center_y):
            # Offset from center
            dx = x - center_x
            dy = y - center_y
            # Apply vertical foreshortening
            dy_scaled = dy * vertical_scale
            # Return new position with height offset
            return (center_x + dx, center_y + dy_scaled - height_offset)

        # Calculate ship vertices in local space, then apply perspective
        # Nose (front) - points where we're going
        nose_local_x = np.cos(ship_visual_angle) * ship_size * 1.5
        nose_local_y = np.sin(ship_visual_angle) * ship_size * 1.5
        nose_x, nose_y = apply_perspective(
            ship_center[0] + nose_local_x,
            ship_center[1] + nose_local_y,
            ship_center[0], ship_center[1]
        )

        # Left wing
        left_angle = ship_visual_angle + np.pi * 0.75
        left_local_x = np.cos(left_angle) * ship_size
        left_local_y = np.sin(left_angle) * ship_size
        left_x, left_y = apply_perspective(
            ship_center[0] + left_local_x,
            ship_center[1] + left_local_y,
            ship_center[0], ship_center[1]
        )

        # Right wing
        right_angle = ship_visual_angle - np.pi * 0.75
        right_local_x = np.cos(right_angle) * ship_size
        right_local_y = np.sin(right_angle) * ship_size
        right_x, right_y = apply_perspective(
            ship_center[0] + right_local_x,
            ship_center[1] + right_local_y,
            ship_center[0], ship_center[1]
        )

        # Tail (back center)
        tail_local_x = -np.cos(ship_visual_angle) * ship_size * 0.5
        tail_local_y = -np.sin(ship_visual_angle) * ship_size * 0.5
        tail_x, tail_y = apply_perspective(
            ship_center[0] + tail_local_x,
            ship_center[1] + tail_local_y,
            ship_center[0], ship_center[1]
        )

        ship_points = [
            (int(nose_x), int(nose_y)),
            (int(left_x), int(left_y)),
            (int(tail_x), int(tail_y)),
            (int(right_x), int(right_y))
        ]

        # Calculate visible "top" of ship for 3D effect
        # When viewing from lower angles, we see the side/height of the ship
        ship_height = 15 * (1 - vertical_scale)  # Ship has height when not top-down

        # === 3D SHIP BODY (show height when viewing from lower angles) ===
        if ship_height > 2:
            # Draw the "side" of the ship as a darker polygon connecting top and bottom
            # Bottom vertices (current ship_points)
            # Top vertices (same but offset upward by ship_height)
            top_nose = (int(nose_x), int(nose_y - ship_height))
            top_left = (int(left_x), int(left_y - ship_height))
            top_right = (int(right_x), int(right_y - ship_height))
            top_tail = (int(tail_x), int(tail_y - ship_height))

            # Draw side panels (connecting top and bottom edges)
            side_color = (60, 80, 100)  # Dark blue-gray for sides

            # Left side panel
            pygame.draw.polygon(screen, side_color, [
                (int(nose_x), int(nose_y)), top_nose,
                top_left, (int(left_x), int(left_y))
            ])
            # Right side panel
            pygame.draw.polygon(screen, side_color, [
                (int(nose_x), int(nose_y)), top_nose,
                top_right, (int(right_x), int(right_y))
            ])
            # Back panel
            pygame.draw.polygon(screen, (40, 60, 80), [
                (int(left_x), int(left_y)), top_left,
                top_tail, (int(tail_x), int(tail_y))
            ])
            pygame.draw.polygon(screen, (40, 60, 80), [
                (int(right_x), int(right_y)), top_right,
                top_tail, (int(tail_x), int(tail_y))
            ])

            # Update ship_points to be the TOP of the ship
            ship_points = [top_nose, top_left, top_tail, top_right]

        # Outer glow (large, soft) - now uses perspective
        for glow_layer in range(4, 0, -1):
            glow_size = ship_size + glow_layer * 8
            glow_alpha = 0.15 / glow_layer

            glow_nose_x, glow_nose_y = apply_perspective(
                ship_center[0] + np.cos(ship_visual_angle) * glow_size * 1.5,
                ship_center[1] + np.sin(ship_visual_angle) * glow_size * 1.5,
                ship_center[0], ship_center[1]
            )
            glow_left_x, glow_left_y = apply_perspective(
                ship_center[0] + np.cos(left_angle) * glow_size,
                ship_center[1] + np.sin(left_angle) * glow_size,
                ship_center[0], ship_center[1]
            )
            glow_right_x, glow_right_y = apply_perspective(
                ship_center[0] + np.cos(right_angle) * glow_size,
                ship_center[1] + np.sin(right_angle) * glow_size,
                ship_center[0], ship_center[1]
            )
            glow_tail_x, glow_tail_y = apply_perspective(
                ship_center[0] - np.cos(ship_visual_angle) * glow_size * 0.5,
                ship_center[1] - np.sin(ship_visual_angle) * glow_size * 0.5,
                ship_center[0], ship_center[1]
            )

            # Apply height offset to glow when viewing 3D
            glow_height_offset = ship_height if ship_height > 2 else 0
            glow_points = [
                (int(glow_nose_x), int(glow_nose_y - glow_height_offset)),
                (int(glow_left_x), int(glow_left_y - glow_height_offset)),
                (int(glow_tail_x), int(glow_tail_y - glow_height_offset)),
                (int(glow_right_x), int(glow_right_y - glow_height_offset))
            ]
            glow_color = (int(100 * pulse), int(200 * pulse), int(255 * pulse))
            pygame.draw.polygon(screen, glow_color, glow_points, 2)

        # Ship body fill (Tuaoi mode color)
        tuaoi_colors = {
            'healing': (0, 180, 80),
            'navigation': (80, 120, 200),
            'communication': (200, 200, 80),
            'power': (200, 80, 80),
            'regeneration': (160, 80, 200),
            'transcendence': (200, 200, 200)
        }
        body_color = tuaoi_colors.get(ship.tuaoi_mode, (150, 150, 200))
        body_color = tuple(int(c * pulse) for c in body_color)
        pygame.draw.polygon(screen, body_color, ship_points)

        # Ship outline (bright, always visible)
        outline_color = (255, 255, 255)
        pygame.draw.polygon(screen, outline_color, ship_points, 3)

        # Cockpit/center marker (elevated when viewing from angle)
        cockpit_center = (ship_center[0], int(ship_center[1] - height_offset - ship_height))
        pygame.draw.circle(screen, (255, 255, 200), cockpit_center, 6)
        pygame.draw.circle(screen, outline_color, cockpit_center, 6, 2)

        # Engine glow at back when moving (account for 3D height)
        if velocity_mag > 0.5:
            engine_intensity = min(1.0, velocity_mag / 5.0)
            engine_color = (255, int(150 * (1 - engine_intensity)), 0)
            # Height adjustment for 3D view
            eng_height = ship_height if ship_height > 2 else 0
            # Left engine
            left_eng_x = (left_x + tail_x) / 2
            left_eng_y = (left_y + tail_y) / 2 - eng_height
            pygame.draw.circle(screen, engine_color, (int(left_eng_x), int(left_eng_y)), int(5 + 5 * engine_intensity))
            # Right engine
            right_eng_x = (right_x + tail_x) / 2
            right_eng_y = (right_y + tail_y) / 2 - eng_height
            pygame.draw.circle(screen, engine_color, (int(right_eng_x), int(right_eng_y)), int(5 + 5 * engine_intensity))
            # Engine trails (extend behind and down in 3D)
            trail_length = 20 * engine_intensity
            trail_end_x = tail_x - np.cos(ship_visual_angle) * trail_length
            trail_end_y = tail_y - np.sin(ship_visual_angle) * trail_length + eng_height * 0.5  # Trails go back and down
            pygame.draw.line(screen, (255, 200, 100), (int(left_eng_x), int(left_eng_y)), (int(trail_end_x), int(trail_end_y)), 2)
            pygame.draw.line(screen, (255, 200, 100), (int(right_eng_x), int(right_eng_y)), (int(trail_end_x), int(trail_end_y)), 2)

        # Direction indicator line (extends from nose, on top of ship)
        indicator_length = 25
        # Get the actual nose position (which may have been updated for 3D)
        actual_nose = ship_points[0] if isinstance(ship_points[0], tuple) else (int(nose_x), int(nose_y - ship_height))
        indicator_x = actual_nose[0] + np.cos(ship_visual_angle) * indicator_length
        indicator_y = actual_nose[1] + np.sin(ship_visual_angle) * indicator_length * vertical_scale
        pygame.draw.line(screen, (255, 255, 0), actual_nose, (int(indicator_x), int(indicator_y)), 2)

        # Pulsing outer ring for extra visibility (ellipse when viewing from angle)
        ring_pulse = 0.7 + 0.3 * np.sin(anim_time * 4)
        ring_radius = int(70 + 10 * ring_pulse)
        ring_color = (int(100 * ring_pulse), int(255 * ring_pulse), int(255 * ring_pulse))
        # Draw as ellipse when not top-down, centered on ship
        ring_center_y = int(ship_center[1] - height_offset - ship_height / 2)
        ring_height = int(ring_radius * vertical_scale)
        if ring_height > 10:
            pygame.draw.ellipse(screen, ring_color,
                              (ship_center[0] - ring_radius, ring_center_y - ring_height,
                               ring_radius * 2, ring_height * 2), 2)
        else:
            # Very flat ellipse - just draw a line
            pygame.draw.line(screen, ring_color,
                           (ship_center[0] - ring_radius, ring_center_y),
                           (ship_center[0] + ring_radius, ring_center_y), 2)

        # === MOTION TRAIL (velocity streaks behind ship) ===
        if velocity_mag > 0.5:
            # Draw fading trail lines behind ship (using rotated velocity for screen-space direction)
            cos_r = np.cos(ship.view_rotation)
            sin_r = np.sin(ship.view_rotation)
            vel_x_rot = ship.velocity[0] * cos_r + ship.velocity[3] * sin_r
            vel_y_rot = ship.velocity[1] * cos_r + ship.velocity[4] * sin_r
            vel_angle = np.arctan2(vel_y_rot, vel_x_rot)
            for trail_i in range(5):
                trail_alpha = int(150 * (1 - trail_i / 5) * glow_intensity)
                trail_length = 10 + trail_i * 8
                trail_spread = trail_i * 3
                # Calculate trail position (behind ship)
                trail_x = ship_center[0] - np.cos(vel_angle) * trail_length
                trail_y = ship_center[1] - np.sin(vel_angle) * trail_length
                # Add some spread
                offset_angle = vel_angle + np.pi/2
                trail_x1 = trail_x + np.cos(offset_angle) * trail_spread
                trail_y1 = trail_y + np.sin(offset_angle) * trail_spread
                trail_x2 = trail_x - np.cos(offset_angle) * trail_spread
                trail_y2 = trail_y - np.sin(offset_angle) * trail_spread
                trail_color = (255, 200, int(50 + 100 * (1 - trail_i / 5)))
                pygame.draw.line(screen, trail_color, ship_center, (int(trail_x1), int(trail_y1)), 1)
                pygame.draw.line(screen, trail_color, ship_center, (int(trail_x2), int(trail_y2)), 1)

        # === BREATHING SPIRAL (pulses with resonance) ===
        # Spiral size breathes based on average resonance
        breath = 1.0 + 0.15 * np.sin(anim_time * 2) * avg_resonance
        max_r = 20 * breath
        theta_max = 6 * np.pi
        spiral_a = max_r / (PHI ** (2 * theta_max / np.pi))

        # Add subtle rotation animation based on resonance
        spiral_rotation = anim_time * 0.3 * avg_resonance

        theta = np.linspace(0, theta_max, 100)
        r = spiral_a * PHI ** (2 * theta / np.pi)
        x = r * np.cos(theta + ship_visual_angle + spiral_rotation)
        y = r * np.sin(theta + ship_visual_angle + spiral_rotation)
        spiral_points = np.tile(ship.position, (100, 1))
        spiral_points[:, 0] += x
        spiral_points[:, 1] += y
        screen_points = [project_to_2d(p, ship.view_rotation, screen_size, zoom_level, ship.position) for p in spiral_points]

        # === SPIRAL COLOR GRADIENT (shifts based on Tuaoi mode and resonance) ===
        # Draw spiral segments with color gradient
        tuaoi_colors = {
            'healing': (0, 255, 100),
            'navigation': (100, 150, 255),
            'communication': (255, 255, 100),
            'power': (255, 100, 100),
            'regeneration': (200, 100, 255),
            'transcendence': (255, 255, 255)
        }
        base_spiral_color = tuaoi_colors.get(ship.tuaoi_mode, (255, 255, 0))

        for seg_i in range(len(screen_points) - 1):
            # Color shifts along spiral
            t = seg_i / len(screen_points)
            color_shift = 0.5 + 0.5 * np.sin(anim_time * 4 + t * 6)
            seg_color = tuple(int(c * (0.5 + 0.5 * color_shift)) for c in base_spiral_color)
            if not ship.high_contrast:
                pygame.draw.line(screen, seg_color, screen_points[seg_i], screen_points[seg_i + 1], 2)
            else:
                pygame.draw.line(screen, (0, 0, 255), screen_points[seg_i], screen_points[seg_i + 1], 2)

        # === ENERGY FLOW PARTICLES (dots flowing along spiral) ===
        num_particles = 8
        for p_i in range(num_particles):
            # Particle position moves along spiral over time
            particle_t = (anim_time * 0.5 + p_i / num_particles) % 1.0
            particle_idx = int(particle_t * (len(screen_points) - 1))
            if particle_idx < len(screen_points):
                px, py = screen_points[particle_idx]
                # Particle brightness pulses
                p_bright = 0.6 + 0.4 * np.sin(anim_time * 6 + p_i)
                p_color = tuple(int(c * p_bright) for c in base_spiral_color)
                pygame.draw.circle(screen, p_color, (int(px), int(py)), 3)

        # === TUAOI CRYSTAL CORE (hexagonal center with mode color) ===
        core_pulse = 0.8 + 0.2 * np.sin(anim_time * 3)
        core_size = int(8 * core_pulse)
        core_color = tuple(int(c * core_pulse) for c in base_spiral_color)

        # Draw hexagonal crystal core (6 sides for Tuaoi)
        hex_points = []
        for h_i in range(6):
            h_angle = h_i * (np.pi / 3) + anim_time * 0.5
            hx = ship_center[0] + core_size * np.cos(h_angle)
            hy = ship_center[1] + core_size * np.sin(h_angle)
            hex_points.append((hx, hy))
        pygame.draw.polygon(screen, core_color, hex_points, 2)

        # Inner glow
        inner_hex_points = []
        for h_i in range(6):
            h_angle = h_i * (np.pi / 3) + anim_time * 0.5
            hx = ship_center[0] + (core_size * 0.5) * np.cos(h_angle)
            hy = ship_center[1] + (core_size * 0.5) * np.sin(h_angle)
            inner_hex_points.append((hx, hy))
        inner_color = tuple(min(255, int(c * 1.3)) for c in core_color)
        pygame.draw.polygon(screen, inner_color, inner_hex_points)

        # === ENGINE POINTS with enhanced glow ===
        theta_engines = np.array([theta_max - i * (np.pi / PHI) for i in range(3)])
        r_engines = spiral_a * PHI ** (2 * theta_engines / np.pi)
        x_engines = r_engines * np.cos(theta_engines + ship_visual_angle + spiral_rotation)
        y_engines = r_engines * np.sin(theta_engines + ship_visual_angle + spiral_rotation)
        engine_points = np.tile(ship.position, (3, 1))
        engine_points[:, 0] += x_engines
        engine_points[:, 1] += y_engines
        screen_engine_points = [project_to_2d(p, ship.view_rotation, screen_size, zoom_level, ship.position) for p in engine_points]

        engine_pulse = 0.7 + 0.3 * np.sin(anim_time * 8)

        for eng_i, ep in enumerate(screen_engine_points):
            # Outer glow based on velocity (larger, more intense when moving)
            if glow_intensity > 0.1:
                glow_size = int(10 + 8 * glow_intensity * engine_pulse)
                # Color shifts orange->white with speed
                glow_r = 255
                glow_g = int(100 + 155 * (1 - glow_intensity))
                glow_b = int(50 * (1 - glow_intensity))
                pygame.draw.circle(screen, (glow_r, glow_g, glow_b), ep, glow_size)
                # Secondary inner glow
                pygame.draw.circle(screen, (255, 200, 100), ep, int(glow_size * 0.6))

            # Engine core with per-engine pulse offset
            eng_pulse = 0.7 + 0.3 * np.sin(anim_time * 10 + eng_i * 2)
            eng_color = (255, int(50 * eng_pulse), 0) if not ship.high_contrast else (0, 255, 0)
            pygame.draw.circle(screen, eng_color, ep, 5)

            # Tiny exhaust particles when moving (using rotated velocity for screen-space)
            if velocity_mag > 1.0:
                cos_r = np.cos(ship.view_rotation)
                sin_r = np.sin(ship.view_rotation)
                vel_x_rot = ship.velocity[0] * cos_r + ship.velocity[3] * sin_r
                vel_y_rot = ship.velocity[1] * cos_r + ship.velocity[4] * sin_r
                for exhaust_i in range(3):
                    ex_dist = 5 + exhaust_i * 4 + np.sin(anim_time * 15 + eng_i + exhaust_i) * 2
                    ex_angle = np.arctan2(vel_y_rot, vel_x_rot) + np.pi  # Behind ship
                    ex_spread = (exhaust_i - 1) * 0.3
                    ex_x = ep[0] + np.cos(ex_angle + ex_spread) * ex_dist
                    ex_y = ep[1] + np.sin(ex_angle + ex_spread) * ex_dist
                    ex_alpha = int(200 * (1 - exhaust_i / 3))
                    pygame.draw.circle(screen, (255, ex_alpha, 0), (int(ex_x), int(ex_y)), 2)

        # Draw resonance rings around ship (5 rings for 5 dimensions)
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
