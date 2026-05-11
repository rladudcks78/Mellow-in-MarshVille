using System;
using UnityEngine;

[Serializable]
public class CropDef
{
    public int cropId;          // 작물 고유 ID (예: 1)
    public int seedItemId;      // 씨앗 아이템 ID (예: 08001)
    public int harvestItemId;   // 수확물 아이템 ID (예: 01001)
    public int growDays;        // 다 자라는데 걸리는 총 일수 (예: 4일)
    public int harvestMin;      // 최소 수확량
    public int harvestMax;      // 최대 수확량

    
    // 런타임 데이터 (게임 실행 중에 채워지는 데이터)
    // 성장 단계별 스프라이트 배열 (0단계: 씨앗 ~ N단계: 수확직전)
    // SpriteResolver를 통해 자동으로 채워집니다.
    public Sprite[] growthSprites;

    /// <summary>
    /// 현재 성장일수(currentDay)를 기반으로 보여줄 스프라이트를 반환합니다.
    /// 이미지가 3장이든 5장이든, 성장 기간에 맞춰 비율대로 계산해줍니다.
    /// </summary>
    public Sprite GetSpriteByDay(int currentDay)
    {
        // 예외 처리: 이미지가 로드되지 않았으면 null 반환
        if (growthSprites == null || growthSprites.Length == 0) return null;

        // 예외 처리: 성장 기간이 0 이하인 데이터 오류 방지
        if (growDays <= 0) return growthSprites[growthSprites.Length - 1];

        // 1. 다 자랐거나 그 이상 지났으면 -> 마지막(다 자란) 이미지 반환
        if (currentDay >= growDays)
            return growthSprites[growthSprites.Length - 1];

        // 2. 성장 진행도 계산 (0.0f ~ 1.0f 사이의 값)
        // (float) 캐스팅을 해야 소수점 계산이 됩니다.
        float progress = (float)currentDay / growDays;

        // 3. 진행도에 따른 배열 인덱스 계산
        // 예: 이미지 4장, 진행도 0.5(절반) -> 인덱스 1 또는 2
        // Length - 1을 하는 이유는 마지막장은 '완료' 상태라서 성장 중에는 제외하거나 포함할지 결정해야 하는데,
        // 보통 마지막장은 수확 가능 상태이므로, 여기서는 전체 길이를 기준으로 비율을 나눕니다.
        int spriteIndex = Mathf.FloorToInt(progress * growthSprites.Length);

        // 인덱스가 배열 범위를 넘지 않게 안전장치(Clamp)
        spriteIndex = Mathf.Clamp(spriteIndex, 0, growthSprites.Length - 1);

        return growthSprites[spriteIndex];
    }
}