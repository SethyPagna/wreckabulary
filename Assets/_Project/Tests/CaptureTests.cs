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

            // P1 carries the CHAIR over their head.
            var p1 = World.Players[0];
            var chair = UnityEngine.Object.FindObjectsByType<Smashable>().FirstOrDefault(s => s.Word == "CHAIR");
            if (chair)
            {
                var c = chair.GetComponent<Rigidbody>().worldCenterOfMass;
                p1.Respawn(World.Flat(c) + Vector3.right * 1.1f);
                p1.FaceTowards(Vector3.left);
                yield return new WaitForFixedUpdate();
                p1.Combat.TryGrab();
                p1.FaceTowards(Vector3.back);
            }

            // P3 summons a SWORD, P2 is halfway through spelling BEES.
            p3.Inventory.Set("SWORDE");
            p3.Summoner.Summon("SWORD");
            p2.Inventory.Set("BEESTA");
            inputs[1].Next.spellDown = true;
            yield return null;
            yield return null;
            inputs[1].Next.confirm = true; // B
            yield return null;
            yield return null;
            inputs[1].Next.confirm = true; // E
            yield return new WaitForSeconds(0.4f);
            Capture(Path.Combine(dir, "2_action.png"));

            p2.Summoner.Add(); // E
            p2.Summoner.Add(); // S
            p2.Summoner.Cast();
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
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = true;
#endif
            Session.Clear();
        }

        [UnityTest]
        public IEnumerator CaptureMapsAndDesigns()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif
            foreach (var map in new[] { Session.BedroomScene, Session.KitchenScene, Session.GardenScene })
            {
                Session.Clear();
                yield return SceneManager.LoadSceneAsync(map);
                yield return new WaitForSeconds(1.5f);
                Capture(Path.Combine(dir, $"map_{map.ToLower()}.png"));
            }

            // Creative: a cozy room in progress, one roommate spelling, one at the room menu.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.CreativeScene);
            yield return null;
            var cJoins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            var builder = cJoins.Join(new ScriptedBinding());
            var planner = cJoins.Join(new ScriptedBinding());
            foreach (var (word, x, z, yaw) in new[]
                     {
                         ("SOFA", -3.5f, 3.6f, 0f), ("TV", -3.5f, 0.6f, 180f), ("RUG", -3.5f, 2.1f, 0f), ("LAMP", -6.6f, 4.4f, 0f),
                         ("PIANO", 5.6f, 4.6f, 0f), ("STOOL", 5.6f, 3.4f, 0f), ("PLANT", 7.2f, 1.2f, 0f), ("TEDDY", -6.2f, 2.8f, 20f),
                         ("CLOCK", 2.8f, 4.9f, 0f), ("BOOKS", 7.0f, -2.2f, 0f),
                     })
                FurnitureCatalog.Spawn(word, new Vector3(x, 0f, z), yaw, World.Transient);
            yield return new WaitForSeconds(1f);
            builder.Respawn(new Vector3(1.5f, 0f, -1.5f));
            builder.Summoner.Open();
            foreach (char c in "TRE") { while (builder.Summoner.Source[builder.Summoner.Cursor] != c) builder.Summoner.Move(1); builder.Summoner.Add(); }
            var creativeDesk = UnityEngine.Object.FindAnyObjectByType<CreativeDesk>();
            planner.Respawn(creativeDesk.transform.position + Vector3.back * 1.3f + Vector3.down * 0.82f);
            creativeDesk.Open(planner);
            yield return new WaitForSeconds(0.4f);
            Capture(Path.Combine(dir, "creative.png"));

            // Furnish First: mid-race, P1 has two of three, P2 one.
            Session.Clear();
            yield return SceneManager.LoadSceneAsync(Session.FurnishFirstScene);
            yield return null;
            var ffJoins = UnityEngine.Object.FindAnyObjectByType<PlayerJoinManager>();
            for (int k = 0; k < 3; k++) ffJoins.Join(new ScriptedBinding());
            var ff = UnityEngine.Object.FindAnyObjectByType<FurnishFirstDirector>();
            ff.CountdownTime = 0.1f;
            ff.StartMatch();
            yield return new WaitForSeconds(0.5f);
            FurnitureCatalog.Spawn(ff.Checklist[0], ff.Zones[0].Centre + new Vector3(-1.5f, 0f, 1f), 0f, World.Transient);
            FurnitureCatalog.Spawn(ff.Checklist[1], ff.Zones[0].Centre + new Vector3(1.5f, 0f, 1f), 0f, World.Transient);
            FurnitureCatalog.Spawn(ff.Checklist[2], ff.Zones[1].Centre + new Vector3(0f, 0f, 1f), 0f, World.Transient);
            yield return new WaitForSeconds(4f);
            Capture(Path.Combine(dir, "furnish_first.png"));

            // A gallery of every object design, in rows, seen from the game camera's angle.
            yield return SceneManager.LoadSceneAsync(Session.GardenScene);
            yield return null;
            foreach (var f in UnityEngine.Object.FindObjectsByType<Smashable>()) UnityEngine.Object.Destroy(f.gameObject);
            foreach (var r in UnityEngine.Object.FindObjectsByType<RoundManager>()) r.enabled = false;
            foreach (var d in UnityEngine.Object.FindObjectsByType<DeliverySpawner>()) d.Running = false;
            yield return null;
            var words = GameAssets.I.words.Words.Where(w => w.category == WordCategory.Furniture).Select(w => w.word).ToList();
            const int perRow = 9;
            for (int i = 0; i < words.Count; i++)
            {
                var at = new Vector3((i % perRow) * 1.85f - 7.4f, 0f, 4.6f - (i / perRow) * 2.3f);
                var s = FurnitureCatalog.Spawn(words[i], at, 0f, null);
                s.GetComponent<Rigidbody>().isKinematic = true;
                var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
                label.font = GameAssets.I.font;
                label.text = words[i];
                label.fontSize = 2.2f;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.color = Color.black;
                label.transform.SetPositionAndRotation(at + new Vector3(0f, 0.02f, -0.9f), Quaternion.Euler(90f, 0f, 0f));
            }
            yield return new WaitForSeconds(0.3f);
            Capture(Path.Combine(dir, "designs.png"));
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = true;
#endif
            Session.Clear();
        }

        [Test]
        public void ExportSounds()
        {
            string dir = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);

            // Every effect in enum order, with a short gap between them.
            var all = new System.Collections.Generic.List<float>();
            foreach (Sound s in Enum.GetValues(typeof(Sound)))
            {
                all.AddRange(Samples(Sfx.ClipFor(s)));
                all.AddRange(new float[Synth.Rate / 3]);
            }
            WriteWav(Path.Combine(dir, "sound_effects.wav"), all.ToArray());
            foreach (Track t in Enum.GetValues(typeof(Track)))
                WriteWav(Path.Combine(dir, $"music_{t.ToString().ToLower()}.wav"), Samples(Music.ClipFor(t)));
        }

        static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            return data;
        }

        static void WriteWav(string path, float[] samples)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Synth.Rate); w.Write(Synth.Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
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
