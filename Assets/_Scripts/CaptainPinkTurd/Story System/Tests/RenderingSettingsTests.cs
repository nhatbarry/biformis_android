#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// The screen shockwave (Shockwave_Screen_Vfx, on the CameraSortingLayer sorting layer) samples
    /// _CameraSortingLayerTexture. URP only captures that texture when the renderer's "texture bound" names an
    /// existing sorting layer by its unique ID - not its index. The Android renderer once had 8 ("Default is the 8th
    /// layer"), which is no layer ID at all: nothing was captured, and on Vulkan/GLES the shockwave smeared stale
    /// frames over the screen whenever an enemy died.
    /// </summary>
    public class RenderingSettingsTests
    {
        private const string ShockwaveLayer = "CameraSortingLayer";

        [Test]
        public void EveryRendererCapturesTheLayersBelowTheShockwave()
        {
            var layers = SortingLayer.layers;
            int shockwave = System.Array.FindIndex(layers, l => l.name == ShockwaveLayer);
            Assert.Greater(shockwave, 0, $"sorting layer {ShockwaveLayer} is missing");
            var below = layers[shockwave - 1];

            foreach (var renderer in RenderersInUse())
            {
                var so = new SerializedObject(renderer);
                if (!so.FindProperty("m_UseCameraSortingLayersTexture").boolValue) continue;

                int bound = so.FindProperty("m_CameraSortingLayersTextureBound").intValue;
                string boundName = layers.Any(l => l.id == bound) ? "layer " + layers.First(l => l.id == bound).name : "no such layer id";
                Assert.AreEqual(below.id, bound,
                    $"{AssetDatabase.GetAssetPath(renderer)}: the camera sorting layer texture bound is {bound} " +
                    $"({boundName}); it must be the id of " +
                    $"'{below.name}' ({below.id}), the layer right below {ShockwaveLayer}");
            }
        }

        //the 2D renderers of the pipeline assets of every quality level (Android uses Medium) and the default one
        private static IEnumerable<Renderer2DData> RenderersInUse()
        {
            var pipelines = new HashSet<RenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline };
            for (int i = 0; i < QualitySettings.names.Length; i++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(i));

            var renderers = new HashSet<Renderer2DData>();
            foreach (var pipeline in pipelines.OfType<UniversalRenderPipelineAsset>())
            {
                var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is Renderer2DData data) renderers.Add(data);
                }
            }
            Assert.IsNotEmpty(renderers, "no 2D renderer found in the pipeline assets");
            return renderers;
        }
    }
}
#endif
