using System;
using System.Collections.Generic;

[Serializable]
public class FishDef
{
    public int fishId;
    public string nameKey;

    public FishRarity rarity;
    public FishArea area;

    public int biomeMask;       //1->봄(마을) /2->여름 /4->가을 /8->겨울

    public FishTimeWindow timeWindow;

    public int difficulty;      //난이도(게이지 감소 량)

    public int baseWeight;      //같은 환경에서 어떤 물고기가 나오냐의 가중치
}

public class FishDatabase
{
    private readonly Dictionary<int, FishDef> byId = new();

    public int count => byId.Count;

    public void Build(IEnumerable<FishDef> defs)
    {
        byId.Clear();

        foreach (var def in defs)
        {
            if (def == null) continue;
            if (def.fishId <= 0) throw new Exception($"Invalid fishId : {def.fishId}");

            //중복이면 최신으로 덮기
            byId[def.fishId] = def;
        }
    }

    public bool TryGet(int fishId, out FishDef def) => byId.TryGetValue(fishId, out def);

    public FishDef Get(int fishId)
    {
        if (!byId.TryGetValue(fishId, out var def))
            throw new KeyNotFoundException($"FishDef not found : {fishId}");

        return def;
    }

    public IEnumerable<FishDef> All => byId.Values;
}
