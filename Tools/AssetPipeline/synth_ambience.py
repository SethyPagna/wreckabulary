from __future__ import annotations

import argparse
from array import array
import json
import math
from pathlib import Path
import random
import statistics
import sys
import wave

RATE = 44_100
REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = REPO / "Assets/_Project/Resources/Ambience/maple_close"
SEED = 20261115
LOOP_SECONDS = 60.0
LOOP_CROSSFADE = 3.0
BED_PEAK_DB = -14.0
SHOT_PEAK_DB = -6.0
TAU = math.tau


def alpha(hz):
    return 1.0 - math.exp(-TAU * hz / RATE)


def lowpass(values, hz, passes=1):
    a = alpha(hz)
    for _ in range(passes):
        state = 0.0
        out = array("d", bytes(8 * len(values)))
        for index, sample in enumerate(values):
            state += a * (sample - state)
            out[index] = state
        values = out
    return values


def highpass(values, hz):
    low = lowpass(values, hz)
    return array("d", (a - b for a, b in zip(values, low)))


def bandpass(values, low, high, passes=2):
    return highpass(lowpass(values, high, passes), low)


def sweep_lowpass(values, cutoffs, passes=2):
    for _ in range(passes):
        state = 0.0
        out = array("d", bytes(8 * len(values)))
        for index, sample in enumerate(values):
            state += (1.0 - math.exp(-TAU * cutoffs[index] / RATE)) * (sample - state)
            out[index] = state
        values = out
    return values


def white(rng, count):
    uniform = rng.uniform
    return array("d", (uniform(-1.0, 1.0) for _ in range(count)))


def pink(rng, count):
    b0 = b1 = b2 = 0.0
    out = array("d", bytes(8 * count))
    uniform = rng.uniform
    for index in range(count):
        w = uniform(-1.0, 1.0)
        b0 = 0.99765 * b0 + w * 0.0990460
        b1 = 0.96300 * b1 + w * 0.2965164
        b2 = 0.57000 * b2 + w * 1.0526913
        out[index] = (b0 + b1 + b2 + w * 0.1848) * 0.2
    return out


def brown(rng, count):
    state = 0.0
    out = array("d", bytes(8 * count))
    uniform = rng.uniform
    for index in range(count):
        state = 0.998 * state + uniform(-1.0, 1.0) * 0.06
        out[index] = state
    return out


def rms(values):
    return math.sqrt(sum(v * v for v in values) / max(1, len(values)))


def unit(values):
    level = rms(values)
    return array("d", (v / level for v in values)) if level > 0 else values


def remove_dc(values):
    mean = sum(values) / max(1, len(values))
    return array("d", (v - mean for v in values))


def normalise(values, peak_db):
    peak = max(abs(v) for v in values)
    gain = 10 ** (peak_db / 20) / peak if peak > 0 else 1.0
    return array("d", (v * gain for v in values))


def edge_fade(values, attack, release):
    count = len(values)
    rise, fall = max(1, int(attack * RATE)), max(1, int(release * RATE))
    out = array("d", values)
    for index in range(min(rise, count)):
        out[index] *= 0.5 - 0.5 * math.cos(math.pi * index / rise)
    for index in range(min(fall, count)):
        out[count - 1 - index] *= 0.5 - 0.5 * math.cos(math.pi * index / fall)
    return out


def bump(t, centre, width):
    return math.exp(-0.5 * ((t - centre) / width) ** 2)


