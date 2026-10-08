using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameplayArtDiagnostics
{
    public static void Inspect()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/Story/Villain_PlagueDoctor.aseprite").OfType<Sprite>().ToArray();
        Directory.CreateDirectory("Logs/art-diagnostics");
        var textures = sprites.Select(s => s.texture).Distinct().ToArray();
        for (int i = 0; i < textures.Length; i++)
        {
            var t = textures[i];
            var rt = RenderTexture.GetTemporary(t.width, t.height);
            Graphics.Blit(t, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(t.width,t.height,TextureFormat.RGBA32,false);
            read.ReadPixels(new Rect(0,0,t.width,t.height),0,0);read.Apply();
            File.WriteAllBytes($"Logs/art-diagnostics/villain-atlas-{i}.png",read.EncodeToPNG());
            RenderTexture.active = previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(read);
        }
        foreach(var s in sprites) Debug.Log($"[ART] {s.name} rect={s.rect} pivot={s.pivot} packed={s.packed} bounds={s.bounds}");
    }
}
