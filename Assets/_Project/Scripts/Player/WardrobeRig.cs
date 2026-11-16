using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class WardrobeRig : MonoBehaviour
    {
        public const string ArtPath = "Wardrobe/SignaturePieces";
        public const string GlyphPrefix = "Glyph_";
        static readonly string[] Anchors = { "pelvis", "chest", "head" };
        static GameObject art;
        static bool artLoaded;

        sealed class Part
        {
            public WardrobePiece Piece;
            public Transform Node;
            public Renderer[] Renderers;
            public Vector3 Rest, Forward;
            public readonly Dictionary<string, GameObject> Glyphs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        }

        readonly Dictionary<string, Part> parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        bool built, broken;

        public static GameObject Art
        {
            get
            {
                if (!artLoaded) { art = Resources.Load<GameObject>(ArtPath); artLoaded = true; }
                return art;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { art = null; artLoaded = false; }

        public IEnumerable<string> Attached => parts.Keys;
        public bool Has(string rigid) => !string.IsNullOrEmpty(rigid) && parts.ContainsKey(rigid);
        public Transform NodeFor(string rigid) => parts.TryGetValue(rigid ?? "", out var part) ? part.Node : null;

        public static WardrobeRig For(SkinnedMeshRenderer[] meshes, WardrobeCatalogue catalogue, bool create = true)
        {
            if (meshes == null || catalogue == null) return null;
            string frameMesh = null;
            foreach (var piece in catalogue.Pieces)
                if (piece.Letter && !string.IsNullOrEmpty(piece.Mesh)) { frameMesh = piece.Mesh; break; }
            SkinnedMeshRenderer frame = null;
            foreach (var mesh in meshes)
                if (mesh && mesh.sharedMesh && mesh.name == frameMesh) { frame = mesh; break; }
            if (!frame || !frame.transform.parent || !frame.gameObject.scene.IsValid()) return null;
            var owner = frame.transform.parent.gameObject;
            var rig = owner.GetComponent<WardrobeRig>();
            if (!create) return rig && rig.built ? rig : null;
            if (!Art) return null;
            if (!rig) rig = owner.AddComponent<WardrobeRig>();
            return rig.Build(meshes, frame, catalogue) ? rig : null;
        }

        bool Build(SkinnedMeshRenderer[] meshes, SkinnedMeshRenderer frame, WardrobeCatalogue catalogue)
        {
            if (built) return parts.Count > 0;
            if (broken) return false;
            GameObject holder = null;
            try
            {
                var owner = transform;
                var binds = new Dictionary<string, (Transform bone, Matrix4x4 bind)>(StringComparer.Ordinal);
                foreach (var mesh in meshes)
                {
                    if (!mesh || !mesh.sharedMesh) continue;
                    var bones = mesh.bones;
                    var poses = mesh.sharedMesh.bindposes;
                    var toOwner = owner.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                    for (int i = 0; i < bones.Length && i < poses.Length; i++)
                        if (bones[i] && !binds.ContainsKey(bones[i].name)) binds[bones[i].name] = (bones[i], toOwner * poses[i].inverse);
                }
                foreach (string anchor in Anchors)
                    if (!binds.ContainsKey(anchor)) { broken = true; return false; }
                holder = Instantiate(Art, owner, false);
                holder.name = "Wardrobe signature art";
                var artRoot = holder.transform;
                var refs = new Vector3[4];
                string[] refNames = { "Ref_pelvis", "Ref_chest", "Ref_head", "Ref_badge" };
                for (int i = 0; i < refNames.Length; i++)
                {
                    var marker = ModelVisual.FindNamed(holder, refNames[i]);
                    if (!marker) { broken = true; return false; }
                    refs[i] = artRoot.InverseTransformPoint(marker.position);
                }
                var frameToOwner = owner.worldToLocalMatrix * frame.transform.localToWorldMatrix;
                var avatar = new[]
                {
                    binds["pelvis"].bind.MultiplyPoint3x4(Vector3.zero), binds["chest"].bind.MultiplyPoint3x4(Vector3.zero),
                    binds["head"].bind.MultiplyPoint3x4(Vector3.zero), frameToOwner.MultiplyPoint3x4(frame.sharedMesh.bounds.center),
                };
                var solve = Similarity(refs, avatar, out var front, out float scale);
                foreach (var piece in catalogue.Pieces)
                {
                    if (string.IsNullOrEmpty(piece.Rigid) || parts.ContainsKey(piece.Rigid)) continue;
                    var node = ModelVisual.FindNamed(holder, piece.Rigid);
                    if (!node || !binds.TryGetValue(piece.Bone ?? "", out var bone)) continue;
                    var inOwner = solve * (artRoot.worldToLocalMatrix * node.localToWorldMatrix);
                    var toBone = bone.bind.inverse;
                    var local = toBone * inOwner;
                    node.SetParent(bone.bone, false);
                    node.localPosition = local.MultiplyPoint3x4(Vector3.zero);
                    node.localRotation = local.rotation;
                    node.localScale = local.lossyScale;
                    var part = new Part
                    {
                        Piece = piece, Node = node, Renderers = node.GetComponentsInChildren<Renderer>(true),
                        Rest = node.localPosition, Forward = toBone.MultiplyVector(front * scale),
                    };
                    foreach (Transform child in node.GetComponentsInChildren<Transform>(true))
                        if (child.name.StartsWith(GlyphPrefix, StringComparison.Ordinal)) part.Glyphs[child.name.Substring(GlyphPrefix.Length)] = child.gameObject;
                    SetLayer(node, gameObject.layer);
                    UseAvatarMaterials(part.Renderers, meshes);
                    node.gameObject.SetActive(false);
                    parts[piece.Rigid] = part;
                }
                built = true;
                return parts.Count > 0;
            }
            catch (Exception error)
            {
                broken = true;
                Debug.LogWarning("[Wreckabulary] Wardrobe signature art could not be attached: " + error.Message);
                return false;
            }
            finally
            {
                if (holder)
                {
                    holder.SetActive(false);
                    if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder);
                }
            }
        }

        static Matrix4x4 Similarity(Vector3[] from, Vector3[] to, out Vector3 front, out float scale)
        {
            var fromUp = from[2] - from[0];
            var toUp = to[2] - to[0];
            scale = toUp.magnitude / Mathf.Max(1e-6f, fromUp.magnitude);
            var fromFront = Vector3.ProjectOnPlane(from[3] - from[1], fromUp);
            var toFront = Vector3.ProjectOnPlane(to[3] - to[1], toUp);
            if (fromFront.sqrMagnitude < 1e-8f || toFront.sqrMagnitude < 1e-8f) throw new InvalidOperationException("the badge marker sits on the spine line");
            var rotation = Quaternion.LookRotation(toFront, toUp) * Quaternion.Inverse(Quaternion.LookRotation(fromFront, fromUp));
            front = toFront.normalized;
            return Matrix4x4.Translate(to[0]) * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one * scale) * Matrix4x4.Translate(-from[0]);
        }

        static void SetLayer(Transform node, int layer)
        {
            foreach (var child in node.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }

        public static string CanonicalMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int cut = name.IndexOf("__Default_", StringComparison.Ordinal);
            if (cut > 0) name = name.Substring(0, cut);
            return name.EndsWith(" (Instance)", StringComparison.Ordinal) ? name.Substring(0, name.Length - 11) : name;
        }

        static void UseAvatarMaterials(Renderer[] renderers, SkinnedMeshRenderer[] meshes)
        {
            var avatar = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var mesh in meshes)
                if (mesh)
                    foreach (var material in mesh.sharedMaterials)
                        if (material && !avatar.ContainsKey(CanonicalMaterial(material.name))) avatar[CanonicalMaterial(material.name)] = material;
            var library = MaterialLibrary.Load();
            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string name = CanonicalMaterial(shared[i] ? shared[i].name : "");
                    if (avatar.TryGetValue(name, out var own)) shared[i] = own;
                    else if (library && library.Find(name) is Material house) shared[i] = house;
                }
                renderer.sharedMaterials = shared;
            }
        }

        public void Show(Outfit outfit, WardrobeCatalogue catalogue, ICollection<string> shown, string letter)
        {
            foreach (var part in parts.Values)
            {
                if (!part.Node) continue;
                bool on = shown.Contains(part.Piece.Rigid);
                part.Node.gameObject.SetActive(on);
                if (!on) continue;
                part.Node.localPosition = part.Rest + part.Forward * catalogue.LiftFor(outfit, part.Piece.Slot);
                foreach (var glyph in part.Glyphs) glyph.Value.SetActive(glyph.Key == letter);
                var colour = catalogue.ColourFor(outfit, part.Piece.Slot);
                foreach (var renderer in part.Renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var block = new MaterialPropertyBlock();
                        string name = CanonicalMaterial(materials[i] ? materials[i].name : "");
                        bool rib = name == "fabric_rib" && part.Piece.TintMaterials.Contains("fabric_main");
                        if (colour != null && (rib || part.Piece.TintMaterials.Contains(name)))
                        {
                            var tint = new Color(colour.R, colour.G, colour.B, 1f);
                            if (rib) tint *= .75f;
                            tint.a = 1f;
                            block.SetColor("_BaseColor", tint);
                            block.SetColor("_Color", tint);
                        }
                        renderer.SetPropertyBlock(block, i);
                    }
                }
            }
        }
    }
}
