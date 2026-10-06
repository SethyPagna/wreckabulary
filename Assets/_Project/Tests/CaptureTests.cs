using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>
    /// Renders screenshots of a staged moment in the Living Room, for reviewing the look without opening the editor.
    /// Explicit: run with -testFilter Wreckabulary.Tests.CaptureTests. Output goes to WRECK_CAPTURE_DIR (or Temp/Captures).
    /// </summary>
    [Explicit, Category("Capture")]
    public class CaptureTests
    {
        ShaderCompilationScope shaderCompilationScope;

        [UnityTest]
        public IEnumerator CaptureLivingRoom() => RunWithSynchronousShaders(CaptureLivingRoomSequence());

        [UnityTest]
        public IEnumerator CaptureHubAndTutorial() => RunWithSynchronousShaders(CaptureHubAndTutorialSequence());

        [UnityTest]
        public IEnumerator CaptureLobby() => RunWithSynchronousShaders(CaptureLobbySequence());

        [UnityTest]
        public IEnumerator CaptureMaps() => RunWithSynchronousShaders(CaptureMapsSequence());

        // UTF can stop an iterator on an unexpected log without disposing it.
        [TearDown]
        public void RestoreShaderCompilation()
        {
            shaderCompilationScope?.Dispose();
            shaderCompilationScope = null;
        }

        internal IEnumerator RunWithSynchronousShaders(IEnumerator sequence)
        {
            if (shaderCompilationScope != null)
                throw new InvalidOperationException("A capture is already running on this fixture.");

            var scope = new ShaderCompilationScope();
            shaderCompilationScope = scope;
            try
            {
                while (sequence.MoveNext()) yield return sequence.Current;
            }
            finally
            {
                try
                {
                    (sequence as IDisposable)?.Dispose();
                }
                finally
                {
                    scope.Dispose();
                    if (ReferenceEquals(shaderCompilationScope, scope)) shaderCompilationScope = null;
                }
            }
        }

        sealed class ShaderCompilationScope : IDisposable
        {
#if UNITY_EDITOR
            readonly bool previousValue;
#endif
            bool disposed;

            public ShaderCompilationScope()
            {
#if UNITY_EDITOR
                previousValue = UnityEditor.EditorSettings.asyncShaderCompilation;
                // Otherwise lit objects are skipped while their shaders compile in the background.
                UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif
            }

            public void Dispose()
            {
                if (disposed) return;
#if UNITY_EDITOR
                UnityEditor.EditorSettings.asyncShaderCompilation = previousValue;
#endif
                disposed = true;
            }
        }

        IEnumerator CaptureLivingRoomSequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            Session.Clear();
            yield return SceneManager.LoadSceneAsync("LivingRoom");
            yield return new WaitForSeconds(0.5f);
            Capture(Path.Combine(dir, "1_lobby.png"));

            var joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var inputs = new ScriptedBinding[3];
            for (int i = 0; i < 3; i++) joins.Join(inputs[i] = new ScriptedBinding());
            RoundManager.Instance.CountdownTime = 0.1f;
            RoundManager.Instance.StartMatch();
            yield return new WaitForSeconds(0.5f);

            var p2 = World.Players[1];
            var p3 = World.Players[2];

            // The table bursts and P1 and P2 run for the letters.
            foreach (var s in UnityEngine.Object.FindObjectsByType<Smashable>())
                if (s.Word == "TABLE") s.Break();
            inputs[0].Next.move = new Vector2(0.6f, 0.8f);
            inputs[1].Next.move = new Vector2(-0.7f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            inputs[0].Next.move = Vector2.zero;
            inputs[1].Next.move = Vector2.zero;

            // P3 summons a SWORD, P2 opens the word wheel.
            p3.Inventory.Set("SWORDE");
            p3.Summoner.Summon("SWORD");
            p2.Inventory.Set("BEESTA");
            inputs[1].Next.spellHeld = true;
            inputs[1].Next.spellDown = true;
            yield return new WaitForSeconds(0.4f);
            Capture(Path.Combine(dir, "2_action.png"));

            inputs[1].Next.spellHeld = false;
            inputs[1].Next.spellUp = true;
            yield return new WaitForSeconds(0.6f);
            Capture(Path.Combine(dir, "3_bees.png"));

            // The same fight as one person with a mouse or a controller sees it: from behind P1.
            CameraRig.Instance.Follow(World.Players[0]);
            yield return new WaitForSeconds(0.8f);
            yield return CaptureFramed(Path.Combine(dir, "4_third_person.png"));
            CameraRig.Instance.Follow(null);
        }

        IEnumerator CaptureHubAndTutorialSequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            Session.Clear();

            // House: two roommates walk in, one sits at the typewriter. The lobby is captured on its own.
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return null;
            if (LobbyMenu.Instance) UnityEngine.Object.Destroy(LobbyMenu.Instance.gameObject);
            yield return null;
            var joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var a = joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(0.3f);
            Capture(Path.Combine(dir, "4_hub_arrival.png"));
            joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(1f);
            var typewriter = UnityEngine.Object.FindAnyObjectByType<Typewriter>();
            a.Respawn(typewriter.transform.position + Vector3.back * 1.2f + Vector3.down * 0.82f);
            typewriter.Open(a);
            yield return new WaitForSeconds(0.3f);
            Capture(Path.Combine(dir, "5_hub_typewriter.png"));

            // Tutorial, a few steps in.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.TutorialScene);
            yield return null;
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            joins.Join(input);
            input.Next.move = new Vector2(1f, 0.3f);
            yield return new WaitForSeconds(1.2f);
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            Capture(Path.Combine(dir, "6_tutorial.png"));

            // Moving Day: the bed is in, boxes are arriving.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.MovingDayScene);
            yield return null;
            joins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new ScriptedBinding());
            joins.Join(new ScriptedBinding());
            var director = UnityEngine.Object.FindAnyObjectByType<MovingDayDirector>();
            yield return new WaitForSeconds(4.5f);
            FurnitureCatalog.Spawn("BED", director.RoomNamed("Bedroom").Centre + new Vector3(0f, 0.3f, 2f), 0f, World.Transient);
            yield return new WaitForSeconds(2.5f);
            Capture(Path.Combine(dir, "7_moving_day.png"));
            Session.Clear();
        }

        IEnumerator CaptureLobbySequence()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            TryLights();
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
            yield return new WaitForSeconds(0.5f);
            var menu = LobbyMenu.Instance;
            var pages = new[]
            {
                (LobbyMenu.Home, "home"), (LobbyMenu.Play, "play"), (LobbyMenu.Loadout, "loadout"), (LobbyMenu.CareerPage, "career"),
                (LobbyMenu.Shop, "shop"), (LobbyMenu.Trophy, "trophy"), (LobbyMenu.Settings, "settings"),
            };
            for (int i = 0; i < pages.Length; i++)
            {
                menu.Open(pages[i].Item1);
                // The camera slides you aside for the side panels.
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"lobby_{i + 1}_{pages[i].Item2}.png"));
            }
            // A couch player in the party panel, opened from its chip.
            var couch = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(couch);
            menu.Open(LobbyMenu.Home);
            menu.ToggleParty();
            yield return new WaitForSeconds(1f);
            yield return CaptureFramed(Path.Combine(dir, $"lobby_{pages.Length + 1}_party.png"));
            menu.ToggleParty();
            menu.Leave(couch);
            // Every map's backdrop, seen from home.
            menu.Open(LobbyMenu.Home);
            foreach (string map in GameConfig.Current.Houses.Keys)
            {
                menu.Choose(map: map);
                // The PLAY page redraws itself after a choice; here Home has to.
                menu.Open(LobbyMenu.Home);
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"lobby_map_{map}.png"));
            }
            Session.Clear();
        }

        /// <summary>Every map in a two-player Dibs match, where the couch camera frames the whole house.</summary>
        IEnumerator CaptureMapsSequence()
        {
            TryLights();
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            foreach (string map in GameConfig.Current.Houses.Keys.ToList())
            {
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(new ScriptedBinding());
                Session.Remember(new ScriptedBinding());
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}.png"));
                // One person with a mouse and AI opponents: the view from behind and the HUD they play with.
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(DesktopBinding.Shared);
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1.5f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}_tps.png"));
                // The Tab bag over that view, with a few letters in it so a recipe lights up.
                var hud = GameHud.Active;
                if (hud && hud.LocalPlayer)
                {
                    hud.LocalPlayer.Inventory.Set("BALLSOAP");
                    hud.transform.Find("Safe HUD/Letter bag/Bag link").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.3f);
                    yield return CaptureFramed(Path.Combine(dir, $"map_{map}_bag.png"));
                    hud.transform.Find("Safe HUD/Letter bag/Bag link").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                // A house with an upstairs: also one overhead player downstairs, with the floors above lifted off.
                if (GameConfig.Current.HouseFor(map).StoreyFloors().Count < 2) continue;
                Session.Clear();
                Session.SelectMap(map);
                Match.ModeOverride = "Dibs";
                Session.Remember(new ScriptedBinding());
                yield return SceneManager.LoadSceneAsync(Session.DibsScene);
                yield return new WaitForSeconds(1.5f);
                yield return CaptureFramed(Path.Combine(dir, $"map_{map}_solo.png"));
            }
            Session.Clear();
        }

        /// <summary>Light values to try, from WRECK_LIGHT ("sun=.9;fill=.33;hemi=.52;env=.25;exp=.45"), for calibrating against the web.</summary>
        static void TryLights()
        {
            string asked = Environment.GetEnvironmentVariable("WRECK_LIGHT");
            if (string.IsNullOrEmpty(asked)) return;
            foreach (var pair in asked.Split(';'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) continue;
                switch (kv[0].Trim())
                {
                    case "sun": GraphicsOptions.Sun = v; break;
                    case "fill": GraphicsOptions.Fill = v; break;
                    case "hemi": GraphicsOptions.Hemisphere = v; break;
                    case "env": GraphicsOptions.Environment = v; break;
                    case "exp": GraphicsOptions.SetExposure(v); break;
                }
            }
            Debug.Log($"Capture lights: {asked}");
        }

        /// <summary>The capture size: WRECK_CAPTURE_SIZE as "2560x1440", or 1600 x 900.</summary>
        static Vector2Int Size()
        {
            string asked = Environment.GetEnvironmentVariable("WRECK_CAPTURE_SIZE");
            var parts = (asked ?? "").Split('x');
            return parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w > 0 && h > 0
                ? new Vector2Int(w, h) : new Vector2Int(1600, 900);
        }

        /// <summary>A free layer the overlay canvases sit on while they are captured.</summary>
        const int UiLayer = 31;

        /// <summary>Like <see cref="Capture"/>, but the camera targets the texture first so the UI lays out at its size.</summary>
        static IEnumerator CaptureFramed(string path)
        {
            var cam = Camera.main;
            var size = Size();
            var rt = new RenderTexture(size.x, size.y, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>()
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            cam.targetTexture = rt;
            // Just past the near plane: on it, depth precision clips the UI away in bands.
            var layers = ToCamera(canvases, cam, Mathf.Max(.3f, cam.nearClipPlane + .05f));
            // Canvas scalers resize in Update.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var shot = RenderWithUi(cam, rt);
            cam.targetTexture = null;
            ToOverlay(canvases, layers);
            Save(shot, rt, path);
        }

        static void Capture(string path)
        {
            var cam = Camera.main;
            var size = Size();
            var rt = new RenderTexture(size.x, size.y, 24);
            // Overlay canvases aren't drawn by cameras, so render the HUD through the camera for the capture.
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var layers = ToCamera(canvases, cam, 1f);
            Canvas.ForceUpdateCanvases();
            var shot = RenderWithUi(cam, rt);
            ToOverlay(canvases, layers);
            Save(shot, rt, path);
        }

        /// <summary>Draws the canvases through the camera, on the capture's UI layer. Returns each object's own layer.</summary>
        static (GameObject go, int layer)[] ToCamera(Canvas[] canvases, Camera cam, float planeDistance)
        {
            var layers = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Select(t => (t.gameObject, t.gameObject.layer)).ToArray();
            foreach (var (go, _) in layers) go.layer = UiLayer;
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = planeDistance;
            }
            return layers;
        }

        static void ToOverlay(Canvas[] canvases, (GameObject go, int layer)[] layers)
        {
            foreach (var c in canvases)
                if (c) c.renderMode = RenderMode.ScreenSpaceOverlay;
            foreach (var (go, layer) in layers)
                if (go) go.layer = layer;
        }

        /// <summary>
        /// The world with its post-processing, and the UI on top without it: on screen the UI is an
        /// overlay that the tone curve never touches, so a capture mustn't tone-map it either. URP clears
        /// a camera's target even when told not to, so the UI renders on its own, once over black and once
        /// over white; the difference between the two is its coverage, which lays it over the world.
        /// </summary>
        static Texture2D RenderWithUi(Camera cam, RenderTexture rt)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            int mask = cam.cullingMask;
            var clear = cam.clearFlags;
            var background = cam.backgroundColor;
            bool post = data.renderPostProcessing, hdr = cam.allowHDR;
            cam.cullingMask = mask & ~(1 << UiLayer);
            Submit(cam, rt);
            var world = Read(rt);
            cam.cullingMask = 1 << UiLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            data.renderPostProcessing = false;
            cam.allowHDR = false;
            cam.backgroundColor = Color.black;
            Submit(cam, rt);
            var black = Read(rt);
            cam.backgroundColor = Color.white;
            Submit(cam, rt);
            var white = Read(rt);
            cam.cullingMask = mask;
            cam.clearFlags = clear;
            cam.backgroundColor = background;
            data.renderPostProcessing = post;
            cam.allowHDR = hdr;

            // The target blends in linear light, so the layers combine there too.
            var linear = new float[256];
            for (int i = 0; i < 256; i++) linear[i] = Mathf.GammaToLinearSpace(i / 255f);
            var pixels = world.GetPixels32();
            var overBlack = black.GetPixels32();
            var overWhite = white.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var w = pixels[i];
                var b = overBlack[i];
                var o = overWhite[i];
                pixels[i] = new Color32(Over(linear, w.r, b.r, o.r), Over(linear, w.g, b.g, o.g), Over(linear, w.b, b.b, o.b), 255);
            }
            world.SetPixels32(pixels);
            world.Apply();
            UnityEngine.Object.Destroy(black);
            UnityEngine.Object.Destroy(white);
            return world;
        }

        /// <summary>One channel of the UI over the world: over black the UI shows its colour times its
        /// coverage; over white, that plus what it lets through.</summary>
        static byte Over(float[] linear, byte world, byte overBlack, byte overWhite)
        {
            float ui = linear[overBlack];
            float through = Mathf.Clamp01(linear[overWhite] - ui);
            return (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(ui + linear[world] * through)) * 255f);
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        static void Submit(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
                return;
            }
            var target = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = target;
        }

        static void Save(Texture2D tex, RenderTexture rt, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            Debug.Log("[Capture] " + path);
        }
    }
}
