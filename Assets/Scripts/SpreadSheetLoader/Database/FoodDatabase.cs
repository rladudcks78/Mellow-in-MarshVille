using System.Collections.Generic;
using UnityEngine;

public class FoodDatabase
{
    private readonly Dictionary<int, FoodDef> byId = new Dictionary<int, FoodDef>();

    public void Build(IEnumerable<FoodDef> defs)
    {
        byId.Clear();
        foreach (var def in defs)
        {
            if (def != null && def.itemId > 0)
                byId[def.itemId] = def;
        }
        Debug.Log($"[FoodDatabase] {byId.Count}개의 데이터 로드 완료.");
    }

    public bool TryGet(int itemId, out FoodDef def) => byId.TryGetValue(itemId, out def);
}