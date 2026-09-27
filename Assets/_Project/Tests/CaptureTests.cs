using System;
using System.Collections;
using System.IO;
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
        [UnityTest]
        public IEnumerator CaptureLivingRoom()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            // Otherwise lit objects are skipped while their shaders compile in the background.
            UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif

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
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = true;
#endif
        }

        [UnityTest]
        public IEnumerator CaptureHubAndTutorial()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif
            Session.Clear();

            // House: two roommates walk in, one sits at the typewriter.
            yield return SceneManager.LoadSceneAsync(Session.HubScene);
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
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = true;
#endif
            Session.Clear();
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
