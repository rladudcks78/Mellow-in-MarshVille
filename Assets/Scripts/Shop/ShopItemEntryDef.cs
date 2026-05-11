using System;
using UnityEngine;

public class ShopItemEntryDef
{
    [Header("키")]
    public int shopId;
    public int itemId;

    [Header("재고")]
    public string stockType;    // Infinite / Limited 등
    public int stock;           // 초기 재고량
    public int restock;         // 리필 수량

    [Header("구매 수량 제한")]
    public int stackMin;        // 1회 구매 최소 수량
    public int stackMax;        // 1회 구매 최대 수량

    [Header("UI")]
    public string category;     // Equipment / Weapon / Fishing / Food / Ingredient
    public int sortOrder;       // 같은 카테고리 내에서의 정렬 순서

    [Header("해금")]
    public string unlockType;  // None / Day / Quest 
    public int unlockParam;     

    public bool IsValid => shopId > 0 && itemId > 0;
    
    public bool IsInfiniteStock => 
        string.Equals(stockType, "Infinite", StringComparison.OrdinalIgnoreCase);

    public int SafeStackMin => Mathf.Max(1, stackMin);
    public int SafeStackMax => Mathf.Max(SafeStackMin, stackMax);
}
