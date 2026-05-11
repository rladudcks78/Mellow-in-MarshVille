using System;
using UnityEngine;

/// <summary>
/// CSV에서 로드되는 퀘스트 정의(정적 데이터).
/// - 런타임 진행도/완료 여부는 QuestStatus 같은 "상태 클래스"가 따로 들고 있음
/// </summary>
[Serializable]
public class QuestDef
{
    [Header("퀘스트 식별")]
    public int questID;

    [Header("제공 NPC (npcId)")]
    public int questGiverNpcId;

    [Header("퀘스트 기본 정보")]
    public string questName;
    [TextArea(3, 5)]
    public string questDescription;

    [Header("퀘스트 목표")]
    public QuestGoalType goalType;
    public int requiredAmount = 1;

    // CollectItems 전용
    public int targetItemId;

    // TalkToNPC 전용
    public int targetNpcId;

    [Header("전달 퀘스트 정보 (Deliver 타입 전용)")]
    public int deliveryItemId = -1;
    public int deliveryTargetNpcId = 0;

    [Header("보상")]
    public int goldReward = 0;
    public int affectionReward = 0;

    public int rewardItemId = -1;
    public int rewardItemAmount = 1;

    [Header("순차 퀘스트 (선택사항)")]
    public int prerequisiteQuestID = -1;

    [Header("분류(확장 컬럼)")]
    public QuestGroup questGroup = QuestGroup.Unknown;                 // Main/Sub
    public QuestInternalSubType internalSubType = QuestInternalSubType.Unknown; // General/Story/Friendship
    public QuestDifficulty difficulty = QuestDifficulty.Unknown;       // Easy/Normal/Hard
    public int friendshipGate = 0;

    /// <summary>
    /// CSV row -> QuestDef 생성
    /// - 컬럼명은 대/소문자를 무시합니다.
    /// - goalType은 문자열(CollectItems/TalkToNPC/Deliver) 또는 숫자(0/1/2) 모두 허용
    /// </summary>
    public static QuestDef FromRow(System.Collections.Generic.Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "") =>
            row != null && row.TryGetValue(key, out var v) ? v : fallback;

        int GetInt(string key, int fallback = 0) =>
            int.TryParse(Get(key), out var v) ? v : fallback;

        QuestGoalType GetGoalType(string key, QuestGoalType fallback = QuestGoalType.CollectItems)
        {
            string raw = Get(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            // 숫자면 enum 인덱스로 파싱
            if (int.TryParse(raw.Trim(), out int n))
            {
                if (Enum.IsDefined(typeof(QuestGoalType), n))
                    return (QuestGoalType)n;
                return fallback;
            }

            // 문자열이면 enum 이름으로 파싱
            if (Enum.TryParse(raw.Trim(), true, out QuestGoalType t))
                return t;

            return fallback;
        }

        // 공용 enum 파서(QuestGroup/internalSubType/difficulty에서 사용)
        TEnum GetEnum<TEnum>(string key, TEnum fallback) where TEnum : struct
        {
            string raw = Get(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            if (int.TryParse(raw.Trim(), out int n))
            {
                if (Enum.IsDefined(typeof(TEnum), n))
                    return (TEnum)Enum.ToObject(typeof(TEnum), n);
                return fallback;
            }

            if (Enum.TryParse(raw.Trim(), true, out TEnum t))
                return t;

            return fallback;
        }

        // 필수 ID
        int id = GetInt("questID", GetInt("QuestID", 0));
        if (id <= 0) return null;

        var def = new QuestDef();

        def.questID = id;

        def.questGiverNpcId = GetInt("questGiverNpcId", 0);

        def.questName = Get("questName", "");
        def.questDescription = Get("questDescription", "");

        def.goalType = GetGoalType("goalType", QuestGoalType.CollectItems);

        def.targetItemId = GetInt("targetItemId", 0);
        def.targetNpcId = GetInt("targetNpcId", 0);
        def.requiredAmount = GetInt("requiredAmount", 1);

        def.deliveryItemId = GetInt("deliveryItemId", -1);
        def.deliveryTargetNpcId = GetInt("deliveryTargetNpcId", 0);

        def.goldReward = GetInt("goldReward", 0);
        def.affectionReward = GetInt("affectionReward", 0);
        def.rewardItemId = GetInt("rewardItemId", -1);
        def.rewardItemAmount = GetInt("rewardItemAmount", 1);

        def.prerequisiteQuestID = GetInt("prerequisiteQuestID", -1);

        def.questGroup = GetEnum("questGroup", QuestGroup.Unknown);
        def.internalSubType = GetEnum("internalSubType", QuestInternalSubType.Unknown);
        def.difficulty = GetEnum("difficulty", QuestDifficulty.Unknown);

        def.friendshipGate = GetInt("friendshipGate", 0);

        // 최소 방어
        if (def.requiredAmount <= 0) def.requiredAmount = 1;
        if (def.rewardItemAmount <= 0) def.rewardItemAmount = 1;

        // 목표 타입별 필수값 체크(경고만)
        if (def.goalType == QuestGoalType.CollectItems && def.targetItemId <= 0)
            Debug.LogWarning($"[QuestDef] CollectItems인데 targetItemId가 비었음. questID={def.questID}");

        if (def.goalType == QuestGoalType.TalkToNPC && def.targetNpcId <= 0)
            Debug.LogWarning($"[QuestDef] TalkToNPC인데 targetNpcId가 비었음. questID={def.questID}");

        if (def.goalType == QuestGoalType.Deliver)
        {
            if (def.deliveryItemId <= 0)
                Debug.LogWarning($"[QuestDef] Deliver인데 deliveryItemId가 비었음. questID={def.questID}");
            if (def.deliveryTargetNpcId <= 0)
                Debug.LogWarning($"[QuestDef] Deliver인데 deliveryTargetNpcId가 비었음. questID={def.questID}");
        }

        if (def.internalSubType == QuestInternalSubType.Friendship)
        {
            if (def.friendshipGate != 20 && def.friendshipGate != 40 && def.friendshipGate != 60 &&
                def.friendshipGate != 80 && def.friendshipGate != 100)
            {
                Debug.LogWarning($"[QuestDef] Friendship 퀘스트인데 friendshipGate가 비었거나 잘못됨. questID={def.questID}, friendshipGate={def.friendshipGate}");
            }
        }
        else
        {
            // 우정 퀘스트가 아닌데 값이 들어오면 혼란 방지용 경고(선택)
            if (def.friendshipGate != 0)
                Debug.LogWarning($"[QuestDef] Friendship이 아닌데 friendshipGate가 설정됨. questID={def.questID}, friendshipGate={def.friendshipGate}");
        }

        return def;
    }
}