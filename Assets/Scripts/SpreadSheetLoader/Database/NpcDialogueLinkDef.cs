using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 노드 간 "링크(분기)" 데이터.
/// - fromNodeId에서 toNodeId로 이동 가능한 후보 1개를 의미합니다.
/// - 조건은 "분기 선택"에 사용합니다.
/// - once=true면 이 링크는 한 번만 사용됩니다.
/// </summary>
[Serializable]
public class NpcDialogueLinkDef
{
    [Header("식별")]
    public int npcId;
    public int fromNodeId;
    public int toNodeId;

    [Header("분기 우선순위")]
    public int priority;

    [Header("조건 (필터링용)")]
    public int affectionStage;   // -1이면 무시
    public string timeOfDay;     // 빈 문자열이면 무시
    public string weather;       // 빈 문자열이면 무시
    public string eventKey;      // 빈 문자열이면 무시

    [Header("링크 once")]
    public bool once;

    [Header("퀘스트 조건")]
    public int requiredQuestId;              // 0이면 무시
    public bool requireObjectiveCompleted;   // requiredQuestId>0 일 때만 의미
    public bool requireRewardClaimed;        // requiredQuestId>0 일 때만 의미

    private static string NormalizeKey(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
    }

    public static NpcDialogueLinkDef FromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "")
            => row != null && row.TryGetValue(key, out var v) ? v : fallback;

        int GetInt(string key, int fallback = 0)
        {
            var s = Get(key, "");
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            return int.TryParse(s, out var v) ? v : fallback;
        }

        bool GetBool(string key, bool fallback = false)
        {
            var s = Get(key, fallback ? "TRUE" : "FALSE");
            s = (s ?? "").Trim().ToUpperInvariant();
            if (s == "TRUE" || s == "1" || s == "YES") return true;
            if (s == "FALSE" || s == "0" || s == "NO") return false;
            return fallback;
        }

        int npcId = GetInt("npcId", 0);
        int fromNodeId = GetInt("fromNodeId", 0);
        int toNodeId = GetInt("toNodeId", 0);

        if (npcId <= 0 || fromNodeId <= 0 || toNodeId <= 0)
            return null;

        var def = new NpcDialogueLinkDef();
        def.npcId = npcId;
        def.fromNodeId = fromNodeId;
        def.toNodeId = toNodeId;

        def.priority = GetInt("priority", 0);

        def.affectionStage = GetInt("affectionStage", -1);
        def.timeOfDay = NormalizeKey(Get("timeOfDay", ""));
        def.weather = NormalizeKey(Get("weather", ""));
        def.eventKey = NormalizeKey(Get("eventKey", ""));

        def.once = GetBool("once", false);

        def.requiredQuestId = GetInt("requiredQuestId", 0);
        def.requireObjectiveCompleted = GetBool("requireObjectiveCompleted", false);
        def.requireRewardClaimed = GetBool("requireRewardClaimed", false);

        return def;
    }

    public bool MatchesCondition(int currentAffection, string currentTime, string currentWeather, string currentEvent)
    {
        currentTime = NormalizeKey(currentTime);
        currentWeather = NormalizeKey(currentWeather);
        currentEvent = NormalizeKey(currentEvent);

        if (affectionStage >= 0 && affectionStage != currentAffection)
            return false;

        if (!string.IsNullOrEmpty(timeOfDay) && timeOfDay != currentTime)
            return false;

        if (!string.IsNullOrEmpty(weather) && weather != currentWeather)
            return false;

        if (!string.IsNullOrEmpty(eventKey) && eventKey != currentEvent)
            return false;

        return true;
    }

    // 퀘스트 조건 체크
    public bool MatchesQuestCondition(QuestStateCsv qs)
    {
        if (requiredQuestId <= 0) return true; // 조건 없음

        if (qs == null) return false;

        if (requireObjectiveCompleted && !qs.objectiveCompleted) return false;
        if (requireRewardClaimed && !qs.rewardClaimed) return false;

        return true;
    }
}
