"""
Celestial body generation for the Golden Spiral Spaceship Simulator.

This module handles the procedural generation of stars, planets, and nebulae
using golden spiral mathematics and Fibonacci sequences.
"""

import numpy as np
import random
from constants import (
    N_DIMENSIONS, PHI, FIB_SEQ, SCALE_FACTOR, FREQUENCY_RANGE,
    N_STARS, N_PLANETS_PER_STAR, N_NEBULAE, ORBIT_RADIUS,
    STELLAR_TYPES, STELLAR_TYPE_PROBABILITIES,
    NEBULA_TYPES, NEBULA_TYPE_PROBABILITIES,
    EXOPLANET_TYPES, EXOPLANET_TYPE_PROBABILITIES,
    MINOR_TEMPLE_COUNT, TEMPLE_KEY_NAMES, TEMPLE_KEY_FREQUENCIES,
    LEY_LINE_COUNT, LEY_LINE_FREQ,
    PYRAMID_COUNT, PYRAMID_RESONANCE_FREQ,
    TEMPLE_RESONANCE_FREQ, HALLS_OF_AMENTI_POS
)


def generate_celestial(n, body_type='star'):
    """
    Generate celestial bodies procedurally using golden spiral positioning.

    Uses the Fibonacci sequence and golden ratio (PHI) to position bodies
    in 5D space along a golden spiral pattern. Higher dimensions are derived
    from spatial dimensions with PHI relationships plus random variation.

    Args:
        n: Number of bodies to generate
        body_type: Type of celestial body ('star', 'planet', or 'nebula')

    Returns:
        List of dictionaries with keys:
            - 'pos': numpy array of 5D position
            - 'freq': base frequency for resonance
            - 'type': body type string
            - 'stellar_type': stellar evolution type (only for stars)
    """
    bodies = []
    for i in range(n):
        theta = i * 2 * np.pi * PHI
        r = FIB_SEQ[i % len(FIB_SEQ)] * SCALE_FACTOR
        pos = np.zeros(N_DIMENSIONS)
        pos[0] = r * np.cos(theta)
        pos[1] = r * np.sin(theta)
        # Higher dimensions derived from spatial dims with PHI relationship
        for d in range(2, N_DIMENSIONS):
            pos[d] = pos[d-2] * PHI + random.uniform(-10, 10)
        freq = random.uniform(*FREQUENCY_RANGE)

        # Create body dictionary
        body = {'pos': pos, 'freq': freq, 'type': body_type}

        # Assign stellar type for stars
        if body_type == 'star':
            stellar_type = np.random.choice(
                list(STELLAR_TYPE_PROBABILITIES.keys()),
                p=list(STELLAR_TYPE_PROBABILITIES.values())
            )
            body['stellar_type'] = stellar_type
            # Multiply frequency by stellar type multiplier
            body['freq'] *= STELLAR_TYPES[stellar_type]['freq_mult']

        # Assign nebula type for nebulae
        elif body_type == 'nebula':
            nebula_type = np.random.choice(
                list(NEBULA_TYPE_PROBABILITIES.keys()),
                p=list(NEBULA_TYPE_PROBABILITIES.values())
            )
            body['nebula_type'] = nebula_type
            # Adjust frequency to nebula type range
            freq_min, freq_max = NEBULA_TYPES[nebula_type]['freq_range']
            body['freq'] = random.uniform(freq_min, freq_max)
            # Store dissonance level
            body['dissonance'] = NEBULA_TYPES[nebula_type]['dissonance']

        bodies.append(body)
    return bodies


