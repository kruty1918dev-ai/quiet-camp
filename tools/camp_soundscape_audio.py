#!/usr/bin/env python3
"""Owned, deterministic 48 kHz camping sound design; no downloaded samples.

Rain combines smooth air with irregular, filtered surface impulses. Loops are
circular signals, not long one-shots with a silent gap. --audit writes a PCM
quality report; it does not claim perceptual or device-listening validation.
"""
import argparse
import json
import pathlib
import wave
import numpy as np
from scipy.signal import butter, sosfilt

ROOT = pathlib.Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'QuietCamp/Assets/QuietCamp/Audio/Generated/Soundscape'
RATE = 48000


def band_noise(n, rng, low, high):
    # Frequency-domain filtering produces a periodic, DC-free noise bed.
    frequency = np.fft.rfftfreq(n, 1 / RATE)
    spectrum = np.fft.rfft(rng.normal(size=n))
    weight = 1 / np.sqrt(1 + (frequency / high) ** 8)
    if low:
        weight *= 1 / np.sqrt(1 + (low / np.maximum(frequency, .01)) ** 8)
    weight[0] = 0
    result = np.fft.irfft(spectrum * weight, n=n)
    return result / max(.001, np.std(result))


def periodic_add(signal, onset, grain):
    start = int(onset * RATE) % len(signal)
    count = min(len(grain), len(signal) - start)
    signal[start:start + count] += grain[:count]
    if count < len(grain):
        signal[:len(grain) - count] += grain[count:]


def rain(canvas=False):
    rng = np.random.default_rng(31818 if canvas else 31817)
    length = RATE * (16 if canvas else 24)
    channels = []
    shared = band_noise(length, rng, 90, 650 if canvas else 1500)
    for channel in range(1 if canvas else 2):
        bed = .10 * shared + .08 * band_noise(length, rng, 550, 4200)
        for onset in rng.uniform(0, length / RATE, 1500 if canvas else 2400):
            t = np.arange(int(RATE * .11)) / RATE
            frequency = rng.uniform(220, 510) if canvas else rng.uniform(650, 1500)
            envelope = (1 - np.exp(-t * 1200)) * np.exp(-t * rng.uniform(55, 100))
            grain = (np.sin(2 * np.pi * frequency * t) * (.7 if canvas else .16)
                     + sosfilt(butter(2, 3500, fs=RATE, output='sos'), rng.normal(size=len(t))) * .24)
            periodic_add(bed, onset, grain * envelope * rng.uniform(.10, .32))
        channels.append(bed)
    return channels[0] if canvas else np.column_stack(channels)


def detail(kind, variant):
    rng = np.random.default_rng(191819 + sum(map(ord, kind)) + variant * 167)
    duration = {'fabric_gust': .95, 'fire_quench': 1.9, 'drip': .42,
                'bird': .78, 'complete': 2.8}[kind]
    t = np.arange(int(duration * RATE)) / RATE
    if kind == 'bird':
        result = np.zeros_like(t)
        for k in range(2 + variant % 2):
            q = t - .08 - k * .15
            length = .10 + variant * .009
            x = np.clip(q / length, 0, 1)
            frequency = (1600 + variant * 140) + 900 * np.sin(x * np.pi)
            phase = 2 * np.pi * np.cumsum(frequency) / RATE
            envelope = np.where((q > 0) & (q < length), np.sin(np.pi * x) ** 2, 0)
            result += (np.sin(phase) + .045 * np.sin(phase * 2)) * envelope
        return result
    if kind == 'complete':
        result = np.zeros_like(t)
        for k, frequency in enumerate((196, 293.665, 392)):
            q = np.maximum(0, t - k * .20)
            envelope = np.where(t >= k * .20, (1 - np.exp(-q * 45)) * np.exp(-q * 2.6), 0)
            result += (np.sin(2 * np.pi * frequency * q)
                       + .14 * np.sin(2 * np.pi * frequency * 2.006 * q)) * envelope
        return result
    low, high = {'fabric_gust': (100, 1800), 'fire_quench': (350, 6500), 'drip': (180, 2100)}[kind]
    texture = band_noise(len(t), rng, low, high)
    if kind == 'fabric_gust':
        envelope = np.sin(np.pi * t / duration) ** 2
        result = texture * (.35 + .18 * np.sin(2 * np.pi * 11 * t))
        result += .12 * np.sin(2 * np.pi * (115 + 12 * variant) * t) * np.exp(-t * 4)
    elif kind == 'fire_quench':
        envelope = (1 - np.exp(-t * 12)) * np.exp(-t * 2.9)
        result = texture * (.40 + .10 * np.sin(2 * np.pi * 33 * t))
    else:
        envelope = (1 - np.exp(-t * 750)) * np.exp(-t * 24)
        phase = 2 * np.pi * np.cumsum(620 + variant * 75 + 700 * np.exp(-t * 40)) / RATE
        result = np.sin(phase) + texture * .12
    return result * envelope


def write(name, signal, loop=False):
    signal = np.asarray(signal, dtype=np.float64)
    signal -= np.mean(signal, axis=0)
    if not loop:
        edge = 480
        fade = np.sin(np.linspace(0, np.pi / 2, edge)) ** 2
        signal[:edge] *= fade[:, None] if signal.ndim == 2 else fade
        signal[-edge:] *= fade[::-1, None] if signal.ndim == 2 else fade[::-1]
        window = np.sin(np.linspace(0, np.pi, len(signal))) ** 2
        correction = np.sum(signal, axis=0) / np.sum(window)
        signal -= window[:, None] * correction if signal.ndim == 2 else window * correction
    signal *= (.40 if loop else .60) / max(.001, np.max(np.abs(signal)))
    with wave.open(str(FOLDER / name), 'wb') as file:
        file.setnchannels(2 if signal.ndim == 2 else 1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(np.round(signal * 32767).astype('<i2').tobytes())


def audit():
    results = []
    for path in sorted(FOLDER.glob('*.wav')):
        with wave.open(str(path)) as file:
            channels = file.getnchannels()
            samples = np.frombuffer(file.readframes(file.getnframes()), '<i2').astype(float).reshape(-1, channels) / 32768
            loop = path.stem in ('rain_canopy', 'rain_canvas')
            seam = float(np.max(np.abs(samples[-1] - samples[0])))
            step = float(np.percentile(np.abs(np.diff(samples, axis=0)), 99.9))
            row = dict(file=path.name, rate=file.getframerate(), channels=channels,
                       seconds=round(len(samples) / RATE, 3), loop=loop,
                       peak_dbfs=round(20 * np.log10(max(1e-9, np.max(np.abs(samples)))), 2),
                       rms_dbfs=round(20 * np.log10(max(1e-9, np.sqrt(np.mean(samples ** 2)))), 2),
                       dc=float(np.max(np.abs(np.mean(samples, axis=0)))), seam=seam)
            assert row['rate'] == RATE and row['peak_dbfs'] < -4 and row['dc'] < .001
            assert seam <= max(.001, step * 2) if loop else np.max(np.abs(samples[[0, -1]])) < .001
            results.append(row)
    assert len(results) == 14, len(results)
    print(json.dumps(results, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit', action='store_true')
    args = parser.parse_args()
    if not args.audit:
        FOLDER.mkdir(parents=True, exist_ok=True)
        write('rain_canopy.wav', rain(), True)
        write('rain_canvas.wav', rain(True), True)
        for kind, count in (('fabric_gust', 3), ('fire_quench', 2), ('drip', 3), ('bird', 3), ('complete', 1)):
            for variant in range(count):
                write(f'{kind}_{variant}.wav', detail(kind, variant))
    audit()