def reverb(values, wet, rng, decay=0.72, damping=0.35):
    tail = int(0.9 * RATE)
    dry = array("d", values) + array("d", bytes(8 * tail))
    count = len(dry)
    combs = [0.0297, 0.0371, 0.0411, 0.0437]
    mixed = array("d", bytes(8 * count))
    for seconds in combs:
        delay = int(seconds * RATE * rng.uniform(0.97, 1.03))
        buffer = array("d", bytes(8 * delay))
        position = 0
        smooth = 0.0
        for index in range(count):
            delayed = buffer[position]
            smooth += (1.0 - damping) * (delayed - smooth)
            buffer[position] = dry[index] + smooth * decay
            mixed[index] += delayed * 0.25
            position += 1
            if position == delay:
                position = 0
    for seconds, gain in ((0.005, 0.7), (0.0017, 0.7)):
        delay = max(1, int(seconds * RATE))
        buffer = array("d", bytes(8 * delay))
        position = 0
        out = array("d", bytes(8 * count))
        for index in range(count):
            delayed = buffer[position]
            incoming = mixed[index] + delayed * gain
            buffer[position] = incoming
            out[index] = delayed - incoming * gain
            position += 1
            if position == delay:
                position = 0
        mixed = out
    return array("d", (d + wet * m for d, m in zip(dry, mixed)))


def periodic_bumps(rng, count, period, bursts, widths, levels):
    events = [(rng.uniform(0, period), rng.uniform(*widths), rng.uniform(*levels)) for _ in range(bursts)]
    out = array("d", bytes(8 * count))
    for centre, width, level in events:
        reach = int(4 * width * RATE)
        for shift in (-period, 0.0, period):
            middle = int((centre + shift) * RATE)
            for index in range(max(0, middle - reach), min(count, middle + reach)):
                out[index] += level * bump(index / RATE, centre + shift, width)
    return out


def garden_loop(rng):
    period = int(LOOP_SECONDS * RATE)
    overlap = int(LOOP_CROSSFADE * RATE)
    preroll = RATE
    total = period + overlap + preroll
    air = unit(highpass(highpass(lowpass(pink(rng, total), 1400, 2), 120), 120))[preroll:]
    leaves = unit(bandpass(white(rng, total), 2300, 6200))[preroll:]
    hum = unit(highpass(lowpass(brown(rng, total), 220, 2), 60))[preroll:]
    count = period + overlap
    phases = [rng.uniform(0, TAU) for _ in range(3)]
    rustle = periodic_bumps(rng, count, LOOP_SECONDS, 46, (0.12, 0.55), (0.25, 1.0))
    mix = array("d", bytes(8 * count))
    for index in range(count):
        u = index / period
        swell = 0.6 + 0.2 * math.sin(TAU * 3 * u + phases[0]) + 0.12 * math.sin(TAU * 7 * u + phases[1]) + 0.08 * math.sin(TAU * 13 * u + phases[2])
        mix[index] = air[index] * swell * 0.55 + leaves[index] * min(1.6, rustle[index]) * swell * swell * 0.2 + hum[index] * 0.12
    loop = array("d", mix[:period])
    for index in range(overlap):
        fade = index / overlap
        loop[index] = mix[index] * math.sin(0.5 * math.pi * fade) + mix[period + index] * math.cos(0.5 * math.pi * fade)
    return normalise(remove_dc(loop), BED_PEAK_DB)


def tone(duration, pitch, amplitude, harmonics=((1, 1.0),), vibrato=(0.0, 0.0), attack=0.012, release=0.03):
    count = int(duration * RATE)
    out = array("d", bytes(8 * count))
    phase = 0.0
    rate, depth = vibrato
    for index in range(count):
        u = index / max(1, count - 1)
        t = index / RATE
        frequency = pitch(u) + depth * math.sin(TAU * rate * t)
        phase += TAU * frequency / RATE
        env = soft(t / attack) * soft((duration - t) / release) * amplitude(u)
        out[index] = env * sum(level * math.sin(ratio * phase) for ratio, level in harmonics)
    return out


def soft(x):
    return 0.5 - 0.5 * math.cos(math.pi * min(1.0, max(0.0, x)))


def place(buffer, sound, at):
    start = int(at * RATE)
    for index, sample in enumerate(sound):
        if start + index < len(buffer):
            buffer[start + index] += sample


def glide(start, end, curve=1.0):
    return lambda u: start + (end - start) * (u ** curve)


def swell_shape(u):
    return math.sin(math.pi * u) ** 1.3


