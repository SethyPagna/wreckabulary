from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import sys
import time
from pathlib import Path

import numpy as np
from scipy import signal

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audio_audit as audit

REPO = audit.REPO
ASSETS = audit.ASSETS
IMPACT_SOURCE = REPO / "ArtSource/Audio/ImpactV1/synth-02"
WINTER_SOURCE = REPO / "ArtSource/Collections/Winter/Audio"
WINTER_MASTERED = WINTER_SOURCE / "Mastered"
BACKUP = REPO / "BuildNotes/H/audio-originals"
RECEIPT = REPO / "BuildNotes/H/audio-process-receipt.json"

SETTINGS = {
    "winter": {"highpass": 40.0, "trim": False, "fade_in_ms": 1.0, "fade_out_ms": 3.0, "deharsh": False},
    "ambience-call": {"highpass": 80.0, "trim": False, "fade_in_ms": 20.0, "fade_out_ms": 40.0, "deharsh": True, "lowpass": 7000.0},
    "ambience-bed": {"highpass": 90.0, "trim": False, "fade_in_ms": 0.0, "fade_out_ms": 0.0, "deharsh": False, "lowpass": 1800.0},
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def circular(x: np.ndarray, sos: np.ndarray) -> np.ndarray:
    n = len(x)
    return signal.sosfilt(sos, np.concatenate([x, x, x]))[n:2 * n]


def peaking(fs: float, hz: float, gain_db: float, q: float) -> np.ndarray:
    a = 10 ** (gain_db / 40)
    w = 2 * math.pi * hz / fs
    alpha = math.sin(w) / (2 * q)
    c = math.cos(w)
    b = np.array([1 + alpha * a, -2 * c, 1 - alpha * a])
    d = np.array([1 + alpha / a, -2 * c, 1 - alpha / a])
    return np.concatenate([b / d[0], d / d[0]]).reshape(1, 6)


def high_shelf(fs: float, hz: float, gain_db: float) -> np.ndarray:
    a = 10 ** (gain_db / 40)
    w = 2 * math.pi * hz / fs
    alpha = math.sin(w) / 2 * math.sqrt(2)
    c = math.cos(w)
    sa = 2 * math.sqrt(a) * alpha
    b = np.array([a * ((a + 1) + (a - 1) * c + sa), -2 * a * ((a - 1) + (a + 1) * c), a * ((a + 1) + (a - 1) * c - sa)])
    d = np.array([(a + 1) - (a - 1) * c + sa, 2 * ((a - 1) - (a + 1) * c), (a + 1) - (a - 1) * c - sa])
    return np.concatenate([b / d[0], d / d[0]]).reshape(1, 6)


def fade(x: np.ndarray, fs: int, fade_in_ms: float, fade_out_ms: float) -> np.ndarray:
    y = x.copy()
    n_in = int(round(fade_in_ms / 1000 * fs))
    n_out = int(round(fade_out_ms / 1000 * fs))
    if n_in > 0:
        y[:n_in] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(n_in) / n_in)
    if n_out > 0:
        y[len(y) - n_out:] *= (0.5 - 0.5 * np.cos(np.pi * np.arange(n_out) / n_out))[::-1]
    if n_in > 0:
        y[0] = 0.0
    if n_out > 0:
        y[-1] = 0.0
    return y


def trim(x: np.ndarray, fs: int) -> np.ndarray:
    first, last = audit.active_region(x, -60.0)
    if last <= first:
        return x
    return x[first:min(len(x), last + int(0.01 * fs))]


def declick(x: np.ndarray, fs: int) -> tuple[np.ndarray, int]:
    y = x.copy()
    d = np.abs(np.diff(y))
    width = max(3, int(fs * 0.0025)) | 1
    local = np.sqrt(np.convolve(d ** 2, np.ones(width) / width, mode="same")) + 1e-12
    fixed = 0
    for i in np.nonzero((d > 6.0 * local) & (d > 0.01))[0]:
        j = i + 1
        if 1 <= j < len(y) - 1:
            smooth = 0.5 * (y[j - 1] + y[j + 1])
            if abs(smooth - y[j - 1]) < abs(y[j] - y[j - 1]):
                y[j] = smooth
                fixed += 1
    return y, fixed


def remove_dc(x: np.ndarray) -> np.ndarray:
    if len(x) < 3:
        return x
    window = 0.5 - 0.5 * np.cos(2 * np.pi * np.arange(len(x)) / (len(x) - 1))
    return x - (x.sum() / window.sum()) * window


