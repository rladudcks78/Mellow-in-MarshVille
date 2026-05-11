using System;
using System.Collections.Generic;

public class NpcDatabase
{
    private readonly Dictionary<int, NpcDef> byId = new();

    public int Count => byId.Count;

    public void Build(IEnumerable<NpcDef> defs)
    {
        byId.Clear();
        if (defs == null) return;

        foreach (var def in defs)
        {
            if (def == null) continue;
            if (def.npcId <= 0) throw new Exception($"Invalid npcId: {def.npcId}");
            byId[def.npcId] = def; // 중복은 최신 것으로 덮어쓰기
        }
    }

    public bool TryGet(int npcId, out NpcDef def) => byId.TryGetValue(npcId, out def);

    public NpcDef Get(int npcId)
    {
        if (!byId.TryGetValue(npcId, out var def))
            throw new KeyNotFoundException($"NpcDef not found: {npcId}");
        return def;
    }
}
