using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 로직 전담 서비스
/// - 상점 오픈 가능 여부(요일/시간)
/// - 상점 표시 아이템 목록 필터링
/// - 구매/판매 실제 처리
/// </summary>
public class ShopService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ShopLoader shopLoader;
    [SerializeField] private ItemLoader itemLoader;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private QuestManagerCsv questManager;

    [Serializable]
    public class ShopDisplayItem
    {
        public int shopId;
        public int itemId;

        public string itemName;
        public string description;
        public string spritePath;

        public int unitPrice;            // 구매 단가(표시용, 수량과 무관)
        public int minSelectableAmount;  // 보통 1
        public int maxSelectableAmount;  // ShopItem.stackMax (소모품 10, 장비/무기 1 등)

        public ShopItemEntryDef entryDef;
        public ItemDef itemDef;
    }

    [Serializable]
    public class ShopOpenContext
    {
        public ShopDef shopDef;
        public List<ShopDisplayItem> visibleItems = new();
    }

    private void Awake()
    {
        if (shopLoader == null) shopLoader = ShopLoader.Instance;
        if (itemLoader == null) itemLoader = ItemLoader.Instance;
        if (timeManager == null) timeManager = TimeManager.Instance;
        if (questManager == null) questManager = QuestManagerCsv.Instance;
    }

    // ------------------------------------------------------------
    // Open / Build
    // ------------------------------------------------------------

    public bool TryBuildOpenContextByNpc(int npcId, out ShopOpenContext ctx, out string failReason)
    {
        ctx = null;
        failReason = string.Empty;

        if (!TryGetDatabases(out var sdb, out var idb, out failReason))
            return false;

        if (!sdb.TryGetShopByNpcId(npcId, out var shopDef) || shopDef == null)
        {
            failReason = $"NPC({npcId})에 연결된 상점을 찾지 못했습니다.";
            return false;
        }

        return TryBuildOpenContextByShopId(shopDef.shopId, out ctx, out failReason);
    }

    public bool TryBuildOpenContextByShopId(int shopId, out ShopOpenContext ctx, out string failReason)
    {
        ctx = null;
        failReason = string.Empty;

        if (!TryGetDatabases(out var sdb, out var idb, out failReason))
            return false;

        if (!sdb.TryGetShop(shopId, out var shopDef) || shopDef == null)
        {
            failReason = $"ShopId({shopId}) 상점을 찾지 못했습니다.";
            return false;
        }

        if (!IsShopOpenNow(shopDef, out failReason))
            return false;

        var list = BuildVisibleItems(shopDef, sdb, idb);

        ctx = new ShopOpenContext
        {
            shopDef = shopDef,
            visibleItems = list
        };

        return true;
    }

    // ------------------------------------------------------------
    // Buy / Sell
    // ------------------------------------------------------------

    public bool TryBuyItem(
        InventorySystem inventorySystem,
        PlayerGold playerGold,
        ShopDisplayItem displayItem,
        int amount,
        out string failReason)
    {
        failReason = string.Empty;

        if (inventorySystem == null || !inventorySystem.IsReady)
        {
            failReason = "인벤토리 시스템이 준비되지 않았습니다.";
            return false;
        }

        if (playerGold == null)
        {
            failReason = "PlayerGold 참조가 없습니다.";
            return false;
        }

        if (displayItem == null || displayItem.itemDef == null)
        {
            failReason = "구매 대상 아이템 정보가 없습니다.";
            return false;
        }

        int min = Mathf.Max(1, displayItem.minSelectableAmount);
        int max = Mathf.Max(1, displayItem.maxSelectableAmount);
        amount = Mathf.Clamp(amount, min, max);

        if (amount <= 0)
        {
            failReason = "구매 수량이 올바르지 않습니다.";
            return false;
        }

        long totalCost = (long)displayItem.unitPrice * amount;
        if (totalCost <= 0)
        {
            failReason = "가격 정보가 올바르지 않습니다.";
            return false;
        }

        if (!playerGold.HasEnough(totalCost))
        {
            failReason = "골드가 부족합니다.";
            return false;
        }

        // 정확 구매를 위해 사전 용량 체크 (부분 추가 방지)
        if (!CanInventoryAcceptExactAmount(inventorySystem, displayItem.itemDef, amount, quality: 0))
        {
            failReason = "인벤토리 공간이 부족합니다.";
            return false;
        }

        int remain = inventorySystem.TryAddFromExternal(displayItem.itemId, amount);
        if (remain > 0)
        {
            // 여기 오면 위 capacity 계산과 실제 스택 조건이 어긋난 케이스 (품질/특수조건 등)
            failReason = "인벤토리에 아이템을 모두 추가하지 못했습니다.";
            return false;
        }

        if (!playerGold.TrySpend(totalCost))
        {
            // 이론상 거의 없음. 발생 시 인벤토리 롤백이 필요하지만 현재 구조에서는 어려움.
            // 실사용에서는 골드 먼저 체크했으므로 정상적으로는 여기 안 옴.
            failReason = "골드 차감에 실패했습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 좌클릭 판매(선택 판매): 같은 itemId 전체 합 기준, 실제 제거는 InventorySystem.RemoveItem(뒤에서부터)
    /// </summary>
    public bool TrySellSelected(
        InventorySystem inventorySystem,
        PlayerGold playerGold,
        int itemId,
        int amount,
        out string failReason)
    {
        failReason = string.Empty;

        if (inventorySystem == null || !inventorySystem.IsReady)
        {
            failReason = "인벤토리 시스템이 준비되지 않았습니다.";
            return false;
        }

        if (playerGold == null)
        {
            failReason = "PlayerGold 참조가 없습니다.";
            return false;
        }

        if (!inventorySystem.TryGetDef(itemId, out var itemDef) || itemDef == null)
        {
            failReason = $"아이템 정의를 찾지 못했습니다. itemId={itemId}";
            return false;
        }

        int totalOwned = inventorySystem.CountItem(itemId);
        if (totalOwned <= 0)
        {
            failReason = "판매할 아이템이 없습니다.";
            return false;
        }

        amount = Mathf.Clamp(amount, 1, totalOwned);

        int unitSellPrice = Mathf.Max(0, itemDef.sellPrice);
        if (unitSellPrice <= 0)
        {
            failReason = "판매 불가 아이템입니다.";
            return false;
        }

        bool removed = inventorySystem.RemoveItem(itemId, amount); // 네 구현: 뒤에서부터 제거
        if (!removed)
        {
            failReason = "인벤토리에서 아이템 제거에 실패했습니다.";
            return false;
        }

        long gained = (long)unitSellPrice * amount;
        playerGold.Add(gained);
        return true;
    }

    /// <summary>
    /// 우클릭 즉시 판매(1개): 반드시 클릭한 슬롯에서 먼저 1개 제거
    /// </summary>
    public bool TryQuickSellFromInventorySlot(
    InventorySystem inventorySystem,
    PlayerGold playerGold,
    int slotIndex,
    out string failReason)
    {
        failReason = string.Empty;

        if (inventorySystem == null || !inventorySystem.IsReady)
        {
            failReason = "인벤토리 시스템이 준비되지 않았습니다.";
            return false;
        }

        if (playerGold == null)
        {
            failReason = "PlayerGold 참조가 없습니다.";
            return false;
        }

        // ContainerGrid는 Count가 아니라 slotCount
        if (slotIndex < 0 || slotIndex >= inventorySystem.Inventory.slotCount)
        {
            failReason = "슬롯 인덱스가 범위를 벗어났습니다.";
            return false;
        }

        var stack = inventorySystem.Inventory.Get(slotIndex);
        if (stack.IsEmpty || stack.amount <= 0)
        {
            failReason = "빈 슬롯입니다.";
            return false;
        }

        if (!inventorySystem.TryGetDef(stack.itemId, out var itemDef) || itemDef == null)
        {
            failReason = $"아이템 정의를 찾지 못했습니다. itemId={stack.itemId}";
            return false;
        }

        int unitSellPrice = Mathf.Max(0, itemDef.sellPrice);
        if (unitSellPrice <= 0)
        {
            failReason = "판매 불가 아이템입니다.";
            return false;
        }

        // 클릭한 슬롯에서 우선 차감
        if (!inventorySystem.TryRemoveFromSlot(slotIndex, 1))
        {
            failReason = "클릭한 슬롯에서 1개 판매에 실패했습니다.";
            return false;
        }

        playerGold.Add(unitSellPrice);
        return true;
    }

    public int GetSellUnitPrice(InventorySystem inventorySystem, int itemId)
    {
        if (inventorySystem == null) return 0;
        if (!inventorySystem.TryGetDef(itemId, out var itemDef) || itemDef == null) return 0;
        return Mathf.Max(0, itemDef.sellPrice);
    }

    // ------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------

    private bool TryGetDatabases(out ShopDatabase sdb, out ItemDatabase idb, out string failReason)
    {
        sdb = null;
        idb = null;
        failReason = string.Empty;

        if (shopLoader == null) shopLoader = ShopLoader.Instance;
        if (itemLoader == null) itemLoader = ItemLoader.Instance;
        if (timeManager == null) timeManager = TimeManager.Instance;
        if (questManager == null) questManager = QuestManagerCsv.Instance;

        if (shopLoader == null || !shopLoader.IsLoaded || shopLoader.ShopDb == null)
        {
            failReason = "ShopLoader가 준비되지 않았습니다.";
            return false;
        }

        if (itemLoader == null || !itemLoader.IsLoaded || itemLoader.itemDb == null)
        {
            failReason = "ItemLoader가 준비되지 않았습니다.";
            return false;
        }

        sdb = shopLoader.ShopDb;
        idb = itemLoader.itemDb;
        return true;
    }

    private bool IsShopOpenNow(ShopDef shopDef, out string failReason)
    {
        failReason = string.Empty;

        if (shopDef == null)
        {
            failReason = "상점 정의가 없습니다.";
            return false;
        }

        if (timeManager == null) timeManager = TimeManager.Instance;
        if (timeManager == null)
        {
            failReason = "TimeManager 참조가 없습니다.";
            return false;
        }

        // 요일 체크 (bit0=월, bit1=화 ... bit6=일 기준 가정)
        int weekdayIndex = Mathf.Clamp(timeManager.CurrentWeekdayIndex, 0, 6);
        int maskBit = 1 << weekdayIndex;
        if (shopDef.weekdayMask > 0 && (shopDef.weekdayMask & maskBit) == 0)
        {
            failReason = "오늘은 영업 요일이 아닙니다.";
            return false;
        }

        // 시간 체크 (분 단위)
        int nowMinute = timeManager.CurrentHour * 60 + timeManager.CurrentMinute;
        int openMinute = Mathf.Clamp(shopDef.openMin, 0, 24 * 60);
        int closeMinute = Mathf.Clamp(shopDef.closeMin, 0, 24 * 60);

        // 같은 날 영업만 가정 (예: 09:00~18:00)
        if (closeMinute > openMinute)
        {
            if (nowMinute < openMinute || nowMinute >= closeMinute)
            {
                failReason = "영업 시간이 아닙니다.";
                return false;
            }
        }
        else
        {
            // 혹시 자정 넘김 케이스가 들어오면 허용 처리
            bool inRange = (nowMinute >= openMinute) || (nowMinute < closeMinute);
            if (!inRange)
            {
                failReason = "영업 시간이 아닙니다.";
                return false;
            }
        }

        return true;
    }

    private List<ShopDisplayItem> BuildVisibleItems(ShopDef shopDef, ShopDatabase sdb, ItemDatabase idb)
    {
        var result = new List<ShopDisplayItem>();
        if (shopDef == null) return result;
        if (sdb == null || idb == null) return result;

        // ShopDatabase는 GetEntries가 아니라 GetShopItems / TryGetShopItems 사용
        var entries = sdb.GetShopItems(shopDef.shopId);
        if (entries == null || entries.Count == 0) return result;

        foreach (var e in entries)
        {
            if (e == null) continue;

            // 현재 구조 기준 유효성 체크
            if (!e.IsValid) continue;
            if (e.itemId <= 0) continue;

            if (!IsEntryUnlocked(e))
                continue;

            if (!idb.TryGet(e.itemId, out var itemDef) || itemDef == null)
                continue;

            // 가격은 ItemDef에서 가져옴 (ShopItemEntryDef에 priceBuy 없음)
            int unitPrice = Mathf.Max(0, itemDef.buyPrice);

            int minSelectable = Mathf.Max(1, e.SafeStackMin);
            int maxSelectable = Mathf.Max(minSelectable, e.SafeStackMax);

            result.Add(new ShopDisplayItem
            {
                shopId = shopDef.shopId,
                itemId = e.itemId,
                itemName = itemDef.name,
                description = itemDef.description,
                spritePath = itemDef.spritePath,
                unitPrice = unitPrice,
                minSelectableAmount = minSelectable,
                maxSelectableAmount = maxSelectable,
                entryDef = e,
                itemDef = itemDef
            });
        }

        return result;
    }

    private bool IsEntryUnlocked(ShopItemEntryDef e)
    {
        if (e == null) return false;

        string unlockType = (e.unlockType ?? string.Empty).Trim().ToLowerInvariant();
        int p = e.unlockParam;

        if (string.IsNullOrEmpty(unlockType) || unlockType == "none")
            return true;

        if (unlockType == "day")
        {
            if (timeManager == null) timeManager = TimeManager.Instance;
            if (timeManager == null) return true; // 시간매니저가 없으면 막지 않음(디버그 친화)
            return timeManager.CurrentDay >= Mathf.Max(1, p);
        }

        if (unlockType == "quest")
        {
            if (questManager == null) questManager = QuestManagerCsv.Instance;
            if (questManager == null) return true;

            // 주의: 현재 공개 API 기준으로 "보상 수령 완료"를 언락 조건으로 사용
            // (네 의도와 다르면 여기만 바꾸면 됨)
            return questManager.IsRewardClaimed(p);
        }

        // 알 수 없는 unlockType이면 일단 표시(시트 오타 때문에 상점이 통째로 비는 상황 방지)
        return true;
    }

    private bool CanInventoryAcceptExactAmount(InventorySystem inventorySystem, ItemDef itemDef, int amount, int quality)
    {
        if (inventorySystem == null || itemDef == null) return false;
        if (amount <= 0) return true;

        int perStackMax = Mathf.Max(1, itemDef.maxStack);
        int capacity = 0;

        var grid = inventorySystem.Inventory;
        int slotCount = grid.slotCount;

        for (int i = 0; i < slotCount; i++)
        {
            var s = grid.Get(i);

            if (s.IsEmpty)
            {
                capacity += perStackMax;
            }
            else if (s.itemId == itemDef.itemId && s.quality == quality)
            {
                int room = perStackMax - s.amount;
                if (room > 0) capacity += room;
            }

            if (capacity >= amount)
                return true;
        }

        return false;
    }
}