using System;
using UnityEngine;

[Serializable]
public class FoodDef
{
    public int itemId;            // 아이템 고유 ID
    public float usingTime;       // 섭취 소요 시간

    [Header("회복 로직")]
    public float healHP;          // 총 회복량
    public float healDuration;    // 회복 지속 시간 (0이면 즉시 회복)

    [Header("버프 로직")]
    public float buffDuration;    // 공격력, 속도 등 버프의 유지 시간
    public float addMaxHP;        // 최대 체력 증가량
    public float addAttackDamage; // 공격력 증가량
    public float addAttackSpeed;  // 공격 속도 증가량
    public float addPlayerSpeed;  // 이동 속도 증가량

    [Header("저항 (확장성)")]
    public float resistCold;      // 추위 저항
    public float resistHeat;      // 더위 저항

    // 품질에 따른 최종 수치 계산 함수 (기존 로직 유지)
    public float GetFinalValue(float baseValue, float multiplier)
    {
        return MathF.Round(baseValue * multiplier, 1);
    }
}