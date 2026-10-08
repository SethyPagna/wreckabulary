# Editing the current Unity game

Open this checkout with Unity **6000.6.3f1**. Use **Wreckabulary → Open Current Workspace**. The Hub is the default Play entry; choose **Play the opened scene** to test the level you are editing. Your open scene is restored after Play.

| Edit | Current source |
| --- | --- |
| Hub layout, desk, door, decorations | `Assets/_Project/Scenes/Hub.unity` |
| Arena presentation / scene overrides | `Assets/_Project/Scenes/LivingRoom.unity` |
| Shared Pinwheel house geometry and furniture | `Assets/_Project/Resources/Worlds/PinwheelHouse.prefab` |
| Garden Courtyard geometry and furniture | `Assets/_Project/Resources/Worlds/GardenCourtyard.prefab` |
| Cooperative Moving Day scene | `Assets/_Project/Scenes/MovingDay.unity` |
| Tutorial layout / teaching anchors | `Assets/_Project/Scenes/Tutorial.unity` |
| Character, imported model, outfit preview | `Assets/_Project/Prefabs/Player.prefab` |
| Gameplay HUD / main menu | `Scripts/Game/GameHud.cs` / `Scripts/UI/FrontDoorMenu.cs` under `Assets/_Project` |
| Inventory layout | `Assets/_Project/Resources/UI/Inventory/Inventory.uxml` and `.uss` |
| Room boundaries, door graph, spawn rules, objectives, recipes | `Assets/_Project/Data/Config` |

The house prefabs are real editable geometry and furniture. Play uses the authored scene instance when its map matches the selected map, otherwise the selected map prefab. Round reset restores the initial authored furniture transforms and components. Edit the shared prefab to affect all modes; apply scene overrides deliberately when you want to share them.

JSON still defines logical room boundaries, connectivity, objectives and spawn rules. Keep those in sync when changing room dimensions or adding rooms; moving a decorative prop does not require regenerating JSON. Existing world prefabs are never silently regenerated from JSON.

The saved camera and **Authoring Preview (Editor Only)** show the current character and close third-person lens before Play. This visual preview contains no player controller, disables itself on Play, and is stripped from builds. Edit the Player prefab for actual character changes. Runtime cameras follow player count and input, so tune `CameraRig` for gameplay framing.

Hub/menu and gameplay HUD layouts are currently built in C#. Their workspace buttons open the actual source. The inventory layout is a separate UI Builder presentation asset. Empty legacy TMP seed labels in the HUD supply references used by the current runtime controller.

**Authoring → Upgrade Current Scenes** creates missing authored assets once and preserves subsequent edits. It backs up original scenes/prefabs to local `Logs/authoring-backups`. The former prototype rebuild is isolated under **Wreckabulary → Legacy** and cannot overwrite production scenes. Preserved prototype geometry lives under `Assets/_Project/Editor/Legacy`; it is excluded from shipped scene content. Test scenes under `Assets/_Project/Tests` are test fixtures, not game entry points.

Current build scenes remain Hub, Tutorial, LivingRoom and MovingDay with Hub first. No version-numbered scene copies are used by the build.
