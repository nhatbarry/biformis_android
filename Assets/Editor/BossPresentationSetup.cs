using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.Scene.Manager;
using CaptainPinkTurd.Story;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Updates the boss reveal, fixed arena and portrait health; leaves the approved action art intact.</summary>
public static class BossPresentationSetup
{
    [MenuItem("Biformis/Update Boss Arena Presentation")]
    public static void Configure()
    {
        BossIceTransformationSetup.Configure();

        UpdateHealth();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CaptainPinkTurd/Story/Level Story 6.unity", OpenSceneMode.Single);
        foreach (var door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None)) Object.DestroyImmediate(door.gameObject);
        foreach (var floor in Object.FindObjectsByType<BossArenaFloor>(FindObjectsSortMode.None)) Object.DestroyImmediate(floor);
        var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
        var camera = Camera.main;
        Vector2 cameraCentre = (Vector2)boss.transform.position + Vector2.up * 0.6f;
        camera.transform.position = new Vector3(cameraCentre.x, cameraCentre.y, -10f);
        camera.orthographicSize = 6.5f;
        if (camera.TryGetComponent<CinemachineBrain>(out var brain)) brain.enabled = false;
        var virtualCamera = Object.FindAnyObjectByType<CinemachineCamera>();
        if (virtualCamera)
        {
            virtualCamera.Target.TrackingTarget = null;
            virtualCamera.transform.position = camera.transform.position;
            if (virtualCamera.TryGetComponent<CinemachinePositionComposer>(out var composer)) composer.enabled = false;
            EditorUtility.SetDirty(virtualCamera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(virtualCamera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(virtualCamera.transform);
        }
        var arenaRoot = GameObject.Find("Boss Arena") ?? new GameObject("Boss Arena");
        var arena = arenaRoot.GetComponent<BossArenaController>() ?? arenaRoot.AddComponent<BossArenaController>();
        var arenaSettings = new SerializedObject(arena);
        arenaSettings.FindProperty("arenaCamera").objectReferenceValue = camera;
        arenaSettings.FindProperty("boss").objectReferenceValue = boss;
        arenaSettings.FindProperty("cameraCentre").vector2Value = cameraCentre;
        arenaSettings.FindProperty("playAreaCentre").vector2Value = (Vector2)boss.transform.position + Vector2.down * 0.5f;
        arenaSettings.ApplyModifiedPropertiesWithoutUndo();
        var exit = arenaRoot.GetComponent<BossFightStoryExit>() ?? arenaRoot.AddComponent<BossFightStoryExit>();
        var exitSettings = new SerializedObject(exit);
        exitSettings.FindProperty("boss").objectReferenceValue = boss;
        exitSettings.FindProperty("level").objectReferenceValue = Object.FindAnyObjectByType<LevelManager>();
        exitSettings.ApplyModifiedPropertiesWithoutUndo();
        var bound = GameObject.Find("Map Bound").GetComponent<BoxCollider2D>();
        bound.offset = cameraCentre;
        bound.size = new Vector2(24f, 14f);
        bound.isTrigger = true;
        EditorUtility.SetDirty(camera);
        if (brain) EditorUtility.SetDirty(brain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossPresentationSetup] Fixed open arena; no door; mask reveal and zoom; 10 HP with portrait health.");
    }

    private static void UpdateHealth()
    {
        const string playerPath = "Assets/Prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var health = new SerializedObject(root.GetComponent<CaptainPinkTurd.UnitSystem.UnitHealth>());
            health.FindProperty("maxHealth").intValue = 10;
            health.ApplyModifiedPropertiesWithoutUndo();
            var previous = root.GetComponentInChildren<PlayerHealthUI>(true);
            if (previous)
            {
                var group = new SerializedObject(previous).FindProperty("hpGroup").objectReferenceValue as Component;
                if (group) group.gameObject.SetActive(false);
                previous.enabled = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string uiPath = "Assets/Prefabs/UI/UI Canvas.prefab";
        root = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            var avatar = root.GetComponentInChildren<PlayerAvatar>(true);
            var bar = avatar.GetComponent<PlayerAvatarHealthBar>() ?? avatar.gameObject.AddComponent<PlayerAvatarHealthBar>();
            var settings = new SerializedObject(bar);
            settings.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/m3x6 VI SDF.asset");
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

}
