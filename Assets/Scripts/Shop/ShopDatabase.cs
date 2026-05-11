using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShopDatabase
{
    private readonly Dictionary<int, ShopDef> shopById = new();
    private readonly Dictionary<int, List<ShopItemEntryDef>> itemsByShopId = new();
    private readonly Dictionary<int, int> shopIdByNpcId = new();

    public int ShopCount => shopById.Count;

    public void Clear()
    {
        shopById.Clear();
        itemsByShopId.Clear();
        shopIdByNpcId.Clear();
    }

    public void Build(IEnumerable<ShopDef> shops, IEnumerable<ShopItemEntryDef> items)
    {
        Clear();

        // 1) Shop 등록
        if (shops != null)
        {
            foreach (var shop in shops)
            {
                if (shop == null) continue;
                if (!shop.IsValid)
                {
                    Debug.LogWarning($"[ShopDatabase] Invalid ShopDef skipped. shopId={shop.shopId}, npcId={shop.npcId}");
                    continue;
                }

                // shopId 중복 시 최신값 덮어씀
                if (shopById.ContainsKey(shop.shopId))
                    Debug.LogWarning($"[ShopDatabase] Duplicate shopId={shop.shopId}. Latest row overrides previous.");

                shopById[shop.shopId] = shop;

                // npcId -> shopId 인덱스
                if (shopIdByNpcId.TryGetValue(shop.npcId, out var prevShopId))
                {
                    Debug.LogWarning($"[ShopDatabase] Duplicate npcId={shop.npcId} (prev shopId={prevShopId}, new shopId={shop.shopId}). Latest row overrides previous.");
                }
                shopIdByNpcId[shop.npcId] = shop.shopId;
            }
        }

        // 2) ShopItems 등록
        if (items != null)
        {
            foreach (var entry in items)
            {
                if (entry == null) continue;
                if (!entry.IsValid)
                {
                    Debug.LogWarning($"[ShopDatabase] Invalid ShopItemEntryDef skipped. shopId={entry.shopId}, itemId={entry.itemId}");
                    continue;
                }

                if (!itemsByShopId.TryGetValue(entry.shopId, out var list))
                {
                    list = new List<ShopItemEntryDef>();
                    itemsByShopId[entry.shopId] = list;
                }

                list.Add(entry);
            }
        }

        // 3) 정렬
        foreach (var kv in itemsByShopId)
        {
            kv.Value.Sort((a, b) =>
            {
                int c = a.sortOrder.CompareTo(b.sortOrder);
                if (c != 0) return c;

                c = string.Compare(a.category, b.category, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;

                return a.itemId.CompareTo(b.itemId);
            });
        }

        // 4) 없는 shopId를 참조하는 ShopItems 검증
        foreach (var shopId in itemsByShopId.Keys.ToList())
        {
            if (!shopById.ContainsKey(shopId))
            {
                Debug.LogWarning($"[ShopDatabase] ShopItems contains unknown shopId={shopId}. Items remain loaded but shop lookup will fail.");
            }
        }
    }

    // -------------------------------------------------
    // 조회
    // -------------------------------------------------

    public bool HasShop(int shopId) => shopById.ContainsKey(shopId);

    public bool HasShopForNpc(int npcId) => shopIdByNpcId.ContainsKey(npcId);

    public bool TryGetShop(int shopId, out ShopDef shop)
        => shopById.TryGetValue(shopId, out shop);

    public ShopDef GetShop(int shopId)
    {
        if (!shopById.TryGetValue(shopId, out var shop))
            throw new KeyNotFoundException($"ShopDef not found: {shopId}");
        return shop;
    }

    public bool TryGetShopByNpcId(int npcId, out ShopDef shop)
    {
        shop = null;

        if (!shopIdByNpcId.TryGetValue(npcId, out var shopId))
            return false;

        return shopById.TryGetValue(shopId, out shop);
    }

    public bool TryGetShopItems(int shopId, out List<ShopItemEntryDef> items)
        => itemsByShopId.TryGetValue(shopId, out items);

    public IReadOnlyList<ShopItemEntryDef> GetShopItems(int shopId)
    {
        if (itemsByShopId.TryGetValue(shopId, out var list))
            return list;

        return Array.Empty<ShopItemEntryDef>();
    }

    public bool TryGetShopItemEntry(int shopId, int itemId, out ShopItemEntryDef entry)
    {
        entry = null;
        if (!itemsByShopId.TryGetValue(shopId, out var list)) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].itemId == itemId)
            {
                entry = list[i];
                return true;
            }
        }

        return false;
    }
}