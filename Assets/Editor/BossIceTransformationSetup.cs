using System.Linq;
using CaptainPinkTurd.Game.Enemy;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>The approved twelve-frame ice-to-knife transformation, with a hold at intact blue ice.</summary>
public static class BossIceTransformationSetup
{
    private const string Art = "Assets/Sprites/Enemies/Plague Doctor Boss/Ice Transformation/boss-bang-vo-bien-hinh-aura";
    private const string Prefab = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";

    [MenuItem("Biformis/Update Boss Ice Transformation")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + ".png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 85.33f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192; //12 full-width canvases: 4968px; preserve source pixels
        var android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = 8192;
        android.format = TextureImporterFormat.RGBA32;
        android.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, 12).Select(i => new SpriteRect
        {
            name = "IceTransformation_" + i,
            rect = new Rect(i * 414, 0, 414, 442),
            alignment = SpriteAlignment.Custom,
            pivot = new Vector2(192f / 414f, 93f / 442f),
            spriteID = previous.FirstOrDefault(rect => rect.name == "IceTransformation_" + i)?.spriteID ?? GUID.Generate(),
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + ".png").OfType<Sprite>()
            .OrderBy(sprite => sprite.rect.x).ToArray();
        var durations = BossPhaseTwoSetup.ReadDurations(Art + ".aseprite");
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var settings = new SerializedObject(root.GetComponent<PlagueDoctorBoss>());
            var frames = settings.FindProperty("iceShatter.frames");
            var timing = settings.FindProperty("iceShatter.frameMilliseconds");
            frames.arraySize = timing.arraySize = 12;
            for (int i = 0; i < 12; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                timing.GetArrayElementAtIndex(i).intValue = durations[i];
            }
            settings.FindProperty("frozenFrame").intValue = 2; //third frame: complete blue ice, before the red crack
            settings.FindProperty("phaseTwoRestingPose").objectReferenceValue = sprites[11];
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BossIceTransformationSetup] 12 source frames, blue-ice hold at frame 3, reveal at frame 7, aura only at frame 8.");
    }
}
