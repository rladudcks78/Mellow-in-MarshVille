using System;
using UnityEngine;

/// <summary>
/// 플레이 중 변하는 퀘스트 상태(런타임).
/// - CSV(QuestDef)는 정적 데이터
/// - QuestStateCsv는 진행도/완료/보상수령 같은 동적 데이터
/// </summary>
[Serializable]
public class QuestStateCsv
{
    public int questId;

    // 현재 진행도(Collect/Talk/Deliver 모두 공용으로 사용)
    public int currentAmount;

    // 목표 달성(보고 전) 여부
    public bool objectiveCompleted;

    // 보상 수령(완전 완료) 여부: 선행 퀘스트 해금 기준으로 이 값을 사용
    public bool rewardClaimed;

    public QuestStateCsv(int questId)
    {
        this.questId = questId;
        this.currentAmount = 0;
        this.objectiveCompleted = false;
        this.rewardClaimed = false;
    }
}

