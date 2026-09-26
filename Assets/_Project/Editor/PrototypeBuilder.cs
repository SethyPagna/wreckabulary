using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// Generates the prototype's materials, prefabs and LivingRoom scene from code.
    /// Menu: Wreckabulary → Rebuild Prototype. Re-running overwrites the generated files,
    /// so once the team starts hand-editing LivingRoom.unity, stop using it for that scene.
    /// Batch mode: Unity -batchmode -executeMethod Wreckabulary.EditorTools.PrototypeBuilder.BuildAll
    /// </summary>
    public static class PrototypeBuilder
    {
        const string Root = "Assets/_Project";
        const string MatDir = Root + "/Materials";
        const string GeneratedMatDir = Root + "/Materials/Generated";
        const string PrefabDir = Root + "/Prefabs";
        const string ScenePath = Root + "/Scenes/LivingRoom.unity";
        const string AssetsPath = Root + "/Resources/GameAssets.asset";
        const string WordsPath = Root + "/Data/Words.asset";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Wreckabulary/Rebuild Prototype")]
        static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild prototype",
                    "This regenerates the prototype materials, prefabs and overwrites LivingRoom.unity. Continue?",
                    "Rebuild", "Cancel")) return;
            BuildAll();
        }

        /// <summary>Imports TextMeshPro's essential resources (font + settings) if missing.</summary>
        public static void ImportTmpEssentials()
        {
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null) return;
            UnityEditor.AssetPackage.Package.Import(TmpEssentials, false);
            AssetDatabase.Refresh();
        }

        public static void BuildAll()
        {
            ImportTmpEssentials();
            EnsureLayers();
            Directory.CreateDirectory(GeneratedMatDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Scenes");

            var assets = BuildGameAssets();
            GameAssets.I = assets;
            GameAssets.EditorTintFactory = SaveTint;
            try
            {
                assets.tilePrefab = BuildTilePrefab(assets);
                assets.playerPrefab = BuildPlayerPrefab(assets);
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                BuildScene(assets);
            }
            finally
            {
                GameAssets.EditorTintFactory = null;
            }
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wreckabulary] Prototype rebuilt: " + ScenePath);
        }

        // ---------------------------------------------------------------- setup

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = "Player";
            layers.GetArrayElementAtIndex(9).stringValue = "Tile";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Lit(string name, Color color, float smoothness = 0.25f, float metallic = 0f, string dir = MatDir)
        {
            string path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material SaveTint(Color c)
        {
            string name = "Tint_" + ColorUtility.ToHtmlStringRGB(c);
            return Lit(name, c, 0.2f, 0f, GeneratedMatDir);
        }

        static GameAssets BuildGameAssets()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(AssetsPath);
            if (!assets)
            {
                assets = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }

            var words = AssetDatabase.LoadAssetAtPath<WordDatabase>(WordsPath);
            if (!words)
            {
                words = ScriptableObject.CreateInstance<WordDatabase>();
                AssetDatabase.CreateAsset(words, WordsPath);
            }
            Set(words, "csv", AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/word_list.csv"));

            assets.font = TMP_Settings.defaultFontAsset;
            assets.blockMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            assets.tileCommon = Lit("Tile_Common", Hex("E3B27A"), 0.3f);
            assets.tileRare = Lit("Tile_Rare", Hex("7FC4B8"), 0.4f);
            assets.tileLegendary = Lit("Tile_Legendary", Hex("F2C14E"), 0.7f, 0.6f);
            assets.tintBase = Lit("Tint_Base", Color.white, 0.2f);
            assets.cardboard = Lit("Cardboard", Hex("C8A06E"), 0.1f);
            assets.words = words;
            assets.tints.RemoveAll(m => !m);
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        // ---------------------------------------------------------------- prefabs

        static LetterTile BuildTilePrefab(GameAssets assets)
        {
            var go = new GameObject("LetterTile") { layer = LayerMask.NameToLayer("Tile") };
            var size = Vector3.one * 0.36f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<BoxCollider>().size = size;

            var mesh = new GameObject("Mesh") { layer = go.layer };
            mesh.transform.SetParent(go.transform, false);
            mesh.transform.localScale = size;
            mesh.AddComponent<MeshFilter>().sharedMesh = assets.blockMesh;
            var renderer = mesh.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = assets.tileCommon;
            var labels = LetterBlocks.AddLabels(go.transform, "A", size, false);

            var tile = go.AddComponent<LetterTile>();
            Set(tile, "labels", labels);
            Set(tile, "body", renderer);
            return Save(go, "LetterTile").GetComponent<LetterTile>();
        }

        static PlayerController BuildPlayerPrefab(GameAssets assets)
        {
            int layer = LayerMask.NameToLayer("Player");
            var root = new GameObject("Player") { layer = layer };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.62f, 0f);
            capsule.radius = 0.42f;
            capsule.height = 1.24f;
            capsule.sharedMaterial = Slippery();

            var bodyMat = Lit("Player_Body", Color.white, 0.45f);
            var white = Lit("Eye_White", Color.white, 0.8f);
            var black = Lit("Eye_Pupil", Hex("1E1A1A"), 0.9f);

            var visual = Child(root.transform, "Visual", Vector3.zero);
            var body = Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, 0.62f, 0f), new Vector3(0.84f, 0.62f, 0.84f), bodyMat);
            var sweater = Primitive(PrimitiveType.Cylinder, visual, "Sweater", new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 0.2f, 0.9f), bodyMat);
            Primitive(PrimitiveType.Sphere, visual, "EyeL", new Vector3(-0.14f, 1.0f, 0.33f), Vector3.one * 0.17f, white);
            Primitive(PrimitiveType.Sphere, visual, "EyeR", new Vector3(0.14f, 1.0f, 0.33f), Vector3.one * 0.17f, white);
            Primitive(PrimitiveType.Sphere, visual, "PupilL", new Vector3(-0.14f, 1.0f, 0.41f), Vector3.one * 0.08f, black);
            Primitive(PrimitiveType.Sphere, visual, "PupilR", new Vector3(0.14f, 1.0f, 0.41f), Vector3.one * 0.08f, black);
            // Hands are unscaled grip points so held weapons keep their size; the sphere is a child.
            var handL = Child(visual, "HandL", new Vector3(-0.52f, 0.55f, 0.05f));
            var handR = Child(visual, "HandR", new Vector3(0.52f, 0.55f, 0.05f));
            var fistL = Primitive(PrimitiveType.Sphere, handL, "Fist", Vector3.zero, Vector3.one * 0.24f, bodyMat);
            var fistR = Primitive(PrimitiveType.Sphere, handR, "Fist", Vector3.zero, Vector3.one * 0.24f, bodyMat);
            var hold = Child(visual, "HoldPoint", new Vector3(0f, 0.85f, 0.8f));
            var overhead = Child(visual, "OverheadPoint", new Vector3(0f, 1.8f, 0.1f));

            var initial = new GameObject("Initial") { layer = layer };
            initial.transform.SetParent(visual, false);
            initial.transform.localPosition = new Vector3(0f, 0.6f, 0.456f);
            initial.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var initialText = initial.AddComponent<TextMeshPro>();
            initialText.font = assets.font;
            initialText.text = "W";
            initialText.fontStyle = FontStyles.Bold;
            initialText.alignment = TextAlignmentOptions.Center;
            initialText.rectTransform.sizeDelta = new Vector2(0.5f, 0.38f);
            initialText.enableAutoSizing = true;
            initialText.fontSizeMin = 0.1f;
            initialText.fontSizeMax = 20f;

            var controller = root.AddComponent<PlayerController>();
            controller.visual = visual;
            controller.handL = handL;
            controller.handR = handR;
            controller.holdPoint = hold;
            controller.overheadPoint = overhead;
            controller.bodyRenderers = new Renderer[] { body.GetComponent<Renderer>(), fistL.GetComponent<Renderer>(), fistR.GetComponent<Renderer>() };
            controller.sweaterRenderers = new Renderer[] { sweater.GetComponent<Renderer>() };
            controller.initialLabel = initialText;

            root.AddComponent<LetterInventory>();
            root.AddComponent<PlayerHealth>();
            root.AddComponent<PlayerCombat>();
            var summoner = root.AddComponent<Summoner>();
            Set(summoner, "database", assets.words);

            // Overhead HUD (letters + word wheel), kept upright and facing the camera by PlayerHud.
            var hudGo = new GameObject("Hud") { layer = layer };
            hudGo.transform.SetParent(root.transform, false);
            hudGo.transform.localPosition = Vector3.up * 2f;
            var letters = HudText(hudGo.transform, "Letters", Vector3.zero, 3.2f, TextAlignmentOptions.Center, assets);
            var wheel = HudText(hudGo.transform, "Wheel", new Vector3(0f, 0.3f, 0f), 2.6f, TextAlignmentOptions.Bottom, assets);
            wheel.rectTransform.pivot = new Vector2(0.5f, 0f);
            wheel.rectTransform.sizeDelta = new Vector2(4f, 3f);
            var hud = hudGo.AddComponent<PlayerHud>();
            Set(hud, "player", controller);
            Set(hud, "lettersText", letters);
            Set(hud, "wheelText", wheel);

            SetLayerRecursive(root, layer);
            return Save(root, "Player").GetComponent<PlayerController>();
        }

        static TextMeshPro HudText(Transform parent, string name, Vector3 pos, float size, TextAlignmentOptions align, GameAssets assets)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = assets.font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(40, 26, 18, 255);
            t.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            return t;
        }

        static PhysicsMaterial Slippery()
        {
            string path = MatDir + "/Player_Slippery.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m) return m;
            m = new PhysicsMaterial("Player_Slippery")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene(GameAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("F4E6D2");
            RenderSettings.ambientEquatorColor = Hex("C9B6A0");
            RenderSettings.ambientGroundColor = Hex("7A6555");

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("FFE9CF");
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("2E2A36");
            camGo.transform.position = new Vector3(0f, 13.5f, -12.5f);
            camGo.transform.LookAt(new Vector3(0f, 0f, 0.2f));
            camGo.AddComponent<CameraRig>();
            camGo.AddComponent<AudioListener>();

            BuildRoom();
            var furniture = BuildFurniture();

            var spawns = new GameObject("SpawnPoints").transform;
            var spawnPositions = new[] { new Vector3(-4.8f, 0f, -2.8f), new Vector3(4.8f, 0f, -2.8f), new Vector3(-2.9f, 0f, 3.2f), new Vector3(2.9f, 0f, 3.2f) };
            var spawnPoints = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
                spawnPoints[i] = Child(spawns, $"Spawn {i + 1}", spawnPositions[i]);

            var game = new GameObject("Game");
            var pool = game.AddComponent<TilePool>();
            Set(pool, "tilePrefab", assets.tilePrefab);
            var roomBuilder = game.AddComponent<RoomBuilder>();
            Set(roomBuilder, "furnitureRoot", furniture);
            var joins = game.AddComponent<PlayerJoinManager>();
            Set(joins, "playerPrefab", assets.playerPrefab);
            Set(joins, "spawnPoints", spawnPoints);
            Set(joins, "playersRoot", new GameObject("Players").transform);
            var deliveries = game.AddComponent<DeliverySpawner>();
            var hud = BuildHud(assets);
            var rounds = game.AddComponent<RoundManager>();
            Set(rounds, "joins", joins);
            Set(rounds, "room", roomBuilder);
            Set(rounds, "deliveries", deliveries);
            Set(rounds, "hud", hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var floor = Lit("Floor_Wood", Hex("C9A27E"), 0.35f);
            var cream = Lit("Wall_Cream", Hex("F1E4CC"), 0.1f);
            var sage = Lit("Wall_Sage", Hex("A9BF9F"), 0.1f);
            var terracotta = Lit("Trim_Terracotta", Hex("C4694A"), 0.2f);
            var rug = Lit("Rug_Sage", Hex("7FA08A"), 0.05f);
            var glass = Lit("Window_Sky", Hex("BFE3F2"), 0.9f);

            Block(room, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(17f, 0.2f, 12.3f), floor);
            Block(room, "Rug", new Vector3(0f, 0.005f, 1f), new Vector3(7.5f, 0.01f, 4.6f), rug, collider: false);
            Block(room, "Wall Back", new Vector3(0f, 1.6f, 6.3f), new Vector3(17.6f, 3.2f, 0.3f), cream);
            Block(room, "Wall Left", new Vector3(-8.65f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 12.9f), sage);
            Block(room, "Wall Right", new Vector3(8.65f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 12.9f), sage);
            Block(room, "Baseboard Back", new Vector3(0f, 0.12f, 6.12f), new Vector3(17f, 0.24f, 0.06f), terracotta, collider: false);
            Block(room, "Window", new Vector3(-4f, 1.9f, 6.14f), new Vector3(2.6f, 1.4f, 0.04f), glass, collider: false);
            Block(room, "Window Frame", new Vector3(-4f, 1.9f, 6.145f), new Vector3(2.8f, 1.6f, 0.02f), terracotta, collider: false);

            // The front wall is invisible so the camera can see in.
            var front = new GameObject("Wall Front (invisible)");
            front.transform.SetParent(room, false);
            front.transform.position = new Vector3(0f, 1.6f, -6.3f);
            front.AddComponent<BoxCollider>().size = new Vector3(17.6f, 3.2f, 0.3f);
            var ceiling = new GameObject("Ceiling (invisible)");
            ceiling.transform.SetParent(room, false);
            ceiling.transform.position = new Vector3(0f, 9f, 0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(17.6f, 0.3f, 12.9f);

            var sign = new GameObject("Sign");
            sign.transform.SetParent(room, false);
            sign.transform.position = new Vector3(3.5f, 2.2f, 6.13f);
            sign.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var text = sign.AddComponent<TextMeshPro>();
            text.font = GameAssets.I.font;
            text.text = "Home Sweet Home";
            text.fontSize = 5f;
            text.fontStyle = FontStyles.Italic | FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Hex("C4694A");
            text.rectTransform.sizeDelta = new Vector2(6f, 1f);
        }

        static Transform BuildFurniture()
        {
            var root = new GameObject("Furniture").transform;
            Furniture(root, "SOFA", new Vector3(0f, 0f, 4.8f), 0f, new Vector3(1.0f, 0.9f, 1.0f), 0, Hex("C8664B"), 16f, 45f);
            Furniture(root, "TABLE", new Vector3(0f, 0f, 1.4f), 0f, new Vector3(0.62f, 0.45f, 1.0f), 0, Hex("A8744A"), 10f, 30f);
            Furniture(root, "PLATE", new Vector3(-0.75f, 0.46f, 1.4f), 0f, new Vector3(0.26f, 0.08f, 0.26f), 0, Hex("F3EFE6"), 0.6f, 8f);
            Furniture(root, "MUG", new Vector3(0.95f, 0.46f, 1.4f), 0f, new Vector3(0.26f, 0.3f, 0.26f), 0, Hex("5E8FC7"), 0.5f, 8f);
            Furniture(root, "LAMP", new Vector3(-7.0f, 0f, 5.0f), 0f, Vector3.one * 0.5f, 1, Hex("F2D48A"), 3f, 20f);
            Furniture(root, "VASE", new Vector3(7.0f, 0f, 5.0f), 0f, Vector3.one * 0.45f, 1, Hex("5FA8A0"), 2.5f, 15f);
            Furniture(root, "CHAIR", new Vector3(-4.2f, 0f, 1.3f), 25f, Vector3.one * 0.5f, 3, Hex("B5835A"), 5f, 25f);
            Furniture(root, "BOOKS", new Vector3(4.2f, 0f, 1.6f), -15f, new Vector3(0.7f, 0.22f, 0.5f), 1, Hex("8C5A9E"), 4f, 20f);
            Furniture(root, "PILLOW", new Vector3(-1.5f, 0f, -2.6f), 10f, Vector3.one * 0.4f, 3, Hex("E7A4B3"), 2f, 12f);
            Furniture(root, "PLANT", new Vector3(7.0f, 0f, -4.6f), 0f, Vector3.one * 0.5f, 1, Hex("6FAE5A"), 3f, 18f);
            Furniture(root, "RADIO", new Vector3(-7.0f, 0f, -4.6f), 0f, Vector3.one * 0.42f, 3, Hex("E08A3C"), 3f, 18f);
            Furniture(root, "CLOCK", new Vector3(2.4f, 0f, -2.8f), -20f, Vector3.one * 0.42f, 3, Hex("D9C29A"), 3f, 18f);
            DeliveryBox(root, "BOX", new Vector3(-5.5f, 0f, -0.8f));
            DeliveryBox(root, "WAX", new Vector3(5.6f, 0f, -0.6f));
            World.ClearTransient();
            return root;
        }

        static void Furniture(Transform root, string word, Vector3 pos, float yaw, Vector3 block, int perRow, Color color, float mass, float health)
        {
            var go = new GameObject(word);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var built = go.AddComponent<LetterBuilt>();
            built.word = word;
            built.blockSize = block;
            built.perRow = perRow;
            built.color = color;
            built.Build();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Smashable>().Init(word, health);
        }

        static void DeliveryBox(Transform root, string word, Vector3 pos)
        {
            var box = DeliverySpawner.CreateBox(word, pos);
            box.transform.SetParent(root, true);
        }

        static GameHud BuildHud(GameAssets assets)
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var title = UiText(canvasGo.transform, "Title", new Vector2(0.5f, 0.62f), new Vector2(1600f, 220f), 150f, assets);
            var subtitle = UiText(canvasGo.transform, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(1600f, 90f), 44f, assets);
            var timer = UiText(canvasGo.transform, "Timer", new Vector2(0.5f, 0.95f), new Vector2(600f, 90f), 60f, assets);
            var score = UiText(canvasGo.transform, "Scoreboard", new Vector2(0.5f, 0.05f), new Vector2(1800f, 80f), 40f, assets);

            var hud = canvasGo.AddComponent<GameHud>();
            Set(hud, "title", title);
            Set(hud, "subtitle", subtitle);
            Set(hud, "timer", timer);
            Set(hud, "scoreboard", score);
            return hud;
        }

        static TextMeshProUGUI UiText(Transform parent, string name, Vector2 anchor, Vector2 size, float fontSize, GameAssets assets)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = assets.font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = Hex("FFF4E0");
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(40, 26, 18, 255);
            t.text = "";
            return t;
        }

        // ---------------------------------------------------------------- helpers

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = parent.gameObject.layer;
            return go.transform;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
        }

        static GameObject Save(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"No serialized field '{field}' on {target.GetType().Name}"); return; }
            if (value is Object[] array)
            {
                p.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
            }
            else p.objectReferenceValue = (Object)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
