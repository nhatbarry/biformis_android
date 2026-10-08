using System;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Game.Enemy;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

public static class BossTopDownPackSetup
{
    public const string Art = "Assets/Sprites/Enemies/Plague Doctor Boss/Phase Two Top Down X2/";
    [Serializable] private class Manifest { public Meta[] animations; }
    [Serializable] private class Meta
    {
        public string animation, direction, action;
        public int frames;
        public int[] frameDurationsMs;
        public float[] pivotNormalizedUnity;
        public float pixelsPerUnitSuggested;
        public bool loopInUnity;
    }
    [Serializable] private class Annotations { public AnnotatedClip[] clips; }
    [Serializable] private class AnnotatedClip { public string animation; public Pose[] poses; }
    [Serializable] private class Pose { public float[] endpoints; public float halfWidth; }

    [MenuItem("Biformis/Configure Boss Top Down X2 Pack")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Art + "manifest.json"));
        var annotations = JsonUtility.FromJson<Annotations>(File.ReadAllText(Art + "blade-annotations.json"));
        const string prefab = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            var directional = root.GetComponent<BossDirectionalArt>() ?? root.AddComponent<BossDirectionalArt>();
            var settings = new SerializedObject(directional);
            var directions = settings.FindProperty("directions");
            directions.arraySize = 4;
            var combat = new SerializedObject(root.GetComponent<BossPhaseTwoCombat>());
            foreach (BossDirectionalArt.EView view in Enum.GetValues(typeof(BossDirectionalArt.EView)))
            {
                var destination = directions.GetArrayElementAtIndex((int)view);
                destination.FindPropertyRelative("view").enumValueIndex = (int)view;
                foreach (var meta in manifest.animations.Where(value => value.direction == view.ToString()))
                {
                    string path = Art + view + "/" + meta.animation + ".aseprite";
                    var importer = (AsepriteImporter)AssetImporter.GetAtPath(path);
                    importer.importMode = FileImportModes.AnimatedSprite;
                    importer.layerImportMode = LayerImportModes.MergeFrame;
                    importer.spritePixelsPerUnit = meta.pixelsPerUnitSuggested;
                    importer.pivotSpace = PivotSpaces.Canvas;
                    importer.pivotAlignment = SpriteAlignment.Custom;
                    importer.customPivotPosition = new Vector2(meta.pivotNormalizedUnity[0], meta.pivotNormalizedUnity[1]);
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.spriteMeshType = SpriteMeshType.FullRect;
                    importer.generatePhysicsShape = false;
                    importer.generateModelPrefab = false;
                    importer.generateAnimationClips = true;
                    foreach (var target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.Android })
                    {
                        var platform = importer.GetImporterPlatformSettings(target);
                        platform.overridden = true;
                        platform.maxTextureSize = 8192;
                        platform.textureCompression = TextureImporterCompression.Uncompressed;
                        platform.format = TextureImporterFormat.RGBA32;
                        importer.SetImporterPlatformSettings(platform);
                    }
                    importer.SaveAndReimport();
                    int[] durations = BossPhaseTwoSetup.ReadDurations(path);
                    if (!durations.SequenceEqual(meta.frameDurationsMs)) throw new InvalidDataException(meta.animation + " timings");
                    var animation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
                    var binding = AnimationUtility.GetObjectReferenceCurveBindings(animation)
                        .Single(value => value.type == typeof(SpriteRenderer) && value.propertyName == "m_Sprite");
                    var keys = AnimationUtility.GetObjectReferenceCurve(animation, binding);
                    var clipSettings = AnimationUtility.GetAnimationClipSettings(animation);
                    clipSettings.loopTime = meta.loopInUnity;
                    AnimationUtility.SetAnimationClipSettings(animation, clipSettings);
                    string field = meta.action switch
                    {
                        "walk" => "walk", "slash" => "horizontalSlash", "cast" => "rangedCharge",
                        "dash" => "dashStab", _ => "verticalSlash",
                    };
                    var poses = annotations.clips.Single(value => value.animation == meta.animation).poses;
                    WriteClip(destination.FindPropertyRelative(field), keys, durations, poses);
                    if (view == BossDirectionalArt.EView.Down && field != "walk")
                        WriteClip(combat.FindProperty(field), keys, durations, poses);
                    Debug.Log($"[BossTopDownPack] {meta.animation}: {durations.Length} frames, PPU {meta.pixelsPerUnitSuggested}");
                }
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            combat.FindProperty("rangedReleaseFrame").intValue = 6;
            combat.ApplyModifiedPropertiesWithoutUndo();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Boss Top Down.mat");
            material.SetFloat("_AlphaCutoff", 0.94f);
            material.SetFloat("_DarkBorderPixels", 8f);
            material.SetFloat("_BladeWarmOnly", 1f);
            EditorUtility.SetDirty(material);
            root.GetComponent<SpriteRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BossTopDownPack] All 20 native animations bound; charged shot releases on source frame 7 (980ms).");
    }
    private static void WriteClip(SerializedProperty clip, ObjectReferenceKeyframe[] keys, int[] durations, Pose[] poses)
    {
        var frames = clip.FindPropertyRelative("frames");
        var timing = clip.FindPropertyRelative("frameMilliseconds");
        var blades = clip.FindPropertyRelative("bladeEndpoints");
        var widths = clip.FindPropertyRelative("bladeWidths");
        frames.arraySize = timing.arraySize = blades.arraySize = widths.arraySize = durations.Length;
        float time = 0f;
        for (int i = 0; i < durations.Length; i++)
        {
            float sample = time + durations[i] / 2000f;
            frames.GetArrayElementAtIndex(i).objectReferenceValue = keys.Last(key => key.time <= sample).value;
            timing.GetArrayElementAtIndex(i).intValue = durations[i];
            float[] p = poses[i].endpoints;
            blades.GetArrayElementAtIndex(i).vector4Value = new Vector4(p[0], p[1], p[2], p[3]);
            widths.GetArrayElementAtIndex(i).floatValue = poses[i].halfWidth;
            time += durations[i] / 1000f;
        }
    }
}
