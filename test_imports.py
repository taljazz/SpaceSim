"""
Quick test to verify all modular imports work correctly.
Run this before running the full game to catch import errors early.
"""

print("Testing modular imports...")

try:
    print("  ✓ Importing constants...")
    from constants import *

    print("  ✓ Importing utils...")
    from utils import project_to_2d

    print("  ✓ Importing celestial...")
    from celestial import generate_celestial, generate_all_celestial_bodies

    print("  ✓ Importing audio_system...")
    from audio_system import AudioSystem, SoundEffect

    print("  ✓ Importing ship...")
    from ship import Ship

    print("\n✅ All imports successful!")
    print("\nYou can now run: python main.py")

except ImportError as e:
    print(f"\n❌ Import error: {e}")
    print("\nMake sure you have activated the conda environment:")
    print("  conda activate ss")
    print("\nAnd installed all dependencies:")
    print("  conda install pygame numpy")
    print("  pip install sounddevice cytolk")
except Exception as e:
    print(f"\n❌ Unexpected error: {e}")
