from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
import time
from pathlib import Path

import numpy as np
from scipy import signal
from scipy.io import wavfile

REPO = Path(__file__).resolve().parents[2]
ASSETS = REPO / "Assets/_Project"
IMPACT = ASSETS / "Audio/Impact/Clips"
WINTER = ASSETS / "Audio/Winter"
AMBIENCE = ASSETS / "Resources/Ambience"

HOUSE_SPEAKER = 0.13
CEILING_DB = -3.0

TARGETS = {
    "contact": {"momentary": -20.0, "tolerance": 1.5, "peak": -3.0, "harsh": -24.0},
    "break": {"momentary": -20.0, "tolerance": 1.5, "peak": -3.0, "harsh": -24.0},
    "damage": {"momentary": -21.0, "tolerance": 1.5, "peak": -3.0, "harsh": -25.0},
    "swing": {"momentary": -23.0, "tolerance": 1.5, "peak": -3.0, "harsh": -28.0},
    "winter": {"momentary": -13.0, "tolerance": 1.5, "peak": -3.1, "harsh": -20.0},
    "cue-action": {"momentary": -18.0, "tolerance": 6.0, "peak": -3.0, "harsh": -30.0},
    "cue-ui": {"momentary": -18.0, "tolerance": 6.0, "peak": -3.0, "harsh": -30.0},
    "cue-tick": {"momentary": -26.0, "tolerance": 3.0, "peak": -3.0, "harsh": -30.0},
    "ambience-bed": {"momentary": -20.5, "tolerance": 3.0, "peak": -6.0, "harsh": -34.0},
    "ambience-call": {"momentary": -19.5, "tolerance": 2.0, "peak": -6.0, "harsh": -22.0},
}

ACTION_CUES = {"Break", "Hit", "Block", "Blast", "Swing", "Out"}
UI_CUES = {"Pickup", "Craft", "Jump", "Dodge", "Protect", "Boost", "Heal"}

BEFORE_GAINS = {
    "contact": 0.8, "break": 0.8, "damage": 0.72, "swing": 0.56,
    "winter": HOUSE_SPEAKER, "cue-action": HOUSE_SPEAKER, "cue-ui": HOUSE_SPEAKER,
    "cue-tick": 0.2 * (0.3 + 0.7 / 18.0), "ambience-bed": 0.2, "ambience-call": 0.26,
}
HOUSE_SPEAKER_AFTER = 0.85
CUE_TRIM = 0.84
CUE_TARGETS = {"Hit": -20.5, "Break": -20.5, "Block": -20.5, "Blast": -20.5, "Out": -21.5, "Swing": -26.5,
               "Pickup": -23.5, "Craft": -23.5, "Jump": -23.5, "Dodge": -23.5, "Protect": -23.5, "Boost": -23.5, "Heal": -23.5}
WINTER_CUES = {"bell": "Pickup", "paper": "Craft", "snow": "Dodge"}
TICK_LEVEL_AFTER = 1.0
AFTER_GAINS = {
    "contact": 0.8, "break": 0.8, "damage": 0.72, "swing": 0.56,
    "winter": None, "cue-action": None, "cue-ui": None,
    "cue-tick": TICK_LEVEL_AFTER * CUE_TRIM, "ambience-bed": 0.2, "ambience-call": 0.26,
}
PINNED = {"contact", "break", "damage", "swing"}


def cue_gain(cue: str, momentary: float) -> float:
    level = min(0.0, CUE_TARGETS[cue] - momentary)
    return HOUSE_SPEAKER_AFTER * CUE_TRIM * 10 ** (level / 20)


def db(value: float, floor: float = -120.0) -> float:
    return float(max(floor, 20.0 * math.log10(value))) if value > 0 else floor


def read(path: Path) -> tuple[int, np.ndarray]:
    rate, data = wavfile.read(str(path))
    if data.dtype == np.int16:
        x = data.astype(np.float64) / 32768.0
    elif data.dtype == np.int32:
        x = data.astype(np.float64) / 2147483648.0
    elif data.dtype == np.uint8:
        x = (data.astype(np.float64) - 128.0) / 128.0
    else:
        x = data.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(axis=1)
    return int(rate), x


