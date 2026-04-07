"""
Audio system for the Golden Spiral Spaceship Simulator.

This module handles all audio generation including the SoundEffect class,
waveform precomputation, vibrato effects, and the main audio callback
for real-time sound synthesis.
"""

import numpy as np
import sounddevice as sd
from constants import (
    SAMPLE_RATE, PHI, N_DIMENSIONS, POWER_BUILD_TIME,
    RIFT_CHARGE_TIME, ROTATION_SOUND_DURATION, SCHUMANN_FREQ,
    SCHUMANN_VOLUME, N_HARMONICS, HARMONIC_FALLOFF,
    SUBHARMONIC_DEPTH, INTERMOD_DEPTH, HARMONIC_RATIOS
)


# Vibrato constants for phase-modulated drive tones
VIBRATO_DEPTH_BASE = 0.25     # Base phase depth in radians (subtle wobble)
VIBRATO_DEPTH_MAX = 1.1       # Max phase depth when perfectly tuned
VIBRATO_RATE_BASE = 3.4       # Base LFO speed in Hz (nice slow golden pulse)
VIBRATO_RATE_MAX = 4.3        # Slightly faster when in perfect harmony


class SoundEffect:
    """
    Sound effect with spatial audio support.

    Manages a waveform with panning, pitch adjustment, looping, and volume.
    """

    def __init__(self, waveform, pan=0.0, pitch=1.0, loop=False, volume=1.0):
        """
        Initialize sound effect.

        Args:
            waveform: numpy array of audio samples
            pan: Stereo panning (-1 left to 1 right)
            pitch: Pitch multiplier (1.0 = normal)
            loop: Whether to loop the sound
            volume: Volume multiplier (0.0 to 1.0)
        """
        self.waveform = waveform * pitch  # Apply pitch to waveform
        self.position = 0  # Current playback position
        self.pan = pan  # Stereo panning (-1 left to 1 right)
        self.loop = loop  # Whether to loop the sound
        self.volume = volume  # Volume multiplier


