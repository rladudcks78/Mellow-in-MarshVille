using System;
using UnityEngine;

[Serializable]
public class ItemDef
{
    [Header("기본 정보")]
    public int      itemId;
    public string   name;
    [TextArea]
    public string   description;
    public string   spritePath;
    public int      maxStack;

    [Header("가격")]
    public int      buyPrice;
    public int      sellPrice;

    [Header("플래그")]
    public bool     isSellable;
    public bool     isCookingredient;
    public bool     isQuestOnly;
    public bool     isUsable;
    public bool     isGiftable;
    public bool     isSeed;

    [Header("티어/ 타입")]
    public int      tier;
    public string   toolType;
    public string   weaponType;

    [Header("도구 범위 (S/M/L")]
    public int      areaW_S;
    public int      areaH_S;
    public int      areaW_M;
    public int      areaH_M;
    public int      areaW_L;
    public int      areaH_L;

    [Header("무기 스탯")]
    public float      attackDamage;
    public float    attackSpeed;

    // 분류 헬퍼
    public bool IsTool => !string.IsNullOrEmpty(toolType);
    public bool IsWeapon => !string.IsNullOrEmpty(weaponType);
    public bool IsEquipment => IsTool || IsWeapon;
    public bool IsIngredient => isCookingredient;
}
