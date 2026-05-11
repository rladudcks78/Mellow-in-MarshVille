using UnityEngine;

public class ShopDef
{
    [Header("기본")]
    public int shopId;
    public string shopName;
    public int npcId;               // NPC와 1:1 매칭

    [Header("영업 시간")]
    [Tooltip("하루 분 단위, 예 : 540 = 오전 9시")]
    public int openMin;
    [Tooltip("하루 분 단위, 예 : 1200 = 오후 8시")]
    public int closeMin;

    [Header("영업 요일")]
    [Tooltip("월 = 1, 화 = 2, 수 = 4, 목 = 8 ... 일 = 64")]
    public int weekdayMask;

    [Header("확장용")]
    public string refreshType;   // 상점 품목 갱신 방식 

    public bool IsValid => shopId > 0 && npcId > 0;

}