class AudioSystem:
    """
    Manages all audio generation for the game.

    Handles waveform precomputation, audio callback, and volume management.
    """

    def __init__(self, config):
        """
        Initialize the audio system.

        Args:
            config: ConfigParser object with audio settings
        """
        # Audio timing
        self.audio_time = 0.0

        # Volume settings (loaded from config)
        self.master_volume = config.getfloat('Audio', 'master_volume', fallback=0.2)
        self.beep_volume = config.getfloat('Audio', 'beep_volume', fallback=0.3)
        self.effect_volume = config.getfloat('Audio', 'effect_volume', fallback=0.2)
        self.drive_volume = config.getfloat('Audio', 'drive_volume', fallback=0.05)

        # Active sound effects list
        self.active_sound_effects = []

        # Ship reference (set externally after ship is created)
        self.ship = None

        # Precompute all waveforms
        self._generate_waveforms()

        # Start audio stream
        self.stream = sd.OutputStream(
            callback=self._audio_callback,
            channels=2,
            samplerate=SAMPLE_RATE
        )

    def _generate_waveforms(self):
        """Precompute all static waveforms used in the game."""

        # Basic beep (for planets)
        beep_duration = 0.1
        beep_frequency = 440
        beep_samples = int(beep_duration * SAMPLE_RATE)
        self.beep_waveform = 0.2 * np.sin(
            2 * np.pi * beep_frequency * np.linspace(0, beep_duration, beep_samples)
        )

        # Rift beep (higher pitch)
        rift_beep_frequency = 880
        self.rift_beep_waveform = 0.2 * np.sin(
            2 * np.pi * rift_beep_frequency * np.linspace(0, beep_duration, beep_samples)
        )

        # Click sound (resonance feedback)
        click_duration = 0.05
        click_freq = 100 * PHI
        self.click_waveform = 0.2 * np.sin(
            2 * np.pi * click_freq * np.linspace(0, click_duration, int(click_duration * SAMPLE_RATE), endpoint=False)
        )

        # Rotation whoosh
        rotation_duration = ROTATION_SOUND_DURATION
        rotation_freq = 200 * PHI
        self.rotation_waveform = 0.1 * np.sin(
            2 * np.pi * rotation_freq * np.linspace(0, rotation_duration, int(rotation_duration * SAMPLE_RATE))
        )

        # Long Golden Harmony Chord — 7 seconds at 432 Hz (the frequency of the universe)
        chord_duration = 7.0
        chord_samples = int(chord_duration * SAMPLE_RATE)
        t_chord = np.linspace(0, chord_duration, chord_samples)

        # Gentle double swell over 7 seconds (breathes like a living thing)
        envelope = (np.sin(np.pi * t_chord / chord_duration) ** 2) * \
                   (0.85 + 0.15 * np.sin(2 * np.pi * t_chord / chord_duration * PHI))

        # 432 Hz A-major with subtle golden-ratio overtones
        base = 432.0
        self.chord_waveform = 0.11 * envelope * (
            np.sin(2 * np.pi * base * t_chord) +           # A4 @ 432 Hz
            np.sin(2 * np.pi * base * 1.25 * t_chord) +
            0.9 * np.sin(2 * np.pi * base * 1.5874 * t_chord) +  # C♯5 & E5 tuned to just intonation-ish ratios
            0.4 * np.sin(2 * np.pi * base * PHI * t_chord) +     # Golden overtone shimmer
            0.2 * np.sin(2 * np.pi * base * PHI**2 * t_chord)     # Even higher golden harmonic
        )

        # Rift hum (dimensional portal ambience)
        rift_hum_duration = 1.0
        rift_hum_base_freq = 220.0
        t_rift = np.linspace(0, rift_hum_duration, int(rift_hum_duration * SAMPLE_RATE))
        self.rift_hum_waveform = 0.1 * (
            np.sin(2 * np.pi * rift_hum_base_freq * t_rift) +
            0.5 * np.sin(2 * np.pi * rift_hum_base_freq * PHI * t_rift) +
            0.25 * np.sin(2 * np.pi * rift_hum_base_freq * PHI**2 * t_rift)
        )

        # Crystal lock beeps (mid to high tones)
        lock_beep_duration = 0.3
        mid_freq = 600
        high_freq = 1000
        lock_beep_samples = int(lock_beep_duration * SAMPLE_RATE)
        half = lock_beep_samples // 2
        t_mid = np.linspace(0, lock_beep_duration / 2, half)
        t_high = np.linspace(0, lock_beep_duration / 2, lock_beep_samples - half)
        self.lock_beep_waveform = np.concatenate((
            0.2 * np.sin(2 * np.pi * mid_freq * t_mid),
            0.2 * np.sin(2 * np.pi * high_freq * t_high)
        ))

        # Approaching lock beeps (mid tones, repeated)
        approaching_beep_duration = 0.15
        approaching_freq = 600
        approaching_beep_samples = int(approaching_beep_duration * SAMPLE_RATE)
        self.approaching_beep_waveform = 0.2 * np.sin(
            2 * np.pi * approaching_freq * np.linspace(0, approaching_beep_duration, approaching_beep_samples)
        )

        # Nebula dissonant rumble
        dissonant_duration = 1.0
        dissonant_freq = 40.0
        t_diss = np.linspace(0, dissonant_duration, int(dissonant_duration * SAMPLE_RATE))
        noise = np.random.rand(len(t_diss)) * 0.5 - 0.25  # Random noise
        self.dissonant_waveform = 0.1 * (np.sin(2 * np.pi * dissonant_freq * t_diss) + noise)

        # Perfect resonance ping
        ping_duration = 0.2
        ping_freq = 1200
        t_ping = np.linspace(0, ping_duration, int(ping_duration * SAMPLE_RATE))
        self.ping_waveform = 0.2 * np.sin(2 * np.pi * ping_freq * t_ping) * np.exp(-t_ping / 0.05)

        # Harmonic chimes (different frequencies for different harmonic types)
        chime_duration = 0.4
        t_chime = np.linspace(0, chime_duration, int(chime_duration * SAMPLE_RATE))
        decay = np.exp(-t_chime / 0.15)

        # Octave chime - C note at 523.25 Hz with octave overtone
        self.octave_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.5 * np.sin(2 * np.pi * 1046.5 * t_chime)  # Octave above
        )

        # Perfect fifth chime - C to G (523.25 Hz and 783.99 Hz)
        self.fifth_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.7 * np.sin(2 * np.pi * 783.99 * t_chime)
        )

        # Golden ratio chime - 432 Hz with PHI ratio overtone
        self.golden_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 432.0 * t_chime) +
            0.6 * np.sin(2 * np.pi * (432.0 * PHI) * t_chime) +
            0.3 * np.sin(2 * np.pi * (432.0 * PHI**2) * t_chime)
        )

        # Perfect fourth chime - C to F (523.25 Hz and 698.46 Hz)
        self.fourth_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.7 * np.sin(2 * np.pi * 698.46 * t_chime)
        )

        # Major third chime - C to E (523.25 Hz and 659.25 Hz)
        self.major_third_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.7 * np.sin(2 * np.pi * 659.25 * t_chime)
        )

        # Minor third chime - C to Eb (523.25 Hz and 622.25 Hz)
        self.minor_third_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.7 * np.sin(2 * np.pi * 622.25 * t_chime)
        )

        # Major sixth chime - C to A (523.25 Hz and 880 Hz)
        self.major_sixth_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.6 * np.sin(2 * np.pi * 880 * t_chime)
        )

        # Minor sixth chime - C to Ab (523.25 Hz and 830.6 Hz)
        self.minor_sixth_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.6 * np.sin(2 * np.pi * 830.6 * t_chime)
        )

        # Tritone chime - C to F# (523.25 Hz and 739.99 Hz) - dissonant!
        self.tritone_chime = 0.15 * decay * (
            np.sin(2 * np.pi * 523.25 * t_chime) +
            0.8 * np.sin(2 * np.pi * 739.99 * t_chime) +
            0.1 * np.sin(2 * np.pi * 261.63 * t_chime)  # Add low rumble for tension
        )

        # Stellar type ambient sounds
        # Red giant pulse (30-50 Hz deep bass pulsation)
        pulse_freq = 40.0
        pulse_duration = 2.0
        t_pulse = np.linspace(0, pulse_duration, int(pulse_duration * SAMPLE_RATE))
        pulse_envelope = (np.sin(np.pi * t_pulse / pulse_duration) ** 2)
        self.red_giant_pulse = 0.1 * pulse_envelope * np.sin(2 * np.pi * pulse_freq * t_pulse)

        # White dwarf whine (1200-1500 Hz high sustained tone)
        whine_freq = 1350.0
        whine_duration = 1.0
        t_whine = np.linspace(0, whine_duration, int(whine_duration * SAMPLE_RATE))
        self.white_dwarf_whine = 0.08 * np.sin(2 * np.pi * whine_freq * t_whine)

        # Brown dwarf rumble (20-30 Hz barely audible deep rumble)
        rumble_freq = 25.0
        rumble_duration = 1.5
        t_rumble = np.linspace(0, rumble_duration, int(rumble_duration * SAMPLE_RATE))
        self.brown_dwarf_rumble = 0.05 * np.sin(2 * np.pi * rumble_freq * t_rumble)

        # Nebula type ambient sounds
        nebula_duration = 1.5
        t_nebula = np.linspace(0, nebula_duration, int(nebula_duration * SAMPLE_RATE))

        # Emission nebula - warm drone (200-300 Hz)
        emission_freq = 250.0
        self.emission_nebula_drone = 0.08 * (
            np.sin(2 * np.pi * emission_freq * t_nebula) +
            0.3 * np.sin(2 * np.pi * emission_freq * 1.5 * t_nebula)
        )

        # Reflection nebula - cool shimmer (600-800 Hz with subtle tremolo)
        reflection_freq = 700.0
        tremolo = 0.8 + 0.2 * np.sin(2 * np.pi * 4.0 * t_nebula)  # 4 Hz tremolo
        self.reflection_nebula_shimmer = 0.06 * tremolo * (
            np.sin(2 * np.pi * reflection_freq * t_nebula) +
            0.4 * np.sin(2 * np.pi * reflection_freq * PHI * t_nebula)
        )

        # Planetary nebula - multi-layered (400-600 Hz with harmonics)
        planetary_freq = 500.0
        self.planetary_nebula_layers = 0.07 * (
            np.sin(2 * np.pi * planetary_freq * t_nebula) +
            0.5 * np.sin(2 * np.pi * planetary_freq * 1.25 * t_nebula) +
            0.3 * np.sin(2 * np.pi * planetary_freq * 1.5 * t_nebula)
        )

        # Supernova remnant - chaotic noise (100-900 Hz sweeping with noise)
        noise = np.random.rand(len(t_nebula)) * 0.6 - 0.3
        sweep_freq = 200 + 700 * np.sin(2 * np.pi * 0.5 * t_nebula)  # 0.5 Hz sweep
        self.supernova_remnant_chaos = 0.1 * (
            np.sin(2 * np.pi * sweep_freq * t_nebula) + noise
        )

        # Exoplanet type ambient sounds
        planet_duration = 1.0
        t_planet = np.linspace(0, planet_duration, int(planet_duration * SAMPLE_RATE))

        # Hot Jupiter - roaring furnace (200-500 Hz with heavy noise and modulation)
        hot_jupiter_noise = np.random.rand(len(t_planet)) * 0.8 - 0.4
        hot_jupiter_mod = 300 + 200 * np.sin(2 * np.pi * 3.0 * t_planet)
        self.hot_jupiter_roar = 0.09 * (
            np.sin(2 * np.pi * hot_jupiter_mod * t_planet) + hot_jupiter_noise
        )

        # Super-Earth - solid resonant tone (300-400 Hz stable fundamental)
        super_earth_freq = 350.0
        self.super_earth_tone = 0.07 * (
            np.sin(2 * np.pi * super_earth_freq * t_planet) +
            0.3 * np.sin(2 * np.pi * super_earth_freq * 2 * t_planet)
        )

        # Ocean World - flowing liquid (200-350 Hz with gentle undulation)
        ocean_flow = 0.9 + 0.1 * np.sin(2 * np.pi * 2.0 * t_planet)
        ocean_freq = 275.0
        self.ocean_world_flow = 0.06 * ocean_flow * (
            np.sin(2 * np.pi * ocean_freq * t_planet) +
            0.5 * np.sin(2 * np.pi * ocean_freq * 1.3 * t_planet)
        )

        # Rogue Planet - ominous silence (very low 50 Hz rumble, barely audible)
        rogue_freq = 50.0
        self.rogue_planet_ominous = 0.03 * np.sin(2 * np.pi * rogue_freq * t_planet)

        # Ice Giant - crystalline chimes (600-1000 Hz with bell-like harmonics)
        ice_freq = 800.0
        ice_decay = np.exp(-t_planet / 0.2)
        self.ice_giant_chime = 0.06 * ice_decay * (
            np.sin(2 * np.pi * ice_freq * t_planet) +
            0.4 * np.sin(2 * np.pi * ice_freq * 1.5 * t_planet) +
            0.2 * np.sin(2 * np.pi * ice_freq * 2 * t_planet)
        )

    def get_vibrato_phase(self, t, resonance_level):
        """
        Generate phase-modulated vibrato that responds to resonance quality.

        The vibrato gets deeper and slightly faster with higher resonance,
        creating a richer, more alive sound when properly tuned.

        Args:
            t: Time array (in seconds)
            resonance_level: 0.0 to 1.0 (tuning quality)

        Returns:
            Phase offset array to add to carrier wave
        """
        depth = VIBRATO_DEPTH_BASE + (VIBRATO_DEPTH_MAX - VIBRATO_DEPTH_BASE) * resonance_level
        rate = VIBRATO_RATE_BASE + (VIBRATO_RATE_MAX - VIBRATO_RATE_BASE) * resonance_level**2

        # Two layered LFOs at golden-ratio intervals for organic beating
        lfo1 = np.sin(2 * np.pi * rate * t)
        lfo2 = np.sin(2 * np.pi * rate * PHI * t) * 0.3
        return depth * (lfo1 + lfo2)

    def detect_harmonic_pairs(self):
        """
        Detect harmonic relationships between drive frequencies.

        Returns a list of (dim1, dim2, harmonic_name) tuples for all
        detected harmonic relationships.

        Returns:
            List of harmonic relationship tuples
        """
        if self.ship is None:
            return []

        harmonic_pairs = []
        for i in range(N_DIMENSIONS):
            for j in range(i + 1, N_DIMENSIONS):
                freq1 = self.ship.r_drive[i]
                freq2 = self.ship.r_drive[j]

                # Check both orderings (freq2/freq1 and freq1/freq2)
                ratio = freq2 / freq1 if freq1 > 0 else 0
                inv_ratio = freq1 / freq2 if freq2 > 0 else 0

                for name, target_ratio in HARMONIC_RATIOS.items():
                    # Check if ratio matches within tolerance
                    tolerance = target_ratio * 0.02  # 2% tolerance
                    if abs(ratio - target_ratio) < tolerance:
                        harmonic_pairs.append((i, j, name))
                        break
                    elif abs(inv_ratio - target_ratio) < tolerance:
                        harmonic_pairs.append((j, i, name))
                        break

        return harmonic_pairs

    def _audio_callback(self, outdata, frames, time, status):
        """
        Real-time audio generation callback.

        Generates drive signals for all dimensions, mixes sound effects,
        and outputs stereo audio.

        Args:
            outdata: Output buffer to fill
            frames: Number of frames to generate
            time: Time info from sounddevice
            status: Status flags
        """
        if self.ship is None:
            # No ship yet, output silence
            outdata[:] = np.zeros((frames, 2))
            return

        # Generate time array
        t = (np.arange(frames) / SAMPLE_RATE) + self.audio_time
        self.audio_time += frames / SAMPLE_RATE

        # Silent Schumann carrier wave (7.83 Hz at -40 dB)
        schumann_wave = SCHUMANN_VOLUME * np.sin(2 * np.pi * SCHUMANN_FREQ * t)

        # Detect harmonic relationships between dimensions
        harmonic_pairs = self.detect_harmonic_pairs()

        # Generate drive signals per dimension with enhanced harmonics
        signals = np.zeros((N_DIMENSIONS, frames))
        for i in range(N_DIMENSIONS):
            base_freq = self.ship.r_drive[i] / 2

            # Per-dimension resonance (makes vibrato respond to how well that dim is tuned)
            delta_f = self.ship.r_drive[i] - self.ship.f_target[i]
            res_level = 1 / (1 + (delta_f / self.ship.resonance_width)**2)

            # Subtle vibrato as phase modulation
            vibrato_phase = self.get_vibrato_phase(t, res_level)

            # Pure sine fundamental - clean and lifelike
            signals[i] += self.drive_volume * np.sin(
                2 * np.pi * base_freq * t + vibrato_phase
            )

            # Golden ratio overtones for organic shimmer (PHI^1, PHI^2, PHI^3)
            # These create the "lifelike" quality without harsh sawtooth harmonics
            for k in range(1, 4):
                amplitude = self.drive_volume * 0.25 / k  # Gentle falloff
                signals[i] += amplitude * np.sin(
                    2 * np.pi * (base_freq * PHI**k) * t + vibrato_phase
                )

            # Subharmonic at golden ratio below (1/PHI) for warmth
            sub_freq = base_freq / PHI
            signals[i] += self.drive_volume * 0.15 * np.sin(
                2 * np.pi * sub_freq * t + vibrato_phase * 0.5
            )

            # Add modulation to higher dimensions
            if i >= 3:
                mod_freq = 0.5 * PHI
                mod = np.sin(2 * np.pi * mod_freq * t) * 0.05
                signals[i] *= (1 + mod)

        # Generate intermodulation tones for harmonically-related dimensions
        for dim1, dim2, harmonic_name in harmonic_pairs:
            freq1 = self.ship.r_drive[dim1] / 2
            freq2 = self.ship.r_drive[dim2] / 2

            # Sum and difference tones (classic intermodulation)
            sum_freq = freq1 + freq2
            diff_freq = abs(freq1 - freq2)

            # Add intermodulation to both dimensions
            intermod_signal = INTERMOD_DEPTH * self.drive_volume * (
                np.sin(2 * np.pi * sum_freq * t) * 0.5 +
                np.sin(2 * np.pi * diff_freq * t) * 0.7
            )
            signals[dim1] += intermod_signal
            signals[dim2] += intermod_signal

        # Pan signals: x left, y center, z right, higher dims mixed
        left_signal = signals[0] + signals[1] * 0.5 + signals[3] * 0.7 + signals[4] * 0.3
        right_signal = signals[2] + signals[1] * 0.5 + signals[3] * 0.3 + signals[4] * 0.7

        # Add ambient modulation
        modulation = 0.5 + 0.5 * np.sin(2 * np.pi * 0.1 * PHI * t)
        ambient_signal = 0.01 * modulation * np.sin(2 * np.pi * 30 * PHI * t)
        left_signal += ambient_signal
        right_signal += ambient_signal

        # Add power chord if power buildup high
        power_condition = not self.ship.landed_mode and any(
            self.ship.resonance_power[i] > POWER_BUILD_TIME - 1 for i in range(N_DIMENSIONS)
        )
        chord_effects = [e for e in self.active_sound_effects if np.array_equal(e.waveform, self.chord_waveform)]
        if power_condition:
            if not chord_effects:
                self.active_sound_effects.append(SoundEffect(self.chord_waveform, pan=0.0, volume=self.effect_volume))
        elif chord_effects:
            for e in chord_effects:
                self.active_sound_effects.remove(e)

        # Add rift charge rising tone
        if self.ship.rift_charge_timer > 0:
            charge_progress = (RIFT_CHARGE_TIME - self.ship.rift_charge_timer) / RIFT_CHARGE_TIME
            charge_freq = 220 + 660 * charge_progress  # Rise from low to high
            charge_wave = 0.1 * np.sin(2 * np.pi * charge_freq * t) * self.effect_volume
            left_signal += charge_wave
            right_signal += charge_wave

        # Mix active sound effects
        for effect in self.active_sound_effects[:]:
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
                    self.active_sound_effects.remove(effect)

        # Apply master volume and clip
        left_signal *= self.master_volume
        right_signal *= self.master_volume

        # Add Schumann to final output
        left_signal += schumann_wave
        right_signal += schumann_wave

        # Create stereo output and clip to valid range
        signal = np.stack((left_signal, right_signal), axis=1)
        signal = np.clip(signal, -1.0, 1.0)
        outdata[:] = signal

    def start(self):
        """Start the audio stream."""
        self.stream.start()

    def stop(self):
        """Stop and close the audio stream."""
        self.stream.stop()
        self.stream.close()

    def set_ship(self, ship):
        """Set the ship reference for audio callback."""
        self.ship = ship
