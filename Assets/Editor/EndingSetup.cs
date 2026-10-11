using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Story;
using CaptainPinkTurd.Story.Cutscene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the story's ending (the ink knot "Ending"): imports its art, puts its actors on the Story Cutscene stage
/// (EndGlow, EndWorld with A_End and B_End inside it, BossEnd, ABMerged), gives the cutscene the ending music, and
/// hands Level Story 6's BossFightStoryExit what its glass shatter shows. Re-running replaces what it built before.
/// The run's picture comes first: the [Explicit] CutsceneFrameCapture.CaptureEndingRunPictures test, then
/// `python -I Tools/ending_art.py world`.
/// </summary>
public static class EndingSetup
{
    private const string Folder = "Assets/Sprites/Story/Ending";
    private const string Breathe = Folder + "/Boss Defeated/Boss Defeated Breathe.png";
    private const string Dust = Folder + "/Boss Defeated/Boss Defeated Dust.png";
    private const string Glow = Folder + "/Ending Glow.png";
    private const string World = Folder + "/Ending Run World.png";
    private const string RedCharacter = "Assets/Animations/Red Character.aseprite";
    private const string BlueCharacter = "Assets/Animations/Blue Character.aseprite";
    private const string CutsceneScene = "Assets/Scenes/CaptainPinkTurd/Story/Story Cutscene.unity";
    private const string BossScene = "Assets/Scenes/CaptainPinkTurd/Story/Level Story 6.unity";
    private const string EndingMusicGuid = "9c1e3346a86a23c4989678ed9c7db33c"; //the old ending's music (Story Ending scene)
    private const string GlassBreakSfx = "Assets/Ink Dialogue/SFX_break_glass.wav"; //the user's glass break

    //Tools/ending_art.py: black 240 | room 160 | 4 levels x 224 | dark 200; the room's centre is the world's x = 0
    private const int WorldWidth = 240 + 160 + 4 * 224 + 200;
    private const float RoomCentre = 240 + 80;
    //the brothers are anchored on the world's pivot (the room's centre), not on its rect's centre 1712 units away,
    //so their x is measured from the same point as the world's own (the ink's numbers)
    private static readonly Vector2 WorldAnchor = new(RoomCentre / WorldWidth, 0.5f);

    //from the pack's settings.json
    private static readonly float[] BreatheMs = { 240, 280, 360, 280, 300, 340 };
    private static readonly float[] DustMs = { 280, 100, 100, 110, 110, 120, 110, 110, 120, 100, 120, 450 };
    private const float BreathePpu = 156.16f;
    private const float DustPpu = 131.84f;

    private static readonly string[] ActorIds = { "EndGlow", "EndWorld", "A_End", "B_End", "BossEnd", "ABMerged" };

    [MenuItem("Biformis/Ending/Build Ending")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var breathe = ImportSheet(Breathe, "Breathe", 6, 604, 593, new Vector2(267, 480), BreathePpu);
        var dust = ImportSheet(Dust, "Dust", 12, 426, 439, new Vector2(192, 381), DustPpu);
        var glow = ImportPicture(Glow, new Vector2(0.5f, 0.5f));
        var world = File.Exists(World) ? ImportPicture(World, new Vector2(RoomCentre / WorldWidth, 0.5f)) : null;
        if (!world) Debug.LogWarning($"[EndingSetup] {World} is missing: run the capture test, then Tools/ending_art.py world");

        var (redIdle, redIdleSeconds) = Clip(RedCharacter, "S-Idle");
        var (redRun, redRunSeconds) = Clip(RedCharacter, "S-Run");
        var (blueIdle, blueIdleSeconds) = Clip(BlueCharacter, "S-Idle");
        var (blueRun, blueRunSeconds) = Clip(BlueCharacter, "S-Run");
        var music = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(EndingMusicGuid));
        var glassBreak = AssetDatabase.LoadAssetAtPath<AudioClip>(GlassBreakSfx);
        if (!glassBreak) Debug.LogWarning($"[EndingSetup] {GlassBreakSfx} is missing: the glass breaks silently");

