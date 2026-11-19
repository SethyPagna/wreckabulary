import argparse
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
WEB_ICONS = "Web/public/art/icons"
UNITY_ICONS = "Assets/_Project/Resources/UI/Items"
HOUSE_ITEMS = "ArtSource/Collections/HouseItemsV1/manifest.json"


def parse_args():
    parser = argparse.ArgumentParser(description="Copy web item icons over the same-named in-game icons.")
    parser.add_argument("--repo", default=os.path.dirname(os.path.dirname(HERE)))
    parser.add_argument("--check", action="store_true", help="report differing icons without copying; exit 1 if any")
    return parser.parse_args()


def icon_words(folder):
    return {name[:-4] for name in os.listdir(folder) if name.endswith(".png")}


def read(path):
    with open(path, "rb") as handle:
        return handle.read()


def main():
    args = parse_args()
    repo = os.path.abspath(args.repo)
    web = os.path.join(repo, WEB_ICONS)
    unity = os.path.join(repo, UNITY_ICONS)
    with open(os.path.join(repo, HOUSE_ITEMS), encoding="utf-8") as handle:
        rendered_by_unity = {entry["word"] for entry in json.load(handle)["entries"]}
    web_words, unity_words = icon_words(web), icon_words(unity)
    shared = sorted((web_words & unity_words) - rendered_by_unity)
    differs = [w for w in shared if read(os.path.join(web, w + ".png")) != read(os.path.join(unity, w + ".png"))]
    if not args.check:
        for word in differs:
            shutil.copyfile(os.path.join(web, word + ".png"), os.path.join(unity, word + ".png"))
    print("ICON_SYNC " + json.dumps({
        "check": args.check,
        "differs": differs,
        "copied": [] if args.check else differs,
        "unchanged": len(shared) - len(differs),
        "skipped_unity_rendered": sorted(rendered_by_unity & unity_words),
        "web_only": sorted(web_words - unity_words),
        "unity_only": sorted(unity_words - web_words - rendered_by_unity),
    }))
    if args.check and differs:
        sys.exit(1)


if __name__ == "__main__":
    main()