def finish_call(buffer, rng, wet, high):
    shaped = lowpass(highpass(buffer, 300), high)
    spaced = reverb(shaped, wet, rng)
    return normalise(edge_fade(remove_dc(spaced), 0.004, 0.35), SHOT_PEAK_DB)


def bird_whistles(rng):
    buffer = array("d", bytes(8 * int(1.6 * RATE)))
    at = 0.12
    for note in range(3):
        start = rng.uniform(4600, 4900) - note * 180
        place(buffer, tone(0.11, glide(start, start - 900, 0.7), swell_shape, ((1, 1.0), (2, 0.06)), (38, 60)), at)
        at += 0.11 + rng.uniform(0.07, 0.1)
    return finish_call(buffer, rng, 0.22, 7500)


def bird_warble(rng):
    buffer = array("d", bytes(8 * int(2.2 * RATE)))
    at = 0.1
    for _ in range(7):
        low = rng.uniform(2500, 3300)
        high = low + rng.uniform(500, 1300)
        duration = rng.uniform(0.07, 0.15)
        pitch = glide(low, high, 0.6) if rng.random() < 0.5 else glide(high, low, 1.4)
        place(buffer, tone(duration, pitch, swell_shape, ((1, 1.0), (2, 0.08)), (rng.uniform(55, 80), rng.uniform(120, 260))), at)
        at += duration + rng.uniform(0.03, 0.09)
    return finish_call(buffer, rng, 0.2, 7000)


def dove_coo(rng):
    buffer = array("d", bytes(8 * int(2.6 * RATE)))
    breath = bandpass(white(rng, len(buffer)), 350, 1100)
    pattern = ((0.24, 505, 535, 0.7), (0.52, 540, 500, 1.0), (0.3, 500, 470, 0.75))
    at = 0.15
    for duration, start, end, level in pattern:
        note = tone(duration, glide(start, end), lambda u, level=level: level * math.sin(math.pi * u) ** 1.2,
                    ((1, 1.0), (2, 0.22), (3, 0.06)), (5.5, 4.0), attack=0.05, release=0.09)
        base = int(at * RATE)
        for index in range(len(note)):
            if base + index < len(buffer):
                note[index] += breath[base + index] * 0.02 * level * math.sin(math.pi * index / len(note))
        place(buffer, note, at)
        at += duration + (0.12 if duration < 0.4 else 0.2)
    shaped = lowpass(highpass(buffer, 180), 2200)
    return normalise(edge_fade(remove_dc(reverb(shaped, 0.24, rng)), 0.01, 0.4), SHOT_PEAK_DB - 1.5)


def sparrow_chips(rng):
    buffer = array("d", bytes(8 * int(1.8 * RATE)))
    at = 0.08
    for chip in range(11):
        centre = rng.uniform(3800, 5200)
        duration = rng.uniform(0.022, 0.038)
        place(buffer, tone(duration, glide(centre + 700, centre - 500, 0.5), swell_shape, ((1, 1.0), (2, 0.12)), attack=0.003, release=0.01),
              at)
        at += duration + (rng.uniform(0.04, 0.07) if chip % 4 != 3 else rng.uniform(0.14, 0.2))
    return finish_call(buffer, rng, 0.2, 8000)


def car_pass(rng, seconds, closest, width, engine, speed):
    count = int(seconds * RATE)
    centre = closest * seconds
    nearness = [1.0 / (1.0 + ((index / RATE - centre) / width) ** 2) for index in range(count)]
    cutoffs = [240 + 1250 * n ** 0.8 for n in nearness]
    body = unit(highpass(highpass(sweep_lowpass(pink(rng, count), cutoffs), 90), 90))
    hiss = unit(bandpass(white(rng, count), 1300, 3600))
    ratio = speed / 343.0
    out = array("d", bytes(8 * count))
    phase = 0.0
    engine_state = 0.0
    for index in range(count):
        t = index / RATE
        n = nearness[index]
        doppler = 1.0 - ratio * math.tanh((t - centre) / (width * 0.55))
        phase += TAU * engine * doppler / RATE
        hum = 0.5 * math.sin(phase) + 0.3 * math.sin(2 * phase + 0.4) + 0.15 * math.sin(3 * phase + 1.1)
        engine_state += alpha(cutoffs[index]) * (hum - engine_state)
        out[index] = body[index] * n + hiss[index] * n ** 1.5 * 0.12 + engine_state * n * 0.35
    return normalise(edge_fade(remove_dc(out), 0.6, 0.9), SHOT_PEAK_DB - 2.0)


