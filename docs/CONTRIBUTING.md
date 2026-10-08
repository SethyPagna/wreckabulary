# Team workflow

## Branches

- `main`: always playable. Only merge through pull requests.
- `feature/<short-name>`: one feature per branch, e.g. `feature/word-wheel`.
- `fix/<short-name>`: bug fixes.

## Commits

Short, present tense, prefixed by area:

```
letters: burst tiles on smash
player: drop letters on hit
ui: add word wheel
```

## Unity rules

- Start with **Wreckabulary → Open Current Workspace** and the [authoring guide](UNITY-WORKSPACE.md). Use **Play the opened scene** when testing a scene directly; normal Play starts at the Hub.
- **One person edits a scene at a time.** Say it in the group chat before opening `LivingRoom.unity`. Prefer building in prefabs and your own test scene.
- Edit shared houses through the current world prefabs and apply scene overrides deliberately. Legacy prototype generation creates isolated references; it cannot replace production scenes.
- Always commit `.meta` files together with their assets.
- Never commit `Library/`, `Temp/` or `Builds/`.
- Big files (models, textures, audio) go through Git LFS automatically.

## Smart merge for scenes and prefabs

Add this to your local git config (path depends on your Unity install):

```
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'<UnityEditorPath>/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
```

## Pull requests

1. Pull `main` and test your branch in Play mode.
2. Open a PR, fill in the template, and ask one teammate to review.
3. Squash and merge.
