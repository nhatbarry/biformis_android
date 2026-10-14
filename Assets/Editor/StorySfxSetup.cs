using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CaptainPinkTurd.AudioSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class StorySfxSetup
{
    private const string Effects = "Assets/Audio/Story SFX/Effects/";
    private const string CatalogPath = "Assets/Resources/Story SFX.asset";

    [MenuItem("Biformis/Audio/Configure Story SFX")]
    public static void Configure()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog = AssetDatabase.LoadAssetAtPath<StorySfxCatalog>(CatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<StorySfxCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/Main.mixer");
        catalog.mixerGroup = Array.Find(mixer.FindMatchingGroups("SoundFX"), group => group.name == "SoundFX");
        if (!catalog.mixerGroup) throw new InvalidOperationException("The SoundFX mixer group is missing.");
        var cues = new List<StorySfxCatalog.Cue>();
        void Add(string id, string filename, float volume, bool existing = false)
        {
            var path = existing ? filename : Effects + filename + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (!clip) throw new InvalidOperationException($"Missing {id} SFX: {path}");
            cues.Add(new StorySfxCatalog.Cue { id = id, clips = new[] { clip }, volume = volume });
        }
        Add("door_bang", "Door Bang", 0.6f);
        Add("door_knock", "Door Knock", 0.45f);
        Add("door_break", "Wood Break", 0.75f);
        Add("door_slam", "Door Slam", 0.6f);
        Add("lever", "Metal Latch", 0.5f);
        Add("trapdoor", "Trapdoor Open", 0.55f);
        Add("vent", "Metal Latch", 0.35f);
        Add("cage_struggle", "Cage Rattle", 0.38f);
        Add("cloth", "Cloth Rustle", 0.3f);
        Add("stab", "Knife Slice", 0.5f);
        Add("box_pickup", "Box Handle", 0.35f);
        Add("footsteps_soft", "Soft Footstep", 0.33f);
        Add("footsteps_metal", "Hard Footstep", 0.33f);
        Add("thud", "Body Thud", 0.6f);
        Add("appear", "Energy Appear", 0.33f);
        Add("vanish", "Energy Vanish", 0.3f);
        Add("merge", "Energy Merge", 0.4f);
        Add("split", "Energy Merge", 0.4f);
        Add("summon", "Boss Summon", 0.5f);
        Add("portal", "Energy Vanish", 0.3f);
        Add("reveal", "Energy Appear", 0.4f);
        Add("ice_freeze", "Energy Merge", 0.4f);
        Add("ice_crack", "Metal Impact", 0.5f);
        Add("ice_break", "Assets/Ink Dialogue/SFX_break_glass.wav", 0.55f, true);
        Add("glass_break", "Assets/Ink Dialogue/SFX_break_glass.wav", 0.7f, true);
        Add("dust", "Assets/Packages Tools/Feel/NiceVibrations/HapticSamples/SoundFX/Crumbling1.wav", 0.4f, true);
        Add("splash", "Assets/Packages Tools/Feel/NiceVibrations/HapticSamples/Nature/Splash1.wav", 0.4f, true);
        Add("collapse", "Assets/Packages Tools/Feel/NiceVibrations/HapticSamples/SoundFX/Crumbling1.wav", 0.6f, true);
        Add("warning", "Assets/Audio/SFX/warning6.mp3", 0.4f, true);
        Add("remote", "Assets/Audio/SFX/HighClick1.mp3", 0.3f, true);
        Add("fall", "Assets/Audio/SFX/dash_sfx.mp3", 0.4f, true);
        Add("volley", "Boss Volley", 0.25f);
        Add("lightning", "Assets/Audio/SFX/explosion_sfx.mp3", 0.45f, true);
        Add("charge", "Boss Summon", 0.3f);
        Add("slash", "Knife Slice", 0.5f);
        Add("dash", "Assets/Audio/SFX/dash_sfx.mp3", 0.5f, true);
        Add("cleave", "Metal Impact", 0.6f);
        catalog.cues = cues.ToArray();
        EditorUtility.SetDirty(catalog);
        CompileInk();
        AssetDatabase.SaveAssets();
        Debug.Log($"Configured {catalog.cues.Length} story SFX cues and compiled both dialogue languages.");
    }

    private static void CompileInk()
    {
        var outputs = new List<(string path, string json)>();
        foreach (var suffix in new[] { "", "_EN" })
        {
            string source = $"Assets/Ink Dialogue/Biformis_Story{suffix}.ink";
            string target = Path.ChangeExtension(source, ".json");
            var errors = new List<string>();
            bool visits = new Ink.Runtime.Story(File.ReadAllText(target, Encoding.UTF8)).mainContentContainer.visitsShouldBeCounted;
            var compiler = new Ink.Compiler(File.ReadAllText(source, Encoding.UTF8), new Ink.Compiler.Options
            {
                sourceFilename = source,
                countAllVisits = visits,
                errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) errors.Add(message); }
            });
            var story = compiler.Compile();
            if (story == null || errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            outputs.Add((target, story.ToJson()));
        }
        foreach (var output in outputs)
        {
            // Unity can memory-map an imported TextAsset on Windows. Replace the file atomically
            // rather than truncating that mapped file while recompiling the story.
            Directory.CreateDirectory(".utmp");
            string temporary = Path.Combine(".utmp", Path.GetFileName(output.path) + ".story-sfx.tmp");
            File.WriteAllText(temporary, output.json, new UTF8Encoding(false));
            File.Replace(temporary, output.path, null);
            AssetDatabase.ImportAsset(output.path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
