"""
Utility functions for the Golden Spiral Spaceship Simulator.

This module contains helper functions for projection, speech output,
and other common operations used throughout the game.
"""

import numpy as np
from cytolk import tolk
from constants import SCREEN_WIDTH, SCREEN_HEIGHT, SPEECH_COOLDOWN


def speak_with_cooldown(msg, simulation_time, last_spoken):
    """
    Speak message via Tolk if cooldown has elapsed.

    Args:
        msg: Message to speak
        simulation_time: Current simulation time
        last_spoken: Dictionary tracking when each message was last spoken

    Returns:
        Updated last_spoken dictionary
    """
    if msg not in last_spoken or simulation_time - last_spoken[msg] > SPEECH_COOLDOWN:
        tolk.speak(msg)
        last_spoken[msg] = simulation_time
    return last_spoken


def project_to_2d(pos, rotation, screen_size=None):
    """
    Project 5D position to 2D screen coordinates.

    Projects higher dimensions into 2D using rotation, mixing dimensions
    3 and 4 (the higher dimensions) with spatial dimensions 0 and 1 based
    on the view rotation angle.

    Args:
        pos: numpy array of position in 5 dimensions
        rotation: View rotation angle in radians
        screen_size: Optional tuple of (width, height). If None, uses constants.

    Returns:
        Tuple of (screen_x, screen_y) pixel coordinates
    """
    if screen_size is None:
        width, height = SCREEN_WIDTH, SCREEN_HEIGHT
    else:
        width, height = screen_size

    cos_r = np.cos(rotation)
    sin_r = np.sin(rotation)
    x = pos[0] * cos_r + pos[3] * sin_r
    y = pos[1] * cos_r + pos[4] * sin_r
    screen_x = (x + 100) / 200 * width
    screen_y = (y + 100) / 200 * height
    return (int(screen_x), int(screen_y))
