using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LookSheetCapture
    {
        const int Width = 256, Height = 320, SmallWidth = 32, SmallHeight = 40;
        const float CouchPitch = 54f, Yaw = 160f;

        [Serializable] sealed class Shot { public string id, outfit, letter; public string[] visible, rigid; }
        [Serializable] sealed class Report
        {
            public string utc, unity, graphics, status = "IN PROGRESS", error = "";
            public string layout = "looks_sheet.png: columns = the six looks in Shop order; rows = front, couch (orthographic, 54 degrees down), couch at 40 px tall upscaled 8x, chest close-up. seats_sheet.png: columns = P1..P4 default outfits; same rows.";
            public List<Shot> looks = new List<Shot>(), seats = new List<Shot>();
        }

        enum View { Front, Couch, Small, Chest }

        [MenuItem("Wreckabulary/Art/Capture look signature sheets")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Capture in Edit Mode with the active render pipeline and a graphics device.");
            var library = ModelLibrary.Load();
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            if (!library || !library.Find("Avatar/Avatar") || !idle) throw new InvalidOperationException("The avatar and its Idle clip are required.");
            if (!WardrobeRig.Art) throw new InvalidOperationException("Resources/" + WardrobeRig.ArtPath + " is missing; run Tools/AssetPipeline/build_signature_pieces.py first.");
            var wardrobe = GameConfig.Current.Wardrobe;
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildNotes/F/looks-" + stamp));
            if (Directory.Exists(folder)) throw new IOException("Preserve existing sheet: " + folder);
            Directory.CreateDirectory(folder);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName };
            try
            {
                var looks = CosmeticBundles.All.Select(look => (look.Id, outfit: look.Apply(wardrobe.Default, wardrobe), seat: 0)).ToList();
                if (looks.Any(look => look.outfit == null)) throw new InvalidOperationException("A look does not fit the wardrobe.");
                var seats = Enumerable.Range(0, 4).Select(seat => ("P" + (seat + 1), outfit: PlayerAppearance.SeatDefault(wardrobe, seat, GameAssets.I.PlayerColor(seat)), seat)).ToList();
                Sheet(Path.Combine(folder, "looks_sheet.png"), looks, idle, report.looks);
                Sheet(Path.Combine(folder, "seats_sheet.png"), seats, idle, report.seats);
                report.status = "CAPTURED; pending owner review";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); throw; }
            finally { File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true)); }
            Debug.Log("[Wreckabulary] LOOK_SHEETS: " + folder + "; looks=" + report.looks.Count + "; seats=" + report.seats.Count);
        }

        static void Sheet(string path, List<(string id, Outfit outfit, int seat)> columns, AnimationClip idle, List<Shot> shots)
        {
            var views = new[] { View.Front, View.Couch, View.Small, View.Chest };
            var sheet = new Texture2D(columns.Count * Width, views.Length * Height, TextureFormat.RGBA32, false);
            try
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    Shot shot = null;
                    for (int r = 0; r < views.Length; r++)
                    {
                        var image = Render(columns[c].outfit, columns[c].seat, views[r], idle, r == 0 ? s => shot = s : null);
                        try { Blit(image, sheet, c * Width, (views.Length - 1 - r) * Height); }
                        finally { Object.DestroyImmediate(image); }
                    }
                    shot.id = columns[c].id;
                    shots.Add(shot);
                }
                sheet.Apply();
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        static void Blit(Texture2D image, Texture2D sheet, int x0, int y0)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int sx = x * image.width / Width, sy = y * image.height / Height;
                    sheet.SetPixel(x0 + x, y0 + y, image.GetPixel(sx, sy));
                }
        }

        static Texture2D Render(Outfit outfit, int seat, View view, AnimationClip idle, Action<Shot> describe)
        {
            var preview = new PreviewRenderUtility();
            GameObject holder = null;
            try
            {
                holder = new GameObject("Look sheet " + view);
                preview.AddSingleGO(holder);
                var model = ModelVisual.Spawn("Avatar/Avatar", holder.transform);
                if (!model) throw new InvalidOperationException("Avatar spawn failed.");
                idle.SampleAnimation(model, 0f);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var worn = PlayerAppearance.Dress(skins, outfit, GameConfig.Current.Wardrobe, seat);
                holder.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("Nothing visible to capture.");
                var rig = model.GetComponentInChildren<WardrobeRig>();
                describe?.Invoke(new Shot
                {
                    outfit = worn.Serialize(), letter = LetterBadge.For(worn, seat),
                    visible = renderers.Select(r => r.name).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    rigid = rig ? rig.Attached.Where(id => rig.NodeFor(id) && rig.NodeFor(id).gameObject.activeInHierarchy).OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0],
                });
                var camera = preview.camera;
                camera.orthographic = true;
                camera.transform.rotation = Quaternion.Euler(view == View.Couch || view == View.Small ? CouchPitch : 0f, 0f, 0f);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var focus = bounds;
                if (view == View.Chest)
                {
                    var chest = ModelVisual.FindNamed(model, "chest");
                    focus = new Bounds(chest ? chest.position + holder.transform.forward * .1f - Vector3.up * .06f : bounds.center, Vector3.one * .26f);
                }
                Frame(camera, focus, (float)Width / Height);
                camera.nearClipPlane = .01f; camera.farClipPlane = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = view == View.Small ? new Color(.78f, .64f, .45f, 1f) : new Color(.97f, .94f, .85f, 1f);
                camera.allowHDR = false;
                preview.lights[0].color = preview.lights[1].color = Color.white;
                preview.lights[0].intensity = .8f; preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(25f, -25f, 0f);
                preview.lights[1].intensity = .4f; preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(10f, 35f, 0f);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                bool small = view == View.Small;
                preview.BeginStaticPreview(new Rect(0, 0, small ? SmallWidth : Width, small ? SmallHeight : Height));
                preview.Render(true);
                return preview.EndStaticPreview();
            }
            finally
            {
                if (holder) Object.DestroyImmediate(holder);
                preview.Cleanup();
            }
        }

        static void Frame(Camera camera, Bounds bounds, float aspect)
        {
            var right = camera.transform.right;
            var up = camera.transform.up;
            float halfWidth = 0f, halfHeight = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var offset = corner - bounds.center;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
            }
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect) * 1.1f;
            camera.transform.position = bounds.center - camera.transform.forward * 5f;
        }
    }
}