        // ------------------------------------------------------------ the cutscene stage
        var scene = EditorSceneManager.OpenScene(CutsceneScene, OpenSceneMode.Single);
        var stage = Object.FindAnyObjectByType<CutsceneStage>(FindObjectsInactive.Include);
        var stageData = new SerializedObject(stage);
        var actorList = stageData.FindProperty("actors");
        var actors = new List<StageActor>();
        for (int i = 0; i < actorList.arraySize; i++)
        {
            var actor = actorList.GetArrayElementAtIndex(i).objectReferenceValue as StageActor;
            if (!actor) continue;
            if (ActorIds.Contains(actor.ActorId)) { Object.DestroyImmediate(actor.gameObject); continue; }
            actors.Add(actor);
        }
        var room = actors.First(actor => actor.ActorId == "Room");
        var parent = room.transform.parent;
        int index = room.transform.GetSiblingIndex() + 1;

        var glowActor = NewActor("EndGlow", null, parent, index++, glow, new Vector2(640f, 360f), new Vector2(0.5f, 0.5f));
        var worldActor = NewActor("EndWorld", null, parent, index++, world,
            new Vector2(WorldWidth * 4f, 360f), new Vector2(RoomCentre / WorldWidth, 0.5f));
        //the run's brothers live inside the world, so a camera that follows B can slide it (#follow)
        var aEnd = NewActor("A_End", "A", worldActor.transform, 0, blueIdle[0], Vector2.zero, new Vector2(0.5f, 0.5f), WorldAnchor);
        Animate(aEnd, 4f, 0f, "idle", ("idle", blueIdle, blueIdleSeconds, true, null), ("run", blueRun, blueRunSeconds, true, null));
        var bEnd = NewActor("B_End", "B", worldActor.transform, 1, redIdle[0], Vector2.zero, new Vector2(0.5f, 0.5f), WorldAnchor);
        Animate(bEnd, 4f, 0f, "idle", ("idle", redIdle, redIdleSeconds, true, null), ("run", redRun, redRunSeconds, true, null));
        var boss = NewActor("BossEnd", "Villain", parent, index++, breathe[0], Vector2.zero, new Vector2(0.5f, 0.5f));
        Animate(boss, GlassShatterUnitsPerPixel, BreathePpu, "breathe",
            ("breathe", breathe, BreatheMs.Select(ms => ms / 1000f).ToArray(), true, null),
            ("dust", dust, DustMs.Select(ms => ms / 1000f).ToArray(), false, null));
        var merged = NewActor("ABMerged", "AB", parent, index++, redIdle[0], Vector2.zero, new Vector2(0.5f, 0.5f));
        //B and A take turns, every 0.14 s (the old ending's flicker), then faster and faster until they come apart
        var split = new List<Sprite>();
        var splitSeconds = new List<float>();
        for (int i = 0; i < 18; i++)
        {
            split.Add(i % 2 == 0 ? redIdle[0] : blueIdle[0]);
            splitSeconds.Add(Mathf.Lerp(0.14f, 0.05f, i / 17f));
        }
        Animate(merged, 4f, 0f, "flicker",
            ("flicker", new[] { redIdle[0], blueIdle[0] }, new[] { 0.14f, 0.14f }, true, null),
            ("split", split.ToArray(), splitSeconds.ToArray(), false, null));

        actors.AddRange(new[] { glowActor, worldActor, aEnd, bEnd, boss, merged });
        actorList.arraySize = actors.Count;
        for (int i = 0; i < actors.Count; i++) actorList.GetArrayElementAtIndex(i).objectReferenceValue = actors[i];

