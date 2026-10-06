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
            // A couch player in the party rail.
            var couch = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(couch);
            menu.Open(LobbyMenu.Home);
            yield return new WaitForSeconds(1f);
            yield return CaptureFramed(Path.Combine(dir, $"lobby_{pages.Length + 1}_party.png"));
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
            }
            Session.Clear();
        }

        /// <summary>Like <see cref="Capture"/>, but the camera targets the texture first so the UI lays out at its size.</summary>
        static IEnumerator CaptureFramed(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1600, 900, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>()
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            cam.targetTexture = rt;
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 0.3f;
            }
            // Canvas scalers resize in Update.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            cam.targetTexture = null;
            foreach (var c in canvases)
                if (c) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Save(rt, path);
        }

        static void Capture(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1600, 900, 24);

            // Overlay canvases aren't drawn by cameras, so render the HUD through the camera for the capture.
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Save(rt, path);
        }

        static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            Debug.Log("[Capture] " + path);
        }
    }
}
