using CaptainPinkTurd.Game.Enemy;
using TMPro;
using UnityEditor;
using UnityEngine;

public static partial class BossPhaseTwoCombatSetup
{
    [MenuItem("Biformis/Configure Boss Phase Two Combat")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        const string materialPath = "Assets/Materials/Boss Blade.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Materials/BossBlade.shader");
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else material.shader = shader;
        const string prefab = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            if (!root.GetComponent<BossPhaseTwoEffects>()) root.AddComponent<BossPhaseTwoEffects>();
            if (!root.GetComponent<BossRoaming>()) root.AddComponent<BossRoaming>();
            if (!root.GetComponent<BossGroundPresentation>()) root.AddComponent<BossGroundPresentation>();
            var effectSettings = new SerializedObject(root.GetComponent<BossPhaseTwoEffects>());
            effectSettings.FindProperty("shotSpeed").floatValue = 1200f;
            effectSettings.ApplyModifiedPropertiesWithoutUndo();
            var directional = root.GetComponent<BossDirectionalArt>();
            bool native = directional && directional.HasAnimations;
            root.GetComponent<SpriteRenderer>().sharedMaterial = native
                ? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Boss Top Down.mat") : material;
            var settings = new SerializedObject(root.GetComponent<BossPhaseTwoCombat>());
            settings.FindProperty("warningFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/m3x6 VI SDF.asset");
            var reference = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Enemies/Luneblade/Reaper.prefab").GetComponent<LunebladeEnemy>());
            settings.CopyFromSerializedProperty(reference.FindProperty("attackSfx"));
            foreach (var (name, poses) in native ? new System.Collections.Generic.Dictionary<string, Vector4[]>() : BladePoses)
            {
                var array = settings.FindProperty(name + ".bladeEndpoints");
                array.arraySize = poses.Length;
                for (int i = 0; i < poses.Length; i++) array.GetArrayElementAtIndex(i).vector4Value = poses[i];
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BossPhaseTwoCombatSetup] Coloured warnings, single high-speed shot, dash afterimages and slower roaming configured.");
    }
}
