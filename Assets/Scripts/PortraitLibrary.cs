using System.Collections.Generic;
using UnityEngine;

// Add this component to the GameManager object (or any object), then drag it into GameManager > Portraits.
// Right-click the component header (or the three-dots menu) > "Fill NPC names from cards.json"
// creates one row per NPC. Then drag a sprite into each row.
public class PortraitLibrary : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string name;       // NPC name from cards.json (or an ending id) - filled automatically
        public Sprite portrait;   // drag the portrait sprite here
    }

    [Tooltip("Name of the JSON file in Assets/Resources (without extension)")]
    public string dataFileName = "cards";

    public List<Entry> entries = new List<Entry>();

    Dictionary<string, Sprite> map;

    public Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (map == null) Build();

        Sprite s;
        return map.TryGetValue(key.Trim(), out s) ? s : null;
    }

    void Build()
    {
        map = new Dictionary<string, Sprite>();
        foreach (var e in entries)
        {
            if (e == null || e.portrait == null || string.IsNullOrEmpty(e.name)) continue;
            map[e.name.Trim()] = e.portrait;
        }
    }

    void OnValidate()
    {
        map = null;   // rebuild after any change in the Inspector
    }

    // Safe to run again after cards.json changes: sprites you already assigned are kept.
    [ContextMenu("Fill NPC names from cards.json")]
    void FillNames()
    {
        var asset = Resources.Load<TextAsset>(dataFileName);
        if (asset == null)
        {
            Debug.LogError("PortraitLibrary: Assets/Resources/" + dataFileName + ".json not found.");
            return;
        }

        var data = JsonUtility.FromJson<GameDataRoot>(asset.text.TrimStart('\uFEFF'));
        if (data == null)
        {
            Debug.LogError("PortraitLibrary: could not read " + dataFileName + ".json");
            return;
        }

        // NPCs (alphabetical), then death endings, then win endings
        var npcs = new List<string>();
        foreach (var c in data.cards)
            if (!string.IsNullOrEmpty(c.character) && !npcs.Contains(c.character)) npcs.Add(c.character);
        npcs.Sort(System.StringComparer.Ordinal);

        var keys = new List<string>(npcs);
        foreach (var e in data.endings) if (!keys.Contains(e.id)) keys.Add(e.id);
        foreach (var w in data.wins) if (!keys.Contains(w.id)) keys.Add(w.id);

        // remember what is already assigned
        var old = new Dictionary<string, Sprite>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.name)) old[e.name] = e.portrait;

        var result = new List<Entry>();
        foreach (var k in keys)
        {
            Sprite s;
            old.TryGetValue(k, out s);
            result.Add(new Entry { name = k, portrait = s });
        }

        // keep rows that were added by hand and are not in cards.json
        foreach (var kv in old)
            if (!keys.Contains(kv.Key)) result.Add(new Entry { name = kv.Key, portrait = kv.Value });

        entries = result;
        map = null;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("PortraitLibrary: " + npcs.Count + " NPCs + " + (keys.Count - npcs.Count) +
                  " endings listed. Drag a sprite into each row, then save the scene (Ctrl+S).");
    }
}
