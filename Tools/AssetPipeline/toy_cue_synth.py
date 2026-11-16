from __future__ import annotations

import math

import numpy as np

RATE = 48000
CEILING_DB = -3.2
BANDLIMIT = 0.42
CUES = ["Pickup", "Break", "Craft", "Hit", "Block", "Jump", "Dodge", "Swing", "Out", "Protect", "Boost", "Blast", "Heal", "Tick"]
TRIM_DB = {"Pickup": -2.0, "Craft": 0.0, "Jump": -1.0, "Dodge": -1.0, "Protect": -1.0, "Boost": -1.0, "Heal": -1.0,
           "Break": 0.0, "Hit": 0.0, "Block": 0.0, "Blast": -2.0, "Swing": -1.0, "Out": -3.0, "Tick": 0.0}

SOFT = ((1.0, 1.0), (2.0, 0.16), (3.0, 0.04))
BELL = ((1.0, 1.0), (2.0, 0.22), (3.0, 0.07), (4.0, 0.025))
HOLLOW = ((1.0, 1.0), (2.0, 0.3), (3.0, 0.1))
PURE = ((1.0, 1.0), (2.0, 0.08))
WOOD = ((1.0, 1.0), (2.0, 0.12))


def tone(start, length, f0, f1, amp, attack, decay, partials, glide=0.0):
    return ("tone", start, length, f0, f1, amp, attack, decay, partials, glide, 0.0)


def puff(start, length, f0, f1, amp, attack, decay, q=0.0):
    return ("puff", start, length, f0, f1, amp, attack, decay, (), 0.0, q)


RECIPES = {
    "Pickup": [tone(0.0, 0.09, 659.26, 659.26, 0.8, 0.004, 0.06, SOFT),
               tone(0.045, 0.1, 830.61, 830.61, 1.0, 0.004, 0.06, SOFT)],
    "Craft": [tone(0.0, 0.12, 523.25, 523.25, 0.8, 0.005, 0.09, BELL),
              tone(0.07, 0.12, 659.26, 659.26, 0.8, 0.005, 0.09, BELL),
              tone(0.14, 0.24, 783.99, 783.99, 1.0, 0.005, 0.13, BELL),
              tone(0.14, 0.22, 1046.5, 1046.5, 0.22, 0.008, 0.11, PURE)],
    "Heal": [tone(0.0, 0.14, 392.0, 392.0, 0.8, 0.012, 0.12, SOFT),
             tone(0.08, 0.24, 587.33, 587.33, 1.0, 0.012, 0.15, SOFT)],
    "Protect": [tone(0.0, 0.24, 440.0, 440.0, 0.9, 0.006, 0.11, BELL),
                tone(0.03, 0.22, 587.33, 587.33, 0.8, 0.006, 0.1, BELL)],
    "Boost": [tone(0.0, 0.08, 587.33, 587.33, 0.8, 0.004, 0.06, SOFT),
              tone(0.04, 0.08, 739.99, 739.99, 0.85, 0.004, 0.06, SOFT),
              tone(0.08, 0.13, 880.0, 880.0, 1.0, 0.004, 0.08, SOFT)],
    "Out": [tone(0.0, 0.15, 392.0, 392.0, 0.9, 0.008, 0.2, HOLLOW),
            tone(0.13, 0.24, 329.63, 293.66, 1.0, 0.008, 0.2, HOLLOW, 0.2)],
    "Jump": [tone(0.0, 0.14, 330.0, 560.0, 1.0, 0.006, 0.1, PURE, 0.12)],
    "Dodge": [puff(0.0, 0.14, 520.0, 1100.0, 1.0, 0.03, 0.09, 1.3)],
    "Swing": [puff(0.0, 0.13, 380.0, 720.0, 1.0, 0.028, 0.08, 1.1)],
    "Hit": [tone(0.0, 0.12, 260.0, 170.0, 1.0, 0.002, 0.06, HOLLOW, 0.05),
            puff(0.0, 0.04, 900.0, 600.0, 0.35, 0.002, 0.015)],
    "Block": [tone(0.0, 0.1, 620.0, 620.0, 1.0, 0.0015, 0.03, WOOD),
              tone(0.0, 0.1, 410.0, 410.0, 0.45, 0.0015, 0.034, WOOD)],
    "Break": [tone(0.0, 0.17, 180.0, 120.0, 1.0, 0.002, 0.055, HOLLOW, 0.1),
              puff(0.0, 0.2, 1100.0, 500.0, 0.55, 0.003, 0.07),
              tone(0.05, 0.05, 520.0, 520.0, 0.28, 0.0015, 0.013, WOOD),
              tone(0.095, 0.05, 440.0, 440.0, 0.24, 0.0015, 0.013, WOOD)],
    "Blast": [tone(0.0, 0.36, 160.0, 70.0, 1.0, 0.004, 0.13, PURE, 0.3),
              puff(0.0, 0.34, 900.0, 300.0, 1.0, 0.006, 0.1)],
    "Tick": [tone(0.0, 0.035, 1050.0, 1050.0, 1.0, 0.001, 0.008, PURE),
             tone(0.0, 0.035, 525.0, 525.0, 0.5, 0.001, 0.01, PURE)],
}


