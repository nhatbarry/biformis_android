using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Story.Cutscene;
using CaptainPinkTurd.Story.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Bakes existing character and phase-one boss frames into Level 6's entrance presentation.
/// Re-running updates the entrance component and its pipe image import, and compiles the story dialogue.
/// </summary>
public static class BossEntranceSetup
{
    private const string ScenePath = "Assets/Scenes/CaptainPinkTurd/Story/Level Story 6.unity";
    private const string BackgroundPath = "Assets/Sprites/Story/Boss Entrance/Pipe Interior.png";
    private const string DialoguePath = "Assets/Prefabs/Story/Story Dialogue System.prefab";
    private const string ShaderPath = "Assets/Materials/BossEntranceReveal.shader";
    private const string BossPath = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";
    private const string RedPath = "Assets/Animations/Red Character.aseprite";
    private const string BluePath = "Assets/Animations/Blue Character.aseprite";

    [MenuItem("Biformis/Boss Entrance/Configure")]
    public static void Configure()
    {
        AssetDatabase.Refresh();

        // Validate all source art before opening or changing the arena scene.
        var dialogue = RequireAsset<GameObject>(DialoguePath);
        var shader = RequireAsset<Shader>(ShaderPath);
        var boss = RequireAsset<GameObject>(BossPath).GetComponent<PlagueDoctorBoss>();
        if (!boss) throw new InvalidOperationException($"{BossPath} has no PlagueDoctorBoss component");

        var redRun = CharacterClip(RedPath, "S-Run", "run");
        var blueRun = CharacterClip(BluePath, "S-Run", "run");
        var redIdle = CharacterClip(RedPath, "S-Idle", "idle");
        var blueIdle = CharacterClip(BluePath, "S-Idle", "idle");
        var bossSource = new SerializedObject(boss);
        var bossIdle = BossClip(bossSource.FindProperty("idle"), "idle", true);
        var bossCast = BossClip(bossSource.FindProperty("cast"), "cast", false);
        var pipe = ImportBackground();
        CompileDialogue();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var entrances = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossEntranceSequence>(true)).ToArray();
        if (entrances.Length > 1)
            throw new InvalidOperationException("Level Story 6 contains more than one BossEntranceSequence");

        var entrance = entrances.FirstOrDefault();
        if (!entrance)
        {
            var root = new GameObject("Boss Entrance Sequence");
            entrance = root.AddComponent<BossEntranceSequence>();
        }

