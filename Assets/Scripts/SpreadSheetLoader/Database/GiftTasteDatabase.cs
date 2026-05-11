using System.Collections.Generic;

public class GiftTasteDatabase
{
    private readonly Dictionary<int, Dictionary<int, GiftTasteDef>> map
        = new Dictionary<int, Dictionary<int, GiftTasteDef>>();

    public int Count { get; private set; }

    public void Add(GiftTasteDef def)
    {
        if (def == null) return;

        if (!map.TryGetValue(def.npcId, out var byItem))
        {
            byItem = new Dictionary<int, GiftTasteDef>();
            map[def.npcId] = byItem;
        }

        byItem[def.itemId] = def;
        Count++;
    }

    public bool TryGet(int npcId, int itemId, out GiftTasteDef def)
    {
        def = null;
        if (npcId <= 0 || itemId <= 0) return false;
        if (!map.TryGetValue(npcId, out var byItem)) return false;
        return byItem.TryGetValue(itemId, out def);
    }
}