def kweight_coefficients(fs):
    gain, q, fc = 3.999843853973347, 0.7071752369554196, 1681.974450955533
    k = math.tan(math.pi * fc / fs)
    vh = 10.0 ** (gain / 20.0)
    vb = vh ** 0.4996667741545416
    a0 = 1.0 + k / q + k * k
    shelf = ((vh + vb * k / q + k * k) / a0, 2.0 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0,
             2.0 * (k * k - 1.0) / a0, (1.0 - k / q + k * k) / a0)
    q2, fc2 = 0.5003270373238773, 38.13547087613982
    k = math.tan(math.pi * fc2 / fs)
    a0 = 1.0 + k / q2 + k * k
    high = (1.0, -2.0, 1.0, 2.0 * (k * k - 1.0) / a0, (1.0 - k / q2 + k * k) / a0)
    return shelf, high


def biquad(x, c):
    b0, b1, b2, a1, a2 = c
    out = np.empty_like(x)
    z1 = z2 = 0.0
    for i, v in enumerate(x):
        y = b0 * v + z1
        z1 = b1 * v - a1 * y + z2
        z2 = b2 * v - a2 * y
        out[i] = y
    return out


def momentary_max(x, fs):
    shelf, high = kweight_coefficients(fs)
    p = biquad(biquad(np.asarray(x, dtype=np.float64), shelf), high) ** 2
    n = int(round(0.4 * fs))
    step = int(round(0.01 * fs))
    csum = np.concatenate([[0.0], np.cumsum(np.concatenate([p, np.zeros(n)]))])
    best = 0.0
    for s in range(0, max(1, len(p)), step):
        e = (csum[s + n] - csum[s]) / n
        if e > best:
            best = e
    return -0.691 + 10.0 * math.log10(max(best, 1e-20))


def envelope(j, rate, length, attack, decay):
    t = j / rate
    a = 0.5 - 0.5 * math.cos(math.pi * t / attack) if t < attack else 1.0
    rel = min(0.03, length * 0.4)
    r = 0.5 + 0.5 * math.cos(math.pi * (t - (length - rel)) / rel) if t > length - rel else 1.0
    return a * math.exp(-t / decay) * r


def svf(state, v0, g, k):
    ic1, ic2 = state
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    v3 = v0 - ic2
    v1 = a1 * ic1 + a2 * v3
    v2 = ic2 + a2 * ic1 + a3 * v3
    state[0] = 2.0 * v1 - ic1
    state[1] = 2.0 * v2 - ic2
    return v1, v2


