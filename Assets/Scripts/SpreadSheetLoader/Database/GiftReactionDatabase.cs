using System.Collections.Generic;
using UnityEngine;

public class GiftReactionDatabase
{
    // npcId -> taste -> lines
    private readonly Dictionary<int, Dictionary<GiftTaste, List<string>>> map = new();

    public int Count { get; private set; }

    public void Add(GiftReactionDef def)
    {
        if (def == null) return;
        if (def.npcId <= 0) return;
        if (string.IsNullOrWhiteSpace(def.line)) return;

        if (!map.TryGetValue(def.npcId, out var tasteMap) || tasteMap == null)
        {
            tasteMap = new Dictionary<GiftTaste, List<string>>();
            map[def.npcId] = tasteMap;
        }

        if (!tasteMap.TryGetValue(def.taste, out var list) || list == null)
        {
            list = new List<string>();
            tasteMap[def.taste] = list;
        }

        list.Add(def.line);
        Count++;
    }

    public bool TryGetRandomLine(int npcId, GiftTaste taste, out string line)
    {
        line = null;

        if (npcId <= 0) return false;

        if (!map.TryGetValue(npcId, out var tasteMap) || tasteMap == null)
            return false;

        // 1) exact taste
        if (tasteMap.TryGetValue(taste, out var list) && list != null && list.Count > 0)
        {
            line = list[Random.Range(0, list.Count)];
            return true;
        }

        // 2) fallback taste: Normal
        if (taste != GiftTaste.Soso &&
            tasteMap.TryGetValue(GiftTaste.Soso, out var list2) && list2 != null && list2.Count > 0)
        {
            line = list2[Random.Range(0, list2.Count)];
            return true;
        }

        return false;
    }
}