        var musicList = stageData.FindProperty("music");
        musicList.arraySize = 1;
        musicList.GetArrayElementAtIndex(0).FindPropertyRelative("Key").stringValue = "ending";
        musicList.GetArrayElementAtIndex(0).FindPropertyRelative("Value").objectReferenceValue = music;
        stageData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // ------------------------------------------------------------ Level 6's glass shatter
        scene = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);
        var exit = Object.FindAnyObjectByType<BossFightStoryExit>(FindObjectsInactive.Include);
        var exitData = new SerializedObject(exit);
        exitData.FindProperty("glow").objectReferenceValue = glow;
        SetArray(exitData.FindProperty("bossPose"), breathe);
        var seconds = exitData.FindProperty("bossPoseSeconds");
        seconds.arraySize = BreatheMs.Length;
        for (int i = 0; i < BreatheMs.Length; i++) seconds.GetArrayElementAtIndex(i).floatValue = BreatheMs[i] / 1000f;
        exitData.FindProperty("redIdle").objectReferenceValue = redIdle[0];
        exitData.FindProperty("blueIdle").objectReferenceValue = blueIdle[0];
        exitData.FindProperty("breakSfx").objectReferenceValue = glassBreak;
        exitData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Debug.Log($"[EndingSetup] ending built: {actors.Count} cutscene actors, world picture {(world ? "in place" : "MISSING")}.");
    }

    private const float GlassShatterUnitsPerPixel = CaptainPinkTurd.Story.Presentation.GlassShatter.BossUnitsPerPixel;

    // ------------------------------------------------------------ art

    //a sheet of equal cells in one row, each cell's pivot at the same pixel (from the cell's top left)
    private static Sprite[] ImportSheet(string path, string prefix, int frames, int width, int height, Vector2 pivotFromTopLeft, float ppu)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        Pixelated(importer);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects();
        var rects = Enumerable.Range(0, frames).Select(i => new SpriteRect
        {
            name = $"{prefix}_{i}",
            rect = new Rect(i * width, 0, width, height),
            alignment = SpriteAlignment.Custom,
            pivot = new Vector2(pivotFromTopLeft.x / width, 1f - pivotFromTopLeft.y / height),
            spriteID = previous.FirstOrDefault(rect => rect.name == $"{prefix}_{i}")?.spriteID ?? GUID.Generate(),
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
    }

    private static Sprite ImportPicture(string path, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        Pixelated(importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    //like the boss's art: Point, no mipmaps, uncompressed, never scaled down
    private static void Pixelated(TextureImporter importer)
    {
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192;
        var android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = 8192;
        android.format = TextureImporterFormat.RGBA32;
        android.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(android);
    }

    //an Aseprite file's tag as the importer made it: its sprites in order and how long each shows
    private static (Sprite[] frames, float[] seconds) Clip(string asepritePath, string tag)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(asepritePath).OfType<AnimationClip>().First(c => c.name == tag);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b => b.propertyName == "m_Sprite");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        var frames = keys.Select(key => (Sprite)key.value).ToArray();
        var seconds = new float[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            float end = i + 1 < keys.Length ? keys[i + 1].time : clip.length;
            seconds[i] = Mathf.Max(0.01f, end - keys[i].time);
        }
        return (frames, seconds);
    }

    // ------------------------------------------------------------ actors

    private static StageActor NewActor(string id, string speaker, Transform parent, int siblingIndex, Sprite sprite, Vector2 size, Vector2 pivot,
        Vector2? anchor = null)
    {
        var go = new GameObject(id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(siblingIndex);
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        var actor = go.AddComponent<StageActor>();
        var data = new SerializedObject(actor);
        data.FindProperty("actorId").stringValue = id;
        data.FindProperty("speakerId").stringValue = speaker ?? "";
        var tinted = data.FindProperty("tintedGraphics");
        tinted.arraySize = 1;
        tinted.GetArrayElementAtIndex(0).objectReferenceValue = image;
        data.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        return actor;
    }

    private static void Animate(StageActor actor, float unitsPerPixel, float referencePpu, string defaultClip,
        params (string name, Sprite[] frames, float[] seconds, bool loop, string next)[] clips)
    {
        var animation = actor.gameObject.AddComponent<StageActorAnimation>();
        var data = new SerializedObject(animation);
        data.FindProperty("unitsPerPixel").floatValue = unitsPerPixel;
        data.FindProperty("referencePixelsPerUnit").floatValue = referencePpu;
        data.FindProperty("defaultClip").stringValue = defaultClip;
        var list = data.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = list.GetArrayElementAtIndex(i);
            clip.FindPropertyRelative("name").stringValue = clips[i].name;
            SetArray(clip.FindPropertyRelative("frames"), clips[i].frames);
            var seconds = clip.FindPropertyRelative("durations");
            seconds.arraySize = clips[i].seconds.Length;
            for (int f = 0; f < clips[i].seconds.Length; f++) seconds.GetArrayElementAtIndex(f).floatValue = clips[i].seconds[f];
            clip.FindPropertyRelative("loop").boolValue = clips[i].loop;
            clip.FindPropertyRelative("repeat").intValue = 0;
            clip.FindPropertyRelative("next").stringValue = clips[i].next ?? "";
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
