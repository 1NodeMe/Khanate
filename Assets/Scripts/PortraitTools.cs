#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Put this file in Assets/Editor/

// 1) Every image placed in Assets/Resources/Portraits/ is imported as a Sprite automatically.
public class PortraitPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/Portraits/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
    }
}

// 2) Tools > Check Portraits: lists NPCs from cards.json that have no portrait yet.
public static class PortraitCheck
{
    [MenuItem("Tools/Check Portraits")]
    static void Check()
    {
        var asset = Resources.Load<TextAsset>("cards");
        if (asset == null) { Debug.LogError("PortraitCheck: Assets/Resources/cards.json not found."); return; }

        var data = JsonUtility.FromJson<GameDataRoot>(asset.text.TrimStart('\uFEFF'));

        var names = new SortedSet<string>();
        foreach (var c in data.cards) names.Add(c.character);

        var missing = new List<string>();
        foreach (var n in names)
            if (Resources.Load<Sprite>("Portraits/" + n) == null) missing.Add(n);

        var missingEndings = new List<string>();
        foreach (var e in data.endings)
            if (Resources.Load<Sprite>("Portraits/" + e.id) == null) missingEndings.Add(e.id);
        foreach (var w in data.wins)
            if (Resources.Load<Sprite>("Portraits/" + w.id) == null) missingEndings.Add(w.id);

        Debug.Log("PortraitCheck: NPCs with portrait " + (names.Count - missing.Count) + "/" + names.Count +
                  (missing.Count > 0 ? ". MISSING: " + string.Join(", ", missing) : ". All NPCs are covered."));
        Debug.Log("PortraitCheck (optional, endings use Death/Win Portrait if missing): " +
                  (missingEndings.Count > 0 ? string.Join(", ", missingEndings) : "all endings have portraits"));
    }
}
#endif
