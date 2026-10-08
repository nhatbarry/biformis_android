using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterSpriteRepair
{
    [MenuItem("Biformis/Repair Character Sprite Packing")]
    public static void Configure()
    {
        // Aseprite's importer can keep rectangles from the old atlas when replacement art packs differently
        // into a texture of the same size. Force the supported settings-change path; retain every sprite ID.
        var paths = AssetDatabase.FindAssets("", new[] { "Assets/Animations", "Assets/Sprites/Enemies/Plague Doctor Boss/Phase Two" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".aseprite"));
        foreach(var path in paths)
        {
            var importer=AssetImporter.GetAtPath(path);
            var settings=new SerializedObject(importer);
            var previous=settings.FindProperty("m_PreviousAsepriteImporterSettings.m_SpritePadding");
            var current=settings.FindProperty("m_AsepriteImporterSettings.m_SpritePadding");
            if(previous == null || current == null) throw new System.InvalidOperationException($"Missing Aseprite packing settings: {path}");
            previous.intValue=current.intValue+1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            settings.Update();
            settings.FindProperty("m_PreviousAsepriteImporterSettings.m_SpritePadding").intValue=current.intValue;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }
        AssetDatabase.SaveAssets();
        GameplayArtDiagnostics.Inspect();
        Debug.Log("[CharacterSpriteRepair] Rebuilt sprite rectangles/UVs with original IDs and authored pivots.");
    }
    public static void RepairAndConfigureBoss()
    {
        Configure();
        BossPhaseTwoSetup.Configure();
    }
}