def generate_all_celestial_bodies():
    """
    Generate the complete universe of celestial bodies.

    Creates stars using golden spiral, planets orbiting each star,
    and nebulae as environmental hazards.

    Returns:
        Tuple of (stars, planets, nebulae, celestial_bodies):
            - stars: List of star bodies
            - planets: List of planet bodies
            - nebulae: List of nebula bodies
            - celestial_bodies: Combined list of all bodies
    """
    # Generate stars using golden spiral
    stars = generate_celestial(N_STARS, 'star')

    # Generate planets orbiting each star
    planets = []
    for star in stars:
        for _ in range(N_PLANETS_PER_STAR):
            pos = star['pos'] + np.random.uniform(-ORBIT_RADIUS, ORBIT_RADIUS, N_DIMENSIONS)
            freq = random.uniform(*FREQUENCY_RANGE)

            # Assign exoplanet type
            exoplanet_type = np.random.choice(
                list(EXOPLANET_TYPE_PROBABILITIES.keys()),
                p=list(EXOPLANET_TYPE_PROBABILITIES.values())
            )

            # Create planet with exoplanet properties
            planet = {
                'pos': pos,
                'freq': freq,
                'type': 'planet',
                'exoplanet_type': exoplanet_type,
                'size_mult': EXOPLANET_TYPES[exoplanet_type]['size_mult'],
                'crystal_mult': EXOPLANET_TYPES[exoplanet_type]['crystal_mult'],
                'difficulty': EXOPLANET_TYPES[exoplanet_type]['difficulty']
            }
            planets.append(planet)

    # Generate nebulae
    nebulae = generate_celestial(N_NEBULAE, 'nebula')

    # Combined list for collision/proximity checks
    celestial_bodies = stars + planets + nebulae

    return stars, planets, nebulae, celestial_bodies


def generate_temples():
    """
    Generate the 12 minor temples (zodiac temples) plus positioning for Halls of Amenti.

    Temples are placed in a sacred geometry pattern - a dodecagon (12-sided)
    arrangement around the universe center, each at golden ratio distances.

    Returns:
        List of temple dictionaries with position, frequency, key name, etc.
    """
    temples = []

    # Generate 12 minor temples in a sacred dodecagon pattern
    for i in range(MINOR_TEMPLE_COUNT):
        # Position temples in golden spiral pattern with zodiac spacing
        angle = i * (2 * np.pi / 12) + (np.pi / 6)  # 30-degree offset for zodiac alignment
        radius = FIB_SEQ[min(i + 3, len(FIB_SEQ) - 1)] * SCALE_FACTOR * PHI

        pos = np.zeros(N_DIMENSIONS)
        pos[0] = radius * np.cos(angle)
        pos[1] = radius * np.sin(angle)
        # Higher dimensions follow golden ratio relationships
        pos[2] = radius * np.sin(angle * PHI) * 0.5
        pos[3] = pos[0] * PHI
        pos[4] = pos[1] * PHI

        temple = {
            'pos': pos,
            'freq': TEMPLE_KEY_FREQUENCIES[i],
            'type': 'temple',
            'key_name': TEMPLE_KEY_NAMES[i],
            'key_index': i,
            'temple_type': 'minor',
            'desc': f'Temple of {TEMPLE_KEY_NAMES[i]} - guardian of the {TEMPLE_KEY_NAMES[i]} key'
        }
        temples.append(temple)

    # Add Halls of Amenti (Master Temple) at universe center
    halls_of_amenti = {
        'pos': HALLS_OF_AMENTI_POS.copy(),
        'freq': TEMPLE_RESONANCE_FREQ,  # 110 Hz ancient healing frequency
        'type': 'temple',
        'key_name': 'Amenti',
        'key_index': -1,  # Special index for master temple
        'temple_type': 'master',
        'desc': 'Halls of Amenti - the Master Temple of eternal wisdom'
    }
    temples.append(halls_of_amenti)

    return temples


