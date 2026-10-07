using System;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Game.Enemy;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

/// <summary>Imports the approved Aseprite actions without rebuilding the arena or phase-one settings.</summary>
public static class BossPhaseTwoSetup
{
    private const string Art = "Assets/Sprites/Enemies/Plague Doctor Boss/Phase Two/";
    private const string Prefab = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";

    [MenuItem("Biformis/Configure Boss Phase Two Assets")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        var actions = new[]
        {
            ("horizontalSlash", "01_VungDaoNgang", 144.21f, new Vector2(226f / 595f, 69f / 496f)),
            ("rangedCharge", "02_TuLucTayTrong", 148.05f, new Vector2(216f / 481f, 65f / 474f)),
            ("dashStab", "03_DamLao", 124.16f, new Vector2(251f / 529f, 82f / 497f)),
            ("verticalSlash", "04_ChemDocTayTrong", 119.04f, new Vector2(218f / 499f, 88f / 514f)),
        };
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var placeholder = root.GetComponent<BossPhaseTwoCombat>() ?? root.AddComponent<BossPhaseTwoCombat>();
            var settings = new SerializedObject(placeholder);
            foreach (var (property, name, ppu, pivot) in actions)
            {
                string path = Art + name + ".aseprite";
                var importer = AssetImporter.GetAtPath(path) as AsepriteImporter;
                if (!importer) throw new InvalidOperationException("Missing Aseprite: " + path);
                importer.importMode = FileImportModes.AnimatedSprite;
                importer.layerImportMode = LayerImportModes.MergeFrame;
                importer.spritePixelsPerUnit = ppu;
                importer.pivotSpace = PivotSpaces.Canvas;
                importer.pivotAlignment = SpriteAlignment.Custom;
                importer.customPivotPosition = pivot;
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
                    platform.maxTextureSize = 4096;
                    platform.textureCompression = TextureImporterCompression.Uncompressed;
                    platform.format = TextureImporterFormat.RGBA32;
                    importer.SetImporterPlatformSettings(platform);
                }
                importer.SaveAndReimport();
                int[] durations = ReadDurations(path);
                var animation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(animation)
                    .Single(value => value.type == typeof(SpriteRenderer) && value.propertyName == "m_Sprite");
                var keys = AnimationUtility.GetObjectReferenceCurve(animation, binding);
                var frames = settings.FindProperty(property + ".frames");
                var timing = settings.FindProperty(property + ".frameMilliseconds");
                frames.arraySize = timing.arraySize = durations.Length;
                float time = 0f;
                for (int frame = 0; frame < durations.Length; frame++)
                {
                    // Sample inside each frame: generated clips may omit duplicate/linked cel keys.
                    float sample = time + durations[frame] / 2000f;
                    var sprite = keys.Last(key => key.time <= sample).value as Sprite;
                    if (!sprite) throw new InvalidOperationException($"{name}: missing frame {frame}");
                    frames.GetArrayElementAtIndex(frame).objectReferenceValue = sprite;
                    timing.GetArrayElementAtIndex(frame).intValue = durations[frame];
                    time += durations[frame] / 1000f;
                }
                // The source tag is a one-shot action. Runtime also returns explicitly to the red-haired pose.
                var clipSettings = AnimationUtility.GetAnimationClipSettings(animation);
                clipSettings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(animation, clipSettings);
                Debug.Log($"[BossPhaseTwoSetup] {name}: {durations.Length} frames, {time:F2}s, PPU {ppu}, canvas pivot {pivot}");
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        BossPhaseTwoCombatSetup.Configure();
        Debug.Log("[BossPhaseTwoSetup] Four approved actions and phase-two combat configured.");
    }

    internal static int[] ReadDurations(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.ReadUInt32();
        if (reader.ReadUInt16() != 0xA5E0) throw new InvalidDataException(path);
        int count = reader.ReadUInt16();
        var durations = new int[count];
        long position = 128;
        for (int frame = 0; frame < count; frame++)
        {
            reader.BaseStream.Position = position;
            uint size = reader.ReadUInt32();
            if (reader.ReadUInt16() != 0xF1FA) throw new InvalidDataException(path);
            reader.ReadUInt16();
            durations[frame] = reader.ReadUInt16();
            position += size;
        }
        return durations;
    }
}
