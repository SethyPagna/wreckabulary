using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public class BackdropLookTests
    {
        static readonly string[] ContractSlots =
        {
            "bk_plaster", "bk_brick", "bk_stone", "bk_roof", "bk_wood", "bk_paint", "bk_metal", "bk_glass_dark",
            "bk_glass_lit", "bk_leaf", "bk_bark", "bk_grass", "bk_road", "bk_line", "bk_card"
        };

        static readonly string[] Passes = { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" };

        static readonly Dictionary<string, string[]> Variants = new()
        {
            ["base"] = new string[0],
            ["pc forward+"] = new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT", "_SCREEN_SPACE_OCCLUSION", "FOG_LINEAR", "EVALUATE_SH_MIXED", "DOTS_INSTANCING_ON", "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT" },
            ["mobile forward"] = new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "FOG_LINEAR", "EVALUATE_SH_VERTEX", "INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
        };

        static Shader BackdropShaderAsset()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BackdropLook.ShaderPath);
            Assert.IsNotNull(shader, BackdropLook.ShaderPath);
            Assert.AreEqual(BackdropShader.ShaderName, shader.name);
            return shader;
        }

        [Test]
        public void EverySlotHasAMaterialOnTheBackdropShader()
        {
            var shader = BackdropShaderAsset();
            CollectionAssert.AreEquivalent(ContractSlots, BackdropLook.Slots.Select(slot => slot.Id));
            foreach (string slot in ContractSlots)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{BackdropLook.MaterialFolder}/{slot}.mat");
                Assert.IsNotNull(material, slot + " material; run BackdropLook.All");
                Assert.AreSame(shader, material.shader, slot);
                foreach (string property in new[] { "_BaseColor", "_EmissionColor", "_Smoothness" })
                    Assert.IsTrue(material.HasProperty(property), slot + property);
            }
        }

        [Test]
        public void SlotColoursKeepTheClassicHueAtSeventyPercentSaturation()
        {
            var classic = ImportedArtSettings.ReadMaterials().Where(spec => spec.Skin == "Classic").GroupBy(spec => spec.Family).ToDictionary(g => g.Key, g => g.First());
            foreach (var slot in BackdropLook.Slots)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{BackdropLook.MaterialFolder}/{slot.Id}.mat");
                Assert.IsNotNull(material, slot.Id);
                Color.RGBToHSV(classic[slot.Family].LinearBaseColor.gamma, out float hue, out float saturation, out _);
                Color.RGBToHSV(material.GetColor("_BaseColor"), out float actualHue, out float actualSaturation, out float actualValue);
                Assert.AreEqual(saturation * BackdropLook.SaturationScale, actualSaturation, .02f, slot.Id + " saturation");
                Assert.AreEqual(slot.Value, actualValue, .01f, slot.Id + " value");
                if (saturation > .05f) Assert.AreEqual(0f, Mathf.DeltaAngle(hue * 360f, actualHue * 360f), 2f, slot.Id + " hue");
                Assert.LessOrEqual(material.GetFloat("_Smoothness"), .75f, slot.Id + " stays matte enough to sit behind play");
                bool glows = material.GetColor("_EmissionColor").maxColorComponent > .01f;
                Assert.AreEqual(slot.Id == "bk_glass_lit", glows, slot.Id + " emission");
            }
            Assert.AreEqual(Color.white, AssetDatabase.LoadAssetAtPath<Material>($"{BackdropLook.MaterialFolder}/bk_card.mat").GetColor("_BaseColor"), "Cards take their colour from vertex colour");
        }

        [Test]
        public void ShaderCompilesEveryPassForPcAndMobileWithoutErrors()
        {
            var shader = BackdropShaderAsset();
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            Assert.IsTrue(shader.isSupported);
            var data = ShaderUtil.GetShaderData(shader);
            Assert.AreEqual(1, data.SerializedSubshaderCount, "The backdrop shader declares one SubShader");
            Assert.GreaterOrEqual(data.SubshaderCount, 1);
            string[] PassNames(ShaderData.Subshader sub) => Enumerable.Range(0, sub.PassCount).Select(i => sub.GetPass(i).Name).ToArray();
            var subshader = data.GetSubshader(0);
            CollectionAssert.AreEquivalent(Passes, PassNames(subshader), "The first runtime subshader is the backdrop's own");
            for (int s = 1; s < data.SubshaderCount; s++)
                CollectionAssert.DoesNotContain(PassNames(data.GetSubshader(s)), "ForwardLit", $"Runtime subshader {s} comes from the FallbackError chain");
            var failures = new List<string>();
            for (int i = 0; i < subshader.PassCount; i++)
            {
                var pass = subshader.GetPass(i);
                foreach (var variant in Variants)
                foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    if (!pass.HasShaderStage(stage)) { failures.Add($"{pass.Name} has no {stage} stage"); continue; }
                    var result = pass.CompileVariant(stage, variant.Value, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    var errors = result.Messages.Where(m => m.severity == ShaderCompilerMessageSeverity.Error).Select(m => $"{m.message} ({m.file}:{m.line})").ToArray();
                    if (!result.Success || errors.Length > 0) failures.Add($"{pass.Name} {stage} [{variant.Key}]: {string.Join("; ", errors)}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void MapleMorningIsAReadableLateMorning()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnvironmentLightingProfile>(BackdropLook.ProfilePath);
            var baseline = AssetDatabase.LoadAssetAtPath<EnvironmentLightingProfile>(BackdropLook.BaselineProfilePath);
            Assert.IsNotNull(profile, BackdropLook.ProfilePath + "; run BackdropLook.All");
            Assert.AreSame(profile, Resources.Load<EnvironmentLightingProfile>("Environment/MapleMorning"), "Loads by light id");
            Assert.That(profile.SunEuler.x, Is.InRange(35f, 70f), "Sun at least 35 degrees up");
            Assert.Greater(profile.FogEnd, profile.FogStart);
            Assert.AreEqual(60f, profile.FogStart, 5f);
            Assert.AreEqual(280f, profile.FogEnd, 20f);
            Assert.AreEqual(baseline.Exposure, profile.Exposure, .1f, "Exposure close to today's");
            Assert.AreEqual(baseline.Contrast, profile.Contrast, 3f, "Contrast close to today's");
            Assert.AreEqual(baseline.Saturation, profile.Saturation, 5f, "Saturation close to today's");
            Assert.AreEqual(baseline.SunIntensity, profile.SunIntensity, .15f, "Sun strength close to today's");
            Assert.GreaterOrEqual(profile.SunColor.r, profile.SunColor.b, "Warm sun");
            Assert.Greater(profile.HorizonColor.b, profile.HorizonColor.r, "Soft blue horizon");
            Assert.IsNotNull(profile.Skybox);
            Assert.AreEqual(BackdropLook.SkyPath, AssetDatabase.GetAssetPath(profile.Skybox));
        }

        [Test]
        public void AmbienceClipsImportAsMonoVorbisWithStreamedLoops()
        {
            string folder = BackdropLook.AmbienceFolder + "/maple_close";
            var paths = Directory.GetFiles(folder, "*.wav").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
            Assert.AreEqual(1, paths.Count(path => BackdropAmbience.IsLoop(Path.GetFileNameWithoutExtension(path))), "One garden loop");
            Assert.GreaterOrEqual(paths.Length, 5, "The loop plus sparse one-shots");
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(importer, path);
                Assert.IsNotNull(clip, path);
                var settings = importer.defaultSampleSettings;
                bool loop = BackdropAmbience.IsLoop(clip.name);
                Assert.IsTrue(importer.forceToMono, path + " mono");
                Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, path);
                Assert.AreEqual(loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad, settings.loadType, path);
                Assert.AreEqual(1, clip.channels, path);
                Assert.AreEqual(44100, clip.frequency, path);
                if (loop) Assert.AreEqual(60f, clip.length, .05f, path + " loop length");
                else Assert.Less(clip.length, 12f, path + " one-shot length");
            }
        }
    }
}