def write(path: Path, rate: int, x: np.ndarray) -> None:
    pcm = np.clip(np.round(x * 32767.0), -32768, 32767).astype(np.int16)
    wavfile.write(str(path), rate, pcm)


def kweight_coefficients(fs: float) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    gain, q, fc = 3.999843853973347, 0.7071752369554196, 1681.974450955533
    k = math.tan(math.pi * fc / fs)
    vh = 10.0 ** (gain / 20.0)
    vb = vh ** 0.4996667741545416
    a0 = 1.0 + k / q + k * k
    b1 = np.array([(vh + vb * k / q + k * k) / a0, 2.0 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0])
    a1 = np.array([1.0, 2.0 * (k * k - 1.0) / a0, (1.0 - k / q + k * k) / a0])
    q2, fc2 = 0.5003270373238773, 38.13547087613982
    k = math.tan(math.pi * fc2 / fs)
    a0 = 1.0 + k / q2 + k * k
    b2 = np.array([1.0, -2.0, 1.0])
    a2 = np.array([1.0, 2.0 * (k * k - 1.0) / a0, (1.0 - k / q2 + k * k) / a0])
    return b1, a1, b2, a2


def kweight(x: np.ndarray, fs: float) -> np.ndarray:
    b1, a1, b2, a2 = kweight_coefficients(fs)
    return signal.lfilter(b2, a2, signal.lfilter(b1, a1, x))


def window_loudness(power: np.ndarray, fs: float, seconds: float, hop: float, complete: bool = False) -> np.ndarray:
    n = int(round(seconds * fs))
    step = max(1, int(round(hop * fs)))
    padded = np.concatenate([power, np.zeros(n)])
    csum = np.concatenate([[0.0], np.cumsum(padded)])
    last = len(power) - n + 1 if complete and len(power) >= n else max(1, len(power))
    starts = np.arange(0, last, step)
    energy = (csum[starts + n] - csum[starts]) / n
    return -0.691 + 10.0 * np.log10(np.maximum(energy, 1e-20))


def loudness(x: np.ndarray, fs: float) -> dict:
    power = kweight(x, fs) ** 2
    momentary = window_loudness(power, fs, 0.4, 0.01)
    short = window_loudness(power, fs, 3.0, 0.1, True)
    blocks = window_loudness(power, fs, 0.4, 0.1, True)
    gated = blocks[blocks > -70.0]
    integrated = -120.0
    if len(gated):
        first = -0.691 + 10 * math.log10(np.mean(10 ** ((gated + 0.691) / 10)))
        relative = gated[gated > first - 10.0]
        if len(relative):
            integrated = -0.691 + 10 * math.log10(np.mean(10 ** ((relative + 0.691) / 10)))
    shorts = short[short > -70.0]
    lra = 0.0
    if len(shorts) > 2:
        mean = -0.691 + 10 * math.log10(np.mean(10 ** ((shorts + 0.691) / 10)))
        kept = shorts[shorts > mean - 20.0]
        if len(kept) > 2:
            lra = float(np.percentile(kept, 95) - np.percentile(kept, 10))
    return {"integrated": float(integrated), "momentary_max": float(momentary.max()),
            "short_term_max": float(short.max()), "loudness_range": lra}


def true_peak(x: np.ndarray) -> float:
    if len(x) < 8:
        return float(np.max(np.abs(x))) if len(x) else 0.0
    return float(np.max(np.abs(signal.resample_poly(x, 4, 1))))


def active_region(x: np.ndarray, threshold_db: float = -60.0) -> tuple[int, int]:
    level = 10 ** (threshold_db / 20)
    above = np.nonzero(np.abs(x) > level)[0]
    if len(above) == 0:
        return 0, 0
    return int(above[0]), int(above[-1]) + 1