def wind_gust(rng):
    seconds = 6.5
    count = int(seconds * RATE)
    envelope = []
    for index in range(count):
        t = index / RATE
        rise = 0.5 - 0.5 * math.cos(math.pi * min(1.0, t / 2.3))
        fall = 0.5 - 0.5 * math.cos(math.pi * min(1.0, (seconds - t) / 3.4))
        flutter = 1.0 + 0.12 * math.sin(TAU * 0.7 * t) + 0.08 * math.sin(TAU * 1.9 * t + 1.3)
        envelope.append(rise * fall * flutter)
    cutoffs = [280 + 950 * e for e in envelope]
    air = unit(highpass(sweep_lowpass(pink(rng, count), cutoffs), 110))
    leaves = unit(bandpass(white(rng, count), 2400, 6000))
    rustle = periodic_bumps(rng, count, seconds, 24, (0.06, 0.25), (0.3, 1.0))
    out = array("d", (air[i] * envelope[i] + leaves[i] * envelope[i] ** 2 * min(1.4, rustle[i]) * 0.12 for i in range(count)))
    return normalise(edge_fade(remove_dc(out), 0.3, 0.6), SHOT_PEAK_DB - 1.0)


def write(path, values, rng):
    data = array("h", bytes(2 * len(values)))
    for index, value in enumerate(values):
        dithered = value * 32767.0 + rng.random() - rng.random()
        data[index] = max(-32768, min(32767, int(round(dithered))))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(data.tobytes())
    return data


def report(name, data, loop):
    values = [v / 32768.0 for v in data]
    peak = max(abs(v) for v in values)
    level = rms(values)
    entry = {
        "file": name,
        "seconds": round(len(values) / RATE, 3),
        "peak_dbfs": round(20 * math.log10(peak), 2) if peak else None,
        "rms_dbfs": round(20 * math.log10(level), 2) if level else None,
        "dc": round(sum(values) / len(values), 6),
        "edge_start": round(abs(values[0]), 5),
        "edge_end": round(abs(values[-1]), 5),
    }
    if loop:
        steps = [abs(values[i] - values[i - 1]) for i in range(1, len(values), 97)]
        entry["seam_step"] = round(abs(values[0] - values[-1]), 6)
        entry["median_step"] = round(statistics.median(steps), 6)
    return entry


def master(path, loop):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import audio_process
    audio_process.master_generated(path, loop)
    with wave.open(str(path), "rb") as handle:
        return array("h", handle.readframes(handle.getnframes()))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--raw", action="store_true")
    args = parser.parse_args()
    sounds = (
        ("loop_garden.wav", garden_loop, True),
        ("bird_01.wav", bird_whistles, False),
        ("bird_02.wav", bird_warble, False),
        ("bird_03.wav", dove_coo, False),
        ("bird_04.wav", sparrow_chips, False),
        ("car_pass_01.wav", lambda rng: car_pass(rng, 8.0, 0.46, 1.1, 96.0, 13.0), False),
        ("car_pass_02.wav", lambda rng: car_pass(rng, 9.5, 0.52, 1.45, 82.0, 10.0), False),
        ("wind_gust_01.wav", wind_gust, False),
    )
    summary = []
    for offset, (name, build, loop) in enumerate(sounds):
        rng = random.Random(SEED + offset * 101)
        data = write(args.out / name, build(rng), rng)
        if not args.raw:
            data = master(args.out / name, loop)
        summary.append(report(name, data, loop))
    print(json.dumps({"output": str(args.out), "rate": RATE, "seed": SEED, "mastered": not args.raw, "files": summary}, indent=1))


if __name__ == "__main__":
    main()
