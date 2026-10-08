using System;
using System.Linq;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using BulletHell;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Rebuilds only the boss prefab and Level 6; usable in the editor or through -executeMethod.</summary>
public static class BossFightSetup
{
    private const string Art = "Assets/Sprites/Enemies/Plague Doctor Boss/";
    private const string Prefab = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";
    private const string ScenePath = "Assets/Scenes/CaptainPinkTurd/Story/Level Story 6.unity";

    [MenuItem("Biformis/Configure Boss Phase One")]
    public static void ConfigurePhaseOne()
    {
        AssetDatabase.Refresh();
        var first = ImportLightning(Art + "Lightning Down 1.png");
        var second = ImportLightning(Art + "Lightning Down 2.png");
        var bossObject = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            bossObject.layer = LayerMask.NameToLayer("Enemy");
            ConfigureCombat(bossObject);
            var hazards = bossObject.GetComponent<BossHazards>() ?? bossObject.AddComponent<BossHazards>();
            var hazardSettings = new SerializedObject(hazards);
            SetArray(hazardSettings.FindProperty("lightningOne"), first);
            SetArray(hazardSettings.FindProperty("lightningTwo"), second);
            hazardSettings.ApplyModifiedPropertiesWithoutUndo();
            var bar = bossObject.GetComponent<BossHealthBar>() ?? bossObject.AddComponent<BossHealthBar>();
            var barSettings = new SerializedObject(bar);
            barSettings.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/m3x6 VI SDF.asset");
            barSettings.ApplyModifiedPropertiesWithoutUndo();

            var boss = new SerializedObject(bossObject.GetComponent<PlagueDoctorBoss>());
            boss.FindProperty("idleSeconds").vector2Value = new Vector2(0.65f, 1.1f);
            boss.FindProperty("hoverSeconds").floatValue = 4.5f;
            boss.FindProperty("levitateWeight").floatValue = 0.7f;
            boss.FindProperty("frozenFrame").intValue = 15;
            var paths = new[]
            {
                "Assets/Prefabs/Enemies/Luneblade/Reaper.prefab",
                "Assets/Prefabs/Enemies/Turrets/Turret Burst.prefab",
                "Assets/Prefabs/Enemies/Luneblade/Axion.prefab",
                "Assets/Prefabs/Enemies/Turrets/Turret Slow Circle.prefab",
                "Assets/Prefabs/Enemies/Luneblade/Riven.prefab",
            };
            SetArray(boss.FindProperty("summonPrefabs"), paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
            boss.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(bossObject, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(bossObject); }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var maps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        var ground = maps.First(map => map.name.Trim() == "Ground");
        var floor = ground.GetTile(ground.WorldToCell(new Vector3(8f, 16f)));
        if (!floor) throw new InvalidOperationException("Level 6 has no floor tile at the boss");
        foreach (var map in maps) map.ClearAllTiles();
        // Large open floor with no wall, gate or invisible player-blocking perimeter.
        var bounds = new BoundsInt(-24, -8, 0, 64, 48, 1);
        var tiles = Enumerable.Repeat(floor, bounds.size.x * bounds.size.y).ToArray();
        ground.SetTilesBlock(bounds, tiles);
        ground.CompressBounds();
        var floorWindow = ground.GetComponent<BossArenaFloor>() ?? ground.gameObject.AddComponent<BossArenaFloor>();
        var floorSettings = new SerializedObject(floorWindow);
        floorSettings.FindProperty("floorTile").objectReferenceValue = floor;
        floorSettings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var collider in UnityEngine.Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
            collider.ProcessTilemapChanges();
        foreach (var collider in UnityEngine.Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
            collider.GenerateGeometry();

        // Remove the zero-wave encounter that previously opened the exit without a fight.
        var encounter = GameObject.Find("Encounter Door");
        if (encounter) UnityEngine.Object.DestroyImmediate(encounter);
        var bound = GameObject.Find("Map Bound").GetComponent<BoxCollider2D>();
        bound.offset = new Vector2(8f, 16f);
        bound.size = new Vector2(64f, 48f);
        bound.isTrigger = true;
        bound.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        var camera = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>();
        camera.Target.TrackingTarget = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>().transform;
        camera.Lens.OrthographicSize = 6.5f;
        EditorUtility.SetDirty(camera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        var composer = camera.GetComponent<CinemachinePositionComposer>();
        if (composer)
        {
            // PositionComposer rotates this offset with the target, but does not apply its scale.
            composer.TargetOffset = Vector3.up * 3.2f;
            EditorUtility.SetDirty(composer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(composer);
        }
        var confiner = camera.GetComponent<CinemachineConfiner2D>();
        if (confiner)
        {
            confiner.enabled = false; //no camera perimeter either; movement stays free
            EditorUtility.SetDirty(confiner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(confiner);
        }
        var oldTarget = GameObject.Find("Boss Arena Camera Target");
        if (oldTarget) UnityEngine.Object.DestroyImmediate(oldTarget);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        BossPresentationSetup.Configure();
        Debug.Log("[BossFightSetup] Phase one configured; fixed open arena; only downward lightning 1 and 2 imported.");
    }

    [MenuItem("Biformis/Update Boss Combat")]
    public static void UpdateBossCombat()
    {
        AssetDatabase.Refresh();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            ConfigureCombat(root);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BossFightSetup] Normal R2/B2 enemy bullets; solid feet collider; existing enemy hit feedback configured.");
    }

    private static void ConfigureCombat(GameObject root)
    {
        var hitbox = root.GetComponent<BoxCollider2D>() ?? root.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector2(1.4f, 2.8f);
        hitbox.offset = new Vector2(0f, 1f);
        var feet = root.GetComponent<CircleCollider2D>() ?? root.AddComponent<CircleCollider2D>();
        feet.isTrigger = false;
        feet.radius = 0.55f;
        feet.offset = new Vector2(0f, 0.25f);
        var body = root.GetComponent<Rigidbody2D>() ?? root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        root.GetComponent<SpriteRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Boss Blade.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Shader Graphs/Hit_Effect/HitEffectMAT.mat");
        var oldEnemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Luneblade/Reaper.prefab");
        var reference = new SerializedObject(oldEnemy.GetComponent<LunebladeEnemy>());
        var boss = new SerializedObject(root.GetComponent<PlagueDoctorBoss>());
        foreach (var name in new[] { "knockbackForce", "hitStopDuration", "shakeProfile", "impactShockwavePrefab", "damagedSfx" })
            boss.CopyFromSerializedProperty(reference.FindProperty(name));
        boss.ApplyModifiedPropertiesWithoutUndo();
        var hazards = new SerializedObject(root.GetComponent<BossHazards>());
        hazards.FindProperty("redProjectile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/[ Final ]/Bullet/R2.png");
        hazards.FindProperty("blueProjectile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/[ Final ]/Bullet/B2.png");
        hazards.FindProperty("projectileDiameter").floatValue = oldEnemy.GetComponentInChildren<ProjectileEmitterBiformis>().Scale;
        hazards.FindProperty("projectileMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40"));
        hazards.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] ImportLightning(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, 6).Select(i => new SpriteRect
        {
            name = "Down_" + i, rect = new Rect(i * 64, 0, 64, 64),
            alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0.0625f),
            spriteID = existing.FirstOrDefault(rect => rect.name == "Down_" + i)?.spriteID ?? GUID.Generate(),
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
    }

    private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            if (!values[i]) throw new InvalidOperationException("Missing asset for " + property.name);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