def frame_rms(x: np.ndarray, fs: float, seconds: float = 0.005) -> np.ndarray:
    n = max(1, int(fs * seconds))
    count = len(x) // n
    if count == 0:
        return np.array([math.sqrt(np.mean(x ** 2))]) if len(x) else np.array([0.0])
    return np.sqrt(np.mean(x[: count * n].reshape(count, n) ** 2, axis=1))


def noise_frames(x: np.ndarray, fs: float, frame: int = 512) -> tuple[float, float]:
    if len(x) < frame:
        return -120.0, -120.0
    window = np.hanning(frame)
    freqs = np.fft.rfftfreq(frame, 1.0 / fs)
    band = (freqs >= 100) & (freqs <= min(16000, fs / 2 - 1))
    levels, noisy = [], []
    for start in range(0, len(x) - frame + 1, frame // 2):
        seg = x[start:start + frame]
        rms = float(np.sqrt(np.mean(seg ** 2)))
        if rms < 1e-6:
            continue
        levels.append(rms)
        p = np.abs(np.fft.rfft(seg * window))[band] ** 2 + 1e-24
        if float(np.exp(np.mean(np.log(p))) / np.mean(p)) > 0.35:
            noisy.append(rms)
    loudest = max(levels) if levels else 0.0
    quiet = [v for v in noisy if v < loudest * 0.1]
    top = db(loudest) if levels else -120.0
    floor = db(float(np.percentile(quiet, 10))) if len(quiet) >= 2 else -120.0
    return floor, top


def spectrum_shares(x: np.ndarray, fs: float) -> dict:
    if len(x) < 32:
        return {"harsh_share": 0.0, "above_6k_share": 0.0, "centroid_hz": 0.0, "flatness": 0.0}
    n = 1 << int(math.ceil(math.log2(max(len(x), 1024))))
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x)), n)) ** 2
    freqs = np.fft.rfftfreq(n, 1.0 / fs)
    total = spec[freqs >= 20].sum()
    if total <= 0:
        return {"harsh_share": 0.0, "above_6k_share": 0.0, "centroid_hz": 0.0, "flatness": 0.0}
    harsh = spec[(freqs >= 2000) & (freqs < 6000)].sum() / total
    high = spec[freqs >= 6000].sum() / total
    centroid = float((spec * freqs).sum() / spec.sum())
    frame = 1024
    flat = []
    for start in range(0, max(1, len(x) - frame), frame // 2):
        seg = x[start:start + frame]
        if len(seg) < frame or np.sqrt(np.mean(seg ** 2)) < 1e-4:
            continue
        p = np.abs(np.fft.rfft(seg * np.hanning(frame))) ** 2 + 1e-20
        band = p[(np.fft.rfftfreq(frame, 1.0 / fs) >= 50) & (np.fft.rfftfreq(frame, 1.0 / fs) <= min(16000, fs / 2))]
        flat.append(float(np.exp(np.mean(np.log(band))) / np.mean(band)))
    return {"harsh_share": float(harsh), "above_6k_share": float(high), "centroid_hz": centroid,
            "flatness": float(np.median(flat)) if flat else 0.0}


def clicks(x: np.ndarray, fs: float, loop: bool = False) -> dict:
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    if peak <= 0:
        return {"start_jump": 0.0, "end_jump": 0.0, "onset_slope": 0.0, "discontinuities": 0}
    onset = int(max(2, fs * 0.001))
    first, _ = active_region(x)
    seg = x[first:first + onset]
    slope = float(np.max(np.abs(np.diff(np.concatenate([[x[first - 1] if first > 0 else 0.0], seg]))))) / peak
    wrapped = np.concatenate([x, x[:1]]) if loop else x
    d = np.abs(np.diff(wrapped))
    width = max(3, int(fs * 0.0025)) | 1
    local = np.sqrt(np.convolve(d ** 2, np.ones(width) / width, mode="same")) + 1e-12
    count = int(np.sum((d > 6.0 * local) & (d > 0.01)))
    start = 0.0 if loop else float(abs(x[0]))
    end = float(abs(x[-1] - x[0])) if loop else float(abs(x[-1]))
    return {"start_jump": start, "end_jump": end, "onset_slope": slope, "discontinuities": count}


def band_loudness(x: np.ndarray, fs: float, low: float = 2000.0, high: float = 6000.0) -> float:
    if len(x) < 16:
        return -120.0
    sos = signal.butter(4, [low / (fs / 2), min(0.99, high / (fs / 2))], "bandpass", output="sos")
    power = kweight(signal.sosfilt(sos, x), fs) ** 2
    return float(window_loudness(power, fs, 0.4, 0.01).max())


def measure(x: np.ndarray, fs: int, category: str, playback_gain: float | None = None, loop: bool = False) -> dict:
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    first, last = active_region(x)
    body = x[first:last] if last > first else x
    rms = float(np.sqrt(np.mean(body ** 2))) if len(body) else 0.0
    floor, top = noise_frames(body, fs)
    loud = loudness(x, fs)
    shares = spectrum_shares(body, fs)
    edge = clicks(x, fs, loop)
    harsh_lufs = band_loudness(x, fs)
    clipped = int(np.sum(np.abs(x) >= 0.999))
    row = {
        "category": category, "rate": fs, "seconds": round(len(x) / fs, 4),
        "lead_silence_ms": round(first / fs * 1000, 2), "tail_silence_ms": round((len(x) - last) / fs * 1000, 2),
        "peak_dbfs": round(db(peak), 2), "true_peak_dbtp": round(db(true_peak(x)), 2),
        "integrated_lufs": round(loud["integrated"], 2), "momentary_max_lufs": round(loud["momentary_max"], 2),
        "short_term_max_lufs": round(loud["short_term_max"], 2), "loudness_range_lu": round(loud["loudness_range"], 2),
        "crest_db": round(db(peak) - db(rms), 2) if rms > 0 else 0.0,
        "dc_offset": float(f"{float(np.mean(x)) if len(x) else 0.0:.6g}"),
        "noise_floor_dbfs": round(floor, 2), "snr_db": round(top - floor, 2) if floor > -120 else 120.0,
        "harsh_2_6k_share": round(shares["harsh_share"], 4), "harsh_band_lufs": round(harsh_lufs, 2), "above_6k_share": round(shares["above_6k_share"], 4),
        "centroid_hz": round(shares["centroid_hz"], 1), "spectral_flatness": round(shares["flatness"], 4),
        "start_jump": round(edge["start_jump"], 5), "end_jump": round(edge["end_jump"], 5),
        "onset_slope": round(edge["onset_slope"], 4), "discontinuities": edge["discontinuities"],
        "clipped_samples": clipped, "loop": loop,
    }
    if playback_gain:
        row["playback_gain"] = round(playback_gain, 4)
        row["effective_momentary_lufs"] = round(loud["momentary_max"] + db(playback_gain), 2)
    row["issues"], row["badness"] = judge(row)
    return row


def judge(row: dict) -> tuple[list[str], float]:
    target = TARGETS.get(row["category"])
    issues, score = [], 0.0
    if target is None:
        return issues, score
    cue = row["category"].startswith("cue")
    over = row["true_peak_dbtp"] - target["peak"]
    if over > 0.05:
        issues.append(f"true peak {row['true_peak_dbtp']} dBTP over {target['peak']}")
        score += over * 2
    loud = row["momentary_max_lufs"]
    limited = over > -0.6 or row["harsh_band_lufs"] > target["harsh"] - 0.6
    if loud > target["momentary"] + target["tolerance"] or (loud < target["momentary"] - target["tolerance"] and not limited):
        issues.append(f"loudness {loud} LUFS-M vs {target['momentary']}")
        score += abs(loud - target["momentary"]) - target["tolerance"]
    if row["harsh_band_lufs"] > target["harsh"] + 0.3:
        issues.append(f"harsh 2-6 kHz band {row['harsh_band_lufs']} LUFS-M over {target['harsh']} ({row['harsh_2_6k_share']:.0%} of energy)")
        score += (row["harsh_band_lufs"] - target["harsh"]) * 1.5
    if row["clipped_samples"]:
        issues.append(f"{row['clipped_samples']} clipped samples")
        score += 10 + row["clipped_samples"] * 0.1
    if not row["loop"] and (row["start_jump"] > 0.001 or row["end_jump"] > 0.001):
        issues.append(f"edge click start {row['start_jump']:.3f} end {row['end_jump']:.3f}")
        score += 3 + 20 * (row["start_jump"] + row["end_jump"])
    if row["loop"] and row["end_jump"] > 0.02:
        issues.append(f"loop seam jump {row['end_jump']:.3f}")
        score += 3
    slope_limit = 0.25 if cue else 0.5
    if row["onset_slope"] > slope_limit:
        issues.append(f"abrupt onset {row['onset_slope']:.2f} of peak in one sample")
        score += 2 * row["onset_slope"]
    if row["discontinuities"]:
        issues.append(f"{row['discontinuities']} discontinuities")
        score += min(5.0, row["discontinuities"] * 0.5)
    if abs(row["dc_offset"]) > 1e-4:
        issues.append(f"DC {row['dc_offset']:.2e}")
        score += min(5.0, abs(row["dc_offset"]) * 1000)
    if cue and row["rate"] < 44100:
        issues.append(f"rendered at {row['rate']} Hz")
        score += 3
    if cue and row["spectral_flatness"] > 0.03:
        issues.append(f"noise-like (spectral flatness {row['spectral_flatness']:.3f})")
        score += row["spectral_flatness"] * 60
    if row.get("image_share", 0.0) > 0.001:
        issues.append(f"resampling images {row['image_share']:.2%} above source Nyquist")
        score += row["image_share"] * 200
    if not row["loop"] and row["category"] in ("contact", "break", "damage", "swing") and row["tail_silence_ms"] > 40:
        issues.append(f"tail padding {row['tail_silence_ms']} ms")
        score += 0.5
    if row["noise_floor_dbfs"] > -50 and row["snr_db"] < 30:
        issues.append(f"noise floor {row['noise_floor_dbfs']} dBFS (SNR {row['snr_db']} dB)")
        score += (row["noise_floor_dbfs"] + 50) * 0.3
    return issues, round(score, 3)


def linear_resample_images(x: np.ndarray, fs: int, out: int = 48000) -> float:
    if fs >= out:
        return 0.0
    t = np.arange(int(len(x) * out / fs)) * fs / out
    y = np.interp(t, np.arange(len(x)), x)
    n = 1 << int(math.ceil(math.log2(max(len(y), 1024))))
    spec = np.abs(np.fft.rfft(y * np.hanning(len(y)), n)) ** 2
    freqs = np.fft.rfftfreq(n, 1.0 / out)
    total = spec.sum()
    return float(spec[freqs > fs / 2].sum() / total) if total > 0 else 0.0


class NetRandom:
    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed: int):
        seeds = [0] * 56
        subtraction = 2147483647 if seed == -2147483648 else abs(seed)
        mj = self.MSEED - subtraction
        seeds[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            seeds[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = seeds[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                seeds[i] -= seeds[1 + (i + 30) % 55]
                if seeds[i] < 0:
                    seeds[i] += self.MBIG
        self.seeds, self.inext, self.inextp = seeds, 0, 21

    def next_double(self) -> float:
        a = self.inext + 1 if self.inext + 1 < 56 else 1
        b = self.inextp + 1 if self.inextp + 1 < 56 else 1
        value = self.seeds[a] - self.seeds[b]
        if value == self.MBIG:
            value -= 1
        if value < 0:
            value += self.MBIG
        self.seeds[a] = value
        self.inext, self.inextp = a, b
        return value * (1.0 / self.MBIG)


OLD_ORDER = ["Pickup", "Break", "Craft", "Hit", "Block", "Jump", "Dodge", "Swing", "Out", "Protect", "Boost", "Blast", "Heal"]


def old_cue(cue: str) -> tuple[int, np.ndarray]:
    rate = 16000
    seconds = 0.28 if cue in ("Craft", "Heal") else 0.3 if cue == "Out" else 0.12
    low = {"Pickup": 670, "Craft": 430, "Break": 140, "Hit": 170, "Block": 520, "Jump": 310, "Dodge": 260, "Out": 400,
           "Protect": 760, "Boost": 340, "Blast": 85, "Heal": 540}.get(cue, 230)
    high = {"Pickup": 1050, "Craft": 1050, "Jump": 750, "Dodge": 680, "Out": 130, "Protect": 1150, "Boost": 980,
            "Heal": 1080}.get(cue, low * 0.45)
    count = int(math.ceil(np.float32(rate * np.float32(seconds))))
    noise = NetRandom(19 + OLD_ORDER.index(cue))
    out = np.zeros(count, dtype=np.float32)
    phase = np.float32(0)
    two_pi = np.float32(math.pi * 2)
    for i in range(count):
        t = np.float32(i) / np.float32(count)
        phase = np.float32(phase + np.float32(low + (high - low) * t) * two_pi / np.float32(rate))
        envelope = min(np.float32(1), t * np.float32(35)) * np.float32((1 - t) ** 2.5)
        wave = np.float32(math.sin(phase)) * np.float32(0.7) + np.float32(math.sin(phase * np.float32(2.01))) * np.float32(0.15)
        if cue in ("Break", "Hit", "Swing", "Blast"):
            wave += np.float32((noise.next_double() * 2 - 1) * 0.24)
        out[i] = wave * envelope
    return rate, out.astype(np.float64)


def old_tick() -> tuple[int, np.ndarray]:
    i = np.arange(800)
    return 16000, np.sin(i * 2 * math.pi * 880 / 16000) * (1 - i / 800)


def cue_category(cue: str) -> str:
    return "cue-tick" if cue == "Tick" else "cue-action" if cue in ACTION_CUES else "cue-ui"


def clip_category(path: Path) -> tuple[str, bool]:
    parts = path.relative_to(ASSETS).parts
    if parts[0] == "Audio" and parts[1] == "Impact":
        return parts[3], False
    if parts[0] == "Audio" and parts[1] == "Winter":
        return "winter", False
    loop = path.stem.lower().startswith("loop")
    return ("ambience-bed" if loop else "ambience-call"), loop


def clip_files() -> list[Path]:
    files = sorted(IMPACT.rglob("*.wav")) + sorted(WINTER.glob("*.wav")) + sorted(AMBIENCE.rglob("*.wav"))
    return [f for f in files if f.is_file()]


def audit(cues: str, gains: dict) -> list[dict]:
    rows = []
    for path in clip_files():
        category, loop = clip_category(path)
        fs, x = read(path)
        gain = gains.get(category)
        if gain is None and category == "winter":
            gain = cue_gain(WINTER_CUES[path.stem.split("-")[0]], loudness(x, fs)["momentary_max"])
        row = measure(x, fs, category, gain, loop)
        row["pinned"] = category in PINNED and gains is AFTER_GAINS
        row["name"] = str(path.relative_to(REPO)).replace("\\", "/")
        row["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
        rows.append(row)
    if cues == "old":
        rendered = [(c, *old_cue(c)) for c in OLD_ORDER] + [("Tick", *old_tick())]
    else:
        import toy_cue_synth
        rendered = [(c, toy_cue_synth.RATE, toy_cue_synth.render(c)) for c in toy_cue_synth.CUES]
    for cue, fs, x in rendered:
        category = cue_category(cue)
        image = linear_resample_images(x, fs)
        gain = gains.get(category)
        if gain is None and cue != "Tick":
            gain = cue_gain(cue, loudness(x, fs)["momentary_max"])
        row = measure(x, fs, category, gain)
        row["image_share"] = round(image, 5)
        row["issues"], row["badness"] = judge(row)
        row["name"] = ("GameFeedback " if cue != "Tick" else "ThrownGear ") + cue + (" (old maths)" if cues == "old" else " (ToyCueSynth)")
        rows.append(row)
    rows.sort(key=lambda r: (-r["badness"], r["name"]))
    return rows


def summary(rows: list[dict]) -> dict:
    out = {}
    for category in TARGETS:
        group = [r for r in rows if r["category"] == category]
        if not group:
            continue
        m = np.array([r["momentary_max_lufs"] for r in group])
        out[category] = {
            "count": len(group),
            "with_issues": sum(1 for r in group if r["issues"]),
            "belt_pinned_unchanged": any(r.get("pinned") for r in group),
            "momentary_lufs_min": round(float(m.min()), 2), "momentary_lufs_max": round(float(m.max()), 2),
            "momentary_lufs_spread": round(float(m.max() - m.min()), 2),
            "effective_lufs_max": round(max(r.get("effective_momentary_lufs", -120) for r in group), 2),
            "true_peak_max": round(max(r["true_peak_dbtp"] for r in group), 2),
            "harsh_share_mean": round(float(np.mean([r["harsh_2_6k_share"] for r in group])), 4),
            "harsh_share_max": round(max(r["harsh_2_6k_share"] for r in group), 4),
            "harsh_band_lufs_max": round(max(r["harsh_band_lufs"] for r in group), 2),
            "onset_slope_max": round(max(r["onset_slope"] for r in group), 3),
            "discontinuities": sum(r["discontinuities"] for r in group),
            "noise_floor_max": round(max(r["noise_floor_dbfs"] for r in group), 2),
            "flatness_median": round(float(np.median([r["spectral_flatness"] for r in group])), 4),
            "clipped": sum(r["clipped_samples"] for r in group),
            "edge_clicks": sum(1 for r in group if not r["loop"] and (r["start_jump"] > 0.001 or r["end_jump"] > 0.001)),
            "dc_max": float(f"{max(abs(r['dc_offset']) for r in group):.3g}"),
        }
    return out


def self_check() -> float:
    b1, a1, b2, a2 = kweight_coefficients(48000)
    reference = np.array([1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585])
    mine = np.array([b1[0], b1[1], b1[2], a1[1], a1[2]])
    tone = np.sin(2 * math.pi * 997 * np.arange(48000 * 4) / 48000) * 10 ** (-20 / 20)
    level = loudness(tone, 48000)["integrated"]
    assert np.max(np.abs(mine - reference)) < 1e-6, mine
    assert abs(level - (-23.0 + 0.0)) < 0.2 or abs(level - (-20.0 - 3.01 + 0.0)) < 0.2, level
    return level


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--cues", choices=["old", "new"], default="new")
    parser.add_argument("--gains", choices=["before", "after"], default="after")
    args = parser.parse_args()
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    reference = self_check()
    rows = audit(args.cues, BEFORE_GAINS if args.gains == "before" else AFTER_GAINS)
    report = {
        "schema": 1,
        "utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "script": "Tools/AssetPipeline/audio_audit.py",
        "script_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "numpy": np.__version__,
        "kweight_reference_lufs_997hz_minus20dbfs": round(reference, 3),
        "cues": args.cues, "gains": args.gains,
        "momentary_note": "momentary_max_lufs is the max 400 ms K-weighted loudness; for sub-second one-shots the 3 s short-term value is the same energy diluted over 3 s",
        "targets": TARGETS,
        "with_issues_in_scope": sum(1 for r in rows if r["issues"] and not r.get("pinned")),
        "summary": summary(rows),
        "rows": rows,
    }
    out = Path(args.out)
    if not out.is_absolute():
        out = REPO / out
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=1), encoding="utf-8")
    print(json.dumps({"rows": len(rows), "with_issues": sum(1 for r in rows if r["issues"]),
                      "with_issues_in_scope": sum(1 for r in rows if r["issues"] and not r.get("pinned")), "summary": report["summary"]}, indent=1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