def generate_ley_lines(temples):
    """
    Generate ley lines connecting temples in a sacred energy grid.

    Ley lines form connections between temples, creating fast-travel
    corridors with enhanced resonance properties.

    Args:
        temples: List of temple dictionaries

    Returns:
        List of ley line dictionaries with start, end, and properties
    """
    ley_lines = []

    # Connect each temple to the next in sequence (forming a ring)
    for i in range(MINOR_TEMPLE_COUNT):
        next_i = (i + 1) % MINOR_TEMPLE_COUNT

        ley_line = {
            'start': temples[i]['pos'].copy(),
            'end': temples[next_i]['pos'].copy(),
            'freq': LEY_LINE_FREQ,
            'type': 'ley_line',
            'name': f"Ley Line: {temples[i]['key_name']} to {temples[next_i]['key_name']}",
            'temple_indices': (i, next_i)
        }
        ley_lines.append(ley_line)

    # Connect opposite temples (6 lines forming a star pattern)
    for i in range(6):
        opposite_i = i + 6
        ley_line = {
            'start': temples[i]['pos'].copy(),
            'end': temples[opposite_i]['pos'].copy(),
            'freq': LEY_LINE_FREQ * PHI,  # Higher frequency for major ley lines
            'type': 'ley_line',
            'name': f"Major Ley Line: {temples[i]['key_name']} to {temples[opposite_i]['key_name']}",
            'temple_indices': (i, opposite_i),
            'major': True
        }
        ley_lines.append(ley_line)

    # Connect all temples to Halls of Amenti (12 radial lines)
    amenti = temples[-1]  # Master temple is last in list
    for i in range(MINOR_TEMPLE_COUNT):
        ley_line = {
            'start': temples[i]['pos'].copy(),
            'end': amenti['pos'].copy(),
            'freq': TEMPLE_RESONANCE_FREQ,  # 110 Hz for Amenti connections
            'type': 'ley_line',
            'name': f"Amenti Path: {temples[i]['key_name']} to Halls of Amenti",
            'temple_indices': (i, -1),
            'amenti_path': True
        }
        ley_lines.append(ley_line)

    return ley_lines


def generate_pyramids():
    """
    Generate pyramid resonance chambers at sacred locations.

    Pyramids are placed at key energy intersection points and provide
    enhanced healing and consciousness-boosting effects.

    Returns:
        List of pyramid dictionaries with position and properties
    """
    pyramids = []

    # Place pyramids at golden ratio distances in sacred directions
    pyramid_positions = [
        # First pyramid: Giza alignment (Earth reference point)
        np.array([PHI * 50, 0, PHI * 30, PHI**2 * 50, 0]),
        # Second pyramid: Stellar alignment (star grid nexus)
        np.array([-PHI * 40, PHI * 40, -PHI * 20, 0, PHI**2 * 40]),
        # Third pyramid: Dimensional gateway (higher dim focus)
        np.array([0, -PHI * 60, PHI * 40, PHI**3 * 30, PHI**3 * 30])
    ]

    pyramid_names = [
        'Pyramid of Giza Resonance',
        'Pyramid of Stellar Alignment',
        'Pyramid of Dimensional Gateway'
    ]

    for i in range(PYRAMID_COUNT):
        pyramid = {
            'pos': pyramid_positions[i],
            'freq': PYRAMID_RESONANCE_FREQ,  # 118 Hz
            'type': 'pyramid',
            'name': pyramid_names[i],
            'index': i,
            'desc': f'{pyramid_names[i]} - sacred resonance chamber at 118 Hz'
        }
        pyramids.append(pyramid)

    return pyramids


def generate_complete_universe():
    """
    Generate the complete Atlantean universe including all celestial bodies,
    temples, ley lines, and pyramids.

    Returns:
        Tuple of (stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids)
    """
    # Generate base celestial bodies
    stars, planets, nebulae, celestial_bodies = generate_all_celestial_bodies()

    # Generate Atlantean structures
    temples = generate_temples()
    ley_lines = generate_ley_lines(temples)
    pyramids = generate_pyramids()

    # Add temples and pyramids to celestial bodies for proximity detection
    celestial_bodies.extend(temples)
    celestial_bodies.extend(pyramids)

    return stars, planets, nebulae, celestial_bodies, temples, ley_lines, pyramids