def process(x: np.ndarray, fs: int, category: str, loop: bool) -> tuple[np.ndarray, dict]:
    settings = SETTINGS[category]
    target = audit.TARGETS[category]
    notes = {}
    filt = circular if loop else (lambda v, s: signal.sosfilt(s, v))
    y = x - np.mean(x)
    y = filt(y, signal.butter(2, settings["highpass"] / (fs / 2), "highpass", output="sos"))
    if "lowpass" in settings:
        y = filt(y, signal.butter(2, settings["lowpass"] / (fs / 2), "lowpass", output="sos"))
        notes["lowpass_hz"] = settings["lowpass"]
    if settings["deharsh"]:
        share = audit.spectrum_shares(y, fs)
        cut = -min(6.0, 12.0 * max(0.0, share["harsh_share"] - 0.5))
        if cut < -0.05:
            y = filt(y, peaking(fs, 3500.0, cut, 0.9))
            notes["deharsh_db"] = round(cut, 2)
        if share["above_6k_share"] > 0.15:
            y = filt(y, high_shelf(fs, 6500.0, -3.0))
            notes["air_shelf_db"] = -3.0
    if not loop:
        y, fixed = declick(y, fs)
        if fixed:
            notes["declicked_samples"] = fixed
    loud = audit.loudness(y, fs)
    level = loud["integrated"] if loop else loud["momentary_max"]
    harsh = audit.band_loudness(y, fs)
    wanted = target["momentary"] - (3.0 if loop else 0.0)
    goal = wanted if loop else min(wanted, target["harsh"] + (loud["momentary_max"] - harsh))
    gain_db = goal - level
    peak = audit.true_peak(y)
    ceiling_db = target["peak"] - 0.05
    if peak > 0:
        gain_db = min(gain_db, ceiling_db - audit.db(peak))
    y = y * 10 ** (gain_db / 20)
    notes["gain_db"] = round(gain_db, 2)
    if settings["trim"]:
        before = len(y)
        y = trim(y, fs)
        notes["trimmed_ms"] = round((before - len(y)) / fs * 1000, 2)
    if not loop:
        y = remove_dc(fade(y, fs, settings["fade_in_ms"], settings["fade_out_ms"]))
    return y, notes


def belt_pinned() -> list[tuple[Path, Path, str]]:
    manifest = json.loads((IMPACT_SOURCE / "manifest.json").read_text(encoding="utf-8"))
    return [(IMPACT_SOURCE / e["path"], audit.IMPACT / e["path"], e["sha256"]) for e in manifest["files"]]


def restore_belt() -> int:
    restored = 0
    for source, target, pinned in belt_pinned():
        if sha(source) != pinned:
            raise SystemExit(f"BELT source changed since its manifest: {source}")
        if not target.exists() or sha(target) != pinned:
            shutil.copyfile(source, target)
            restored += 1
    return restored


def master_generated(path: Path, loop: bool) -> dict:
    category = "ambience-bed" if loop else "ambience-call"
    fs, x = audit.read(path)
    y, notes = process(x, fs, category, loop)
    audit.write(path, fs, y)
    return {"category": category, **notes}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--restore-belt", action="store_true")
    args = parser.parse_args()
    if args.restore_belt:
        print(json.dumps({"belt_restored": restore_belt()}))
    changed = [str(t.relative_to(REPO)) for s, t, pinned in belt_pinned() if sha(t) != pinned]
    if changed:
        raise SystemExit(f"{len(changed)} BELT-pinned impact clips differ from their manifest; run with --restore-belt")
    receipt = json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {"files": {}}
    produced = {v["output_sha256"] for v in receipt["files"].values()}
    rows = {}
    for target in sorted(audit.WINTER.glob("*.wav")):
        source = WINTER_SOURCE / target.name
        rel = str(target.relative_to(REPO)).replace("\\", "/")
        backup = BACKUP / target.relative_to(ASSETS)
        current = sha(target)
        if current not in produced and current != sha(source):
            raise SystemExit(f"{rel} differs from its pinned source and from the last processed output")
        if not backup.exists():
            if current in produced:
                raise SystemExit(f"{rel} is already processed but its original backup is missing")
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, backup)
        fs, x = audit.read(source)
        y, notes = process(x, fs, "winter", False)
        if args.check:
            continue
        audit.write(target, fs, y)
        WINTER_MASTERED.mkdir(parents=True, exist_ok=True)
        audit.write(WINTER_MASTERED / target.name, fs, y)
        rows[rel] = {"category": "winter", "source": str(source.relative_to(REPO)).replace("\\", "/"),
                     "source_sha256": sha(source), "output_sha256": sha(target),
                     "mastered_copy": str((WINTER_MASTERED / target.name).relative_to(REPO)).replace("\\", "/"),
                     "samples_before": len(x), "samples_after": len(y), **notes}
    if args.check:
        print("check ok")
        return 0
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps({"schema": 2, "utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                                   "script": "Tools/AssetPipeline/audio_process.py",
                                   "script_sha256": sha(Path(__file__)), "audit_sha256": sha(Path(audit.__file__)),
                                   "belt": "Audio/Impact/Clips are BELT-pinned by BeltRangeSetup and the synth-02 manifest; kept at their original bytes",
                                   "ambience": "Resources/Ambience is mastered by synth_ambience.py through master_generated",
                                   "targets": audit.TARGETS, "settings": SETTINGS, "files": rows}, indent=1), encoding="utf-8")
    print(json.dumps({"processed": len(rows)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