def render_puff(out, part, rate, pitch, seed):
    _, start, length, f0, f1, amp, attack, decay, _, _, q = part
    i0 = int(round(start * rate))
    m = int(round(length * rate))
    first, second = [0.0, 0.0], [0.0, 0.0]
    state = seed & 0xFFFFFFFF or 1
    for j in range(m):
        t = j / rate
        state ^= (state << 13) & 0xFFFFFFFF
        state ^= state >> 17
        state ^= (state << 5) & 0xFFFFFFFF
        noise = state / 4294967296.0 * 2.0 - 1.0
        fc = min(f0 * pitch * (f1 / f0) ** min(t / length, 1.0), 0.4 * rate)
        g = math.tan(math.pi * fc / rate)
        band, low = svf(first, noise, g, 1.0 / q if q > 0.0 else 1.41421356237)
        v = band if q > 0.0 else low
        g2 = math.tan(math.pi * min(fc * 1.6, 0.4 * rate) / rate)
        _, v = svf(second, v, g2, 1.41421356237)
        out[i0 + j] += amp * envelope(j, rate, length, attack, decay) * v


def render(cue, rate=RATE, pitch=1.0):
    parts = RECIPES[cue]
    n = int(math.ceil(max(p[1] + p[2] for p in parts) * rate)) + 1
    buf = np.zeros(n + 2, dtype=np.float64)
    index = CUES.index(cue)
    for k, part in enumerate(parts):
        if part[0] == "tone":
            render_tone_env(buf, part, rate, pitch)
        else:
            render_puff(buf, part, rate, pitch, 0x9E3779B9 ^ (index * 7919 + k * 104729))
    buf = buf[:n]
    fade_in = max(2, int(round(0.001 * rate)))
    fade_out = max(2, int(round(0.004 * rate)))
    for j in range(min(fade_in, n)):
        buf[j] *= 0.5 - 0.5 * math.cos(math.pi * j / fade_in)
    for j in range(min(fade_out, n)):
        buf[n - 1 - j] *= 0.5 - 0.5 * math.cos(math.pi * j / fade_out)
    window = np.array([0.5 - 0.5 * math.cos(2.0 * math.pi * i / (n - 1)) for i in range(n)])
    buf = buf - (buf.sum() / window.sum()) * window
    peak = float(np.max(np.abs(buf)))
    if peak <= 0.0:
        return buf.astype(np.float32).astype(np.float64)
    gain = 10.0 ** ((CEILING_DB + TRIM_DB[cue]) / 20.0) / peak
    return (buf * gain).astype(np.float32).astype(np.float64)


def render_tone_env(out, part, rate, pitch):
    _, start, length, f0, f1, amp, attack, decay, partials, glide, _ = part
    i0 = int(round(start * rate))
    m = int(round(length * rate))
    top = max(f0, f1) * pitch
    used = [(r, a, decay / (1.0 + 0.5 * (r - 1.0))) for r, a in partials if r * top < BANDLIMIT * rate]
    rel = min(0.03, length * 0.4)
    phase = 0.0
    for j in range(m):
        t = j / rate
        f = f0 * pitch
        if glide > 0.0 and f1 != f0:
            f = f0 * pitch * (f1 / f0) ** min(t / glide, 1.0)
        a = 0.5 - 0.5 * math.cos(math.pi * t / attack) if t < attack else 1.0
        e = 0.5 + 0.5 * math.cos(math.pi * (t - (length - rel)) / rel) if t > length - rel else 1.0
        s = 0.0
        for ratio, weight, tau in used:
            s += weight * math.exp(-t / tau) * math.sin(ratio * phase)
        out[i0 + j] += amp * a * e * s
        phase += 2.0 * math.pi * f / rate


def checksum(x):
    weights = np.arange(1, len(x) + 1, dtype=np.float64)
    return float(np.sum(np.asarray(x, dtype=np.float64) * np.sin(weights * 0.001)))