        var settings = new SerializedObject(entrance);
        settings.FindProperty("pipeBackground").objectReferenceValue = pipe;
        settings.FindProperty("dialoguePrefab").objectReferenceValue = dialogue;
        settings.FindProperty("revealShader").objectReferenceValue = shader;
        SetClip(settings.FindProperty("redRun"), redRun);
        SetClip(settings.FindProperty("blueRun"), blueRun);
        SetClip(settings.FindProperty("redIdle"), redIdle);
        SetClip(settings.FindProperty("blueIdle"), blueIdle);
        SetClip(settings.FindProperty("bossIdle"), bossIdle);
        SetClip(settings.FindProperty("bossCast"), bossCast);
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entrance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[BossEntranceSetup] Level 6 entrance configured with native side-run/idle frames, " +
                  "phase-one boss idle/cast, pipe background and shared story dialogue.");
    }

    // The editor's automatic Ink compiler can still be queued when a batch configuration quits.
    // Compile both languages synchronously so BeforeBoss is already present in the referenced JSON assets.
    private static void CompileDialogue()
    {
        var sources = new[]
        {
            "Assets/Ink Dialogue/Biformis_Story.ink",
            "Assets/Ink Dialogue/Biformis_Story_EN.ink"
        };
        var compiled = new List<(string path, string json)>();
        foreach (string sourcePath in sources)
        {
            var errors = new List<string>();
            string compiledPath = Path.ChangeExtension(sourcePath, ".json");
            // Preserve each language's existing visit-count setting when rebuilding its dialogue.
            bool countAllVisits = !File.Exists(compiledPath) ||
                new Ink.Runtime.Story(File.ReadAllText(compiledPath, Encoding.UTF8)).mainContentContainer.visitsShouldBeCounted;
            var compiler = new Ink.Compiler(File.ReadAllText(sourcePath, Encoding.UTF8), new Ink.Compiler.Options
            {
                sourceFilename = sourcePath,
                countAllVisits = countAllVisits,
                errorHandler = (message, type) =>
                {
                    if (type == Ink.ErrorType.Error) errors.Add(message);
                    else if (type == Ink.ErrorType.Warning) Debug.LogWarning(message);
                }
            });
            var story = compiler.Compile();
            if (story == null || errors.Count > 0)
                throw new InvalidOperationException($"Cannot compile {sourcePath}: {string.Join("\n", errors)}");
            string json = story.ToJson();
            // Fail before replacing either JSON if the required entrance knot is absent.
            new Ink.Runtime.Story(json).ChoosePathString("BeforeBoss");
            compiled.Add((compiledPath, json));
        }

        foreach (var output in compiled)
        {
            File.WriteAllText(output.path, output.json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(output.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static T RequireAsset<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new InvalidOperationException($"Missing {typeof(T).Name}: {path}");
        return asset;
    }

    private static Sprite ImportBackground()
    {
        var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        if (!importer) throw new InvalidOperationException($"Missing pipe background: {BackgroundPath}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // The generated source stays intact; import it near the stage's logical pixel resolution.
        importer.maxTextureSize = 256;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
        textureSettings.spritePivot = new Vector2(0.5f, 0.5f);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(textureSettings);

        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.maxTextureSize = 256;
        defaults.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        defaults.crunchedCompression = false;
        importer.SetPlatformTextureSettings(defaults);

        var android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = 256;
        android.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
        android.format = TextureImporterFormat.RGBA32;
        android.textureCompression = TextureImporterCompression.Uncompressed;
        android.crunchedCompression = false;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        return RequireAsset<Sprite>(BackgroundPath);
    }

    // Use the Aseprite importer's AnimationClip rather than assuming sprite-name order matches frame order.
    private static StageActorAnimation.Clip CharacterClip(string path, string tag, string name)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == tag);
        if (!source) throw new InvalidOperationException($"{path} has no animation tag {tag}");
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(source)
            .Where(binding => binding.propertyName == "m_Sprite").ToArray();
        if (bindings.Length != 1)
            throw new InvalidOperationException($"{path} / {tag} must have one merged sprite track");
        var keys = AnimationUtility.GetObjectReferenceCurve(source, bindings[0]);
        if (keys.Length == 0) throw new InvalidOperationException($"{path} / {tag} has no sprite keys");

        var result = new StageActorAnimation.Clip
        {
            name = name,
            frames = keys.Select(key => key.value as Sprite).ToArray(),
            durations = new float[keys.Length],
            loop = true,
            repeat = 0,
            next = ""
        };
        for (int i = 0; i < keys.Length; i++)
        {
            if (!result.frames[i]) throw new InvalidOperationException($"{path} / {tag} frame {i} is missing");
            float end = i + 1 < keys.Length ? keys[i + 1].time : source.length;
            result.durations[i] = Mathf.Max(0.01f, end - keys[i].time);
        }
        return result;
    }

    // Reuse the gameplay prefab's approved sprites, order, pivots and native millisecond timing.
    private static StageActorAnimation.Clip BossClip(SerializedProperty source, string name, bool loop)
    {
        if (source == null) throw new InvalidOperationException($"Boss prefab has no clip {name}");
        var frames = source.FindPropertyRelative("frames");
        var milliseconds = source.FindPropertyRelative("frameMilliseconds");
        if (frames.arraySize == 0 || milliseconds.arraySize != frames.arraySize)
            throw new InvalidOperationException($"Boss clip {name} has inconsistent frame/timing data");

        var result = new StageActorAnimation.Clip
        {
            name = name,
            frames = new Sprite[frames.arraySize],
            durations = new float[frames.arraySize],
            loop = loop,
            repeat = 0,
            next = ""
        };
        for (int i = 0; i < frames.arraySize; i++)
        {
            result.frames[i] = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
            int duration = milliseconds.GetArrayElementAtIndex(i).intValue;
            if (!result.frames[i] || duration <= 0)
                throw new InvalidOperationException($"Boss clip {name} frame {i} is missing or has invalid timing");
            result.durations[i] = duration / 1000f;
        }
        return result;
    }

    private static void SetClip(SerializedProperty destination, StageActorAnimation.Clip source)
    {
        destination.FindPropertyRelative("name").stringValue = source.name;
        var frames = destination.FindPropertyRelative("frames");
        var durations = destination.FindPropertyRelative("durations");
        frames.arraySize = source.frames.Length;
        durations.arraySize = source.durations.Length;
        for (int i = 0; i < source.frames.Length; i++)
        {
            frames.GetArrayElementAtIndex(i).objectReferenceValue = source.frames[i];
            durations.GetArrayElementAtIndex(i).floatValue = source.durations[i];
        }
        destination.FindPropertyRelative("loop").boolValue = source.loop;
        destination.FindPropertyRelative("repeat").intValue = source.repeat;
        destination.FindPropertyRelative("next").stringValue = source.next;
    }
}
