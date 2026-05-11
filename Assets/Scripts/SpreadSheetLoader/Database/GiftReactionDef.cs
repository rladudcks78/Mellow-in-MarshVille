using System;
using System.Collections.Generic;

[Serializable]
public class GiftReactionDef
{
    public int npcId;
    public GiftTaste taste;
    public string line;

    public static GiftReactionDef FromRow(Dictionary<string, string> row)
    {
        if (row == null) return null;

        int npcId = CsvUtil.GetInt(row, "npcId", 0);
        if (npcId <= 0) return null;

        string tasteStr = CsvUtil.GetString(row, "taste", "Soso");
        if (!Enum.TryParse(tasteStr, true, out GiftTaste taste))
            taste = GiftTaste.Soso;

        string line = CsvUtil.GetString(row, "line", "");
        if (string.IsNullOrWhiteSpace(line)) return null;

        return new GiftReactionDef
        {
            npcId = npcId,
            taste = taste,
            line = line
        };
    }
}
