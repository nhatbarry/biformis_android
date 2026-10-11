using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Restores the Red Final cutscene's pixel-art import settings. The runtime reads the committed
/// Resources textures and creates full-canvas sprites, so these textures must not be sliced or resized.
/// </summary>
public static class RedFinalCutsceneImport
{
    private const string Folder = "Assets/Resources/Story Ending/Red Final";
    private const int MaxTextureSize = 1024; // backgrounds: 320x180; largest atlas: 384x288
    private static readonly string[] Pictures =
    {
        "backgrounds/wide.png",
        "backgrounds/close.png",
        "characters/red/world.png",
        "characters/red/look_talk.png",
        "characters/blue/world.png",
        "characters/blue/clap_talk.png",
        "characters/mom/world.png",
        "characters/mom/clap_talk.png",
        "characters/doctor/world.png",
        "characters/doctor/clap_talk.png",
        "characters/teen/world.png",
        "characters/teen/clap_talk.png",
        "characters/pink/world.png",
        "characters/pink/bob.png",
        "characters/green/world.png",
        "characters/green/clap_talk.png",
        "characters/bunny/world.png",
        "characters/bunny/dance.png",
        "characters/reaper/world.png",
        "characters/reaper/clap_talk.png",
    };

    [MenuItem("Biformis/Ending/Import Red Final Cutscene Assets")]
    public static void Import()
    {
        // Validate the complete set before changing any importer.
        foreach (var picture in Pictures)
            if (!File.Exists(Folder + "/" + picture))
                throw new FileNotFoundException("Red Final cutscene texture is missing", Folder + "/" + picture);
        var timingPath = Folder + "/timing_positions.json";
        if (!File.Exists(timingPath))
            throw new FileNotFoundException("Red Final cutscene timing is missing", timingPath);

        AssetDatabase.Refresh();
        foreach (var picture in Pictures)
        {
            var path = Folder + "/" + picture;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = MaxTextureSize;

            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.maxTextureSize = MaxTextureSize;
            defaults.format = TextureImporterFormat.RGBA32;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            defaults.crunchedCompression = false;
            importer.SetPlatformTextureSettings(defaults);

            var android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = MaxTextureSize;
            android.format = TextureImporterFormat.RGBA32;
            android.textureCompression = TextureImporterCompression.Uncompressed;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(timingPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RedFinalCutsceneImport] {Pictures.Length} textures and the timing data are ready.");
    }
}
