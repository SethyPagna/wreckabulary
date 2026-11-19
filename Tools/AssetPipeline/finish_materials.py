import argparse
import copy
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
MATERIALS = "Assets/_Project/Data/Generated/materials.json"
PATTERNS = "Assets/_Project/Materials/Library/Patterns/"
SEASONAL = "Assets/_Project/Art/Seasonal/Winter/Finishes"
CANDY_STRIPES = PATTERNS + "candy_stripes.png"
ARCADE_GRID = PATTERNS + "arcade_grid.png"
WINTER_FROST = PATTERNS + "winter_frost.png"
WINTER_GLINTS = PATTERNS + "winter_glints.png"
NEON_CYAN = "#2BE6FF"
NEON_MAGENTA = "#FF3FD2"
SNOW_GLINT = "#B9D3E6"


def fam(colour, roughness, metallic=0.0, emit=None, glow=0.0, base_map=None, emission_map=None):
    return {"colour": colour, "roughness": roughness, "metallic": metallic, "emit": emit, "glow": glow,
            "baseMap": base_map, "emissionMap": emission_map}


def candy(colour, roughness=0.26, metallic=0.0, emit=None, glow=0.0, striped=True):
    return fam(colour, roughness, metallic, emit, glow, CANDY_STRIPES if striped else None)


def cabinet(colour, grid=NEON_CYAN, roughness=0.42, glow=0.8):
    return fam(colour, roughness, 0.0, grid, glow, None, ARCADE_GRID)


def neon(colour, glow, roughness=0.38, metallic=0.0, emit=None):
    return fam(colour, roughness, metallic, emit or colour, glow)


def frost(colour, roughness=0.62, metallic=0.0, glint=0.9):
    return fam(colour, roughness, metallic, SNOW_GLINT, glint, WINTER_FROST, WINTER_GLINTS)


FINISHES = {
    "Candy": {
        "wood": candy("#C7A8FF"),
        "wood_light": candy("#FF9DC6"),
        "wood_dark": candy("#C94377"),
        "kraft": candy("#FFBE95"),
        "plush": candy("#FFA8CE"),
        "cream": candy("#FFE066"),
        "ceramic": candy("#FFD0E2", 0.16),
        "blue": candy("#9FE8FF"),
        "red": candy("#FF4F9A"),
        "green": candy("#BDF7D8"),
        "dark": candy("#6B3A2A", 0.3),
        "soap": candy("#CDB2FF", 0.18),
        "ivory": candy("#FFE0F0", 0.2, striped=False),
        "white": candy("#C8F5DF", 0.24, striped=False),
        "pink": candy("#FF9E7A", 0.22, striped=False),
        "leaf": candy("#8EEBC0", 0.3, striped=False),
        "gold": candy("#F2A08F", 0.14, 0.55, striped=False),
        "metal": candy("#FFB3C1", 0.14, 0.45, striped=False),
        "glow": candy("#FFA6D2", 0.3, emit="#FF4FA8", glow=1.2, striped=False),
        "water": candy("#FF9FCB", 0.1, striped=False),
        "glass": candy("#FFC6E1", 0.08, striped=False),
    },
    "Arcade": {
        "wood": cabinet("#26206A"),
        "wood_light": cabinet("#4A3AA8"),
        "wood_dark": cabinet("#0E1030"),
        "kraft": cabinet("#173C66"),
        "plush": cabinet("#3A2272", NEON_MAGENTA),
        "ceramic": cabinet("#2A2F7A", roughness=0.3),
        "blue": cabinet("#1A3FB0"),
        "red": cabinet("#7A1260", NEON_MAGENTA),
        "green": cabinet("#0F5A50"),
        "dark": cabinet("#0A0A1A", NEON_MAGENTA),
        "cream": neon(NEON_CYAN, 0.7),
        "ivory": neon("#FFE83D", 0.6),
        "gold": neon(NEON_MAGENTA, 0.7, 0.3, 0.3),
        "pink": neon("#FF4FB8", 0.6),
        "leaf": neon("#6DFF3C", 0.6),
        "soap": neon("#8CFF4A", 0.5, 0.2),
        "white": neon("#D6C4FF", 0.3),
        "glow": neon("#B8FFFF", 1.6, 0.3, emit="#3FF8FF"),
        "metal": neon("#8FF4FF", 0.12, 0.1, 0.85, NEON_CYAN),
        "water": neon(NEON_CYAN, 0.4, 0.1),
        "glass": neon("#FF6FE0", 0.3, 0.08),
    },
}

SEASONAL_FINISHES = {
    "Winter": {
        "blue": frost("#2E7A5C"),
        "red": frost("#8C1131"),
        "ivory": frost("#CFE7F7", 0.5),
        "gold": frost("#EEF4F8", 0.4, 0.2),
        "metal": fam("#E6F6FF", 0.15, 0.7, SNOW_GLINT, 0.3, None, WINTER_GLINTS),
        "wood_dark": frost("#1F4A3A"),
    },
}


def linear(hex_colour):
    value = hex_colour.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(value[i:i + 2], 16) / 255
        out.append(round(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4, 4))
    return out


def entry(family, finish, spec, classic):
    emissive = [0, 0, 0]
    if spec["emit"] and spec["glow"] > 0:
        emissive = [round(c * spec["glow"], 4) for c in linear(spec["emit"])]
    alpha = classic["alphaMode"] if classic else "OPAQUE"
    base = linear(spec["colour"]) + [classic["baseColor"][3] if classic and alpha == "BLEND" else 1]
    return {"name": f"{family}_{finish}", "baseColor": base, "metallic": spec["metallic"],
            "roughness": spec["roughness"], "emissive": emissive, "alphaMode": alpha,
            "alphaCutoff": classic["alphaCutoff"] if classic else 0.5,
            "doubleSided": classic["doubleSided"] if classic else False,
            "baseMap": spec["baseMap"], "normalMap": None, "emissionMap": spec["emissionMap"],
            "family": family, "skin": finish,
            "sources": list(classic["sources"]) if classic else []}


def apply(library):
    library = copy.deepcopy(library)
    by_name = {m["name"]: m for m in library["materials"]}
    for finish, families in FINISHES.items():
        for family, spec in families.items():
            classic = by_name.get(family + "_Classic")
            if classic is None:
                raise RuntimeError(f"{family}_{finish} has no {family}_Classic to follow")
            by_name[f"{family}_{finish}"] = entry(family, finish, spec, classic)
    library["materials"] = [by_name[n] for n in sorted(by_name)]
    seasonal = []
    for finish, families in SEASONAL_FINISHES.items():
        for family, spec in sorted(families.items()):
            classic = by_name.get(family + "_Classic")
            if classic is None:
                raise RuntimeError(f"{family}_{finish} has no {family}_Classic to follow")
            seasonal.append(dict(entry(family, finish, spec, classic), folder=SEASONAL))
    library["seasonal"] = seasonal
    return library


def main():
    parser = argparse.ArgumentParser(description="Write the Candy, Arcade and Winter finish specs into materials.json.")
    parser.add_argument("--repo", default=REPO)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    path = os.path.join(args.repo, MATERIALS)
    with open(path, encoding="utf-8") as handle:
        before = json.load(handle)
    after = apply(before)
    changed = json.dumps(before, indent=1) != json.dumps(after, indent=1)
    if not args.check and changed:
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(after, handle, indent=1)
    print("FINISH_MATERIALS " + json.dumps({"check": args.check, "changed": changed,
                                            "materials": len(after["materials"]), "seasonal": len(after["seasonal"])}))
    if args.check and changed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
