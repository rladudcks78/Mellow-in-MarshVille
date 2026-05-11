using System.Collections.Generic;
using System;

public class ItemDatabase
{
    private readonly Dictionary<int, ItemDef> byId = new();

    public int count => byId.Count;

    public void Build(IEnumerable<ItemDef> defs)
    {
        byId.Clear();

        foreach(var def in defs)
        {
            if(def == null) continue;

            //나중에 아이템 추가할 때 아이템아이디 안적는(0으로 된 상태) 경우가 많으니 방어
            if(def.itemId <= 0) throw new Exception($"Invalid itemId : {def.itemId}");

            //중복 id는 최신 것으로 덮어씌움
            byId[def.itemId] = def;
        }
    }

    public bool TryGet(int itemId, out ItemDef def) => byId.TryGetValue(itemId, out def);

    public ItemDef Get(int itemId)
    {
        if (!byId.TryGetValue(itemId, out var def))
            throw new KeyNotFoundException($"ItemDef not found: {itemId}");

        return def;
    }
}
