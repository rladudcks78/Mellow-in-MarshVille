using System.Collections.Generic;

public static class QuestUiTextUtil
{
    public static string ResolveItemName(int itemId, ItemLoader itemLoader)
    {
        if (itemId <= 0) return "";

        var loader = ItemLoader.Instance != null ? ItemLoader.Instance : itemLoader;
        if (loader != null && loader.IsLoaded && loader.itemDb != null &&
            loader.itemDb.TryGet(itemId, out var def) && def != null)
        {
            return !string.IsNullOrWhiteSpace(def.name) ? def.name : itemId.ToString();
        }

        return itemId.ToString();
    }

    public static string BuildQuestRewardText(QuestDef def, ItemLoader itemLoader)
    {
        if (def == null) return "";

        var parts = new List<string>();

        if (def.rewardItemId > 0 && def.rewardItemAmount > 0)
            parts.Add($"{ResolveItemName(def.rewardItemId, itemLoader)} x {def.rewardItemAmount}");

        if (def.goldReward > 0)
            parts.Add($"골드 {def.goldReward}");

        if (def.affectionReward > 0)
            parts.Add($"호감도 {def.affectionReward}");

        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }
}


