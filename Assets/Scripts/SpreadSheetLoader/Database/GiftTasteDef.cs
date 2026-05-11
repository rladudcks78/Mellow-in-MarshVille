using System;
using System.Collections.Generic;

public enum GiftTaste
{
    NoThanks = 0,   // +1
    Soso = 1,  // +0
    Dislike = 2,  // -1
    Like = 3      // +3
}

[Serializable]
public class GiftTasteDef
{
    public int npcId;
    public int itemId;
    public GiftTaste taste;
    public int affectionDelta;

    public static GiftTasteDef FromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "")
            => row != null && row.TryGetValue(key, out var v) ? v : fallback;

        int GetInt(string key, int fallback = 0)
            => int.TryParse(Get(key), out var v) ? v : fallback;

        int npcId = GetInt("npcId", 0);
        int itemId = GetInt("itemId", 0);
        if (npcId <= 0 || itemId <= 0) return null;

        var def = new GiftTasteDef();
        def.npcId = npcId;
        def.itemId = itemId;

        var tasteStr = Get("taste", "Normal").Trim();
        if (!Enum.TryParse<GiftTaste>(tasteStr, true, out var taste))
            taste = GiftTaste.Soso;
        def.taste = taste;

        int delta = GetInt("affectionDelta", int.MinValue);
        if (delta == int.MinValue)
            delta = GiftTasteUtil.GetDelta(taste);

        def.affectionDelta = delta;
        return def;
    }
}

public static class GiftTasteUtil
{
    public static int GetDelta(GiftTaste taste)
    {
        switch (taste)
        {
            case GiftTaste.Like: return 3;
            case GiftTaste.Dislike: return -1;
            case GiftTaste.NoThanks: return 0;
            case GiftTaste.Soso:
            default: return 1;
        }
    }
}
