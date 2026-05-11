using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노드 기반 NPC 대화 데이터
/// - 대화 트리 구조 지원
/// - 표정, 선택지, 조건부 분기 지원
/// </summary>
[Serializable]
public class NpcDialogueDef
{
    [Header("노드 식별")]
    public int nodeId;                 // 대화 노드 고유 ID
    public int npcId;                  // NPC ID
    public bool isEntryNode;

    [Header("발화자")]
    public string speaker;             // "npc" 또는 "player", "monologue", "router", "system"

    [Header("대화 내용")]
    public string dialogueText;        // 대사
    public string expressionKey;       // 표정 키

    [Header("시스템 문구 키(옵션)")]
    [Tooltip("speaker가 system일 때 사용")]
    public string systemKey;

    [Header("흐름 제어")]
    public int nextNodeId;             // (구버전 호환) 다음 노드 ID (-1이면 대화 종료)
    public int priority;               // 우선순위
    public bool once;                  // 한 번만 재생

    [Header("조건 (필터링용)")]
    public int affectionStage;         // 호감도 단계 (-1이면 무시)
    public string timeOfDay;           // 시간대 (빈 문자열이면 무시)
    public string weather;             // 날씨 (빈 문자열이면 무시)
    public string eventKey;            // 이벤트 키 (빈 문자열이면 무시)

    [Header("선택지 (옵션)")]
    public string choiceText;          // 선택지 텍스트
    public int choiceGroupId;          // 같은 그룹 ID끼리 선택지로 묶임 (0이면 선택지 아님)

    // UI 쪽에서 "선택지 노드인가?"를 nextNodeId가 아니라 이것으로 판단하면 안전
    public bool IsChoiceNode => choiceGroupId > 0;

    // 시스템 문구 노드인가?
    public bool IsSystemLineNode => !string.IsNullOrWhiteSpace(systemKey);


    // 라우터 노드인가? (매니저에서 즉시 넘기기)
    public bool IsRouterNode =>
        string.Equals((speaker ?? "").Trim(), "router", StringComparison.OrdinalIgnoreCase);

    public static NpcDialogueDef FromRow(Dictionary<string, string> row)
    {
        if (row == null) return null;

        // 공통 GetString/Int/Bool 유틸을 로컬 함수로 정의 (앞뒤 공백 처리 포함)
        string Get(string key, string fallback = "")
        {
            if (!row.TryGetValue(key, out var v) || v == null)
                return fallback;
            return v;
        }

        int GetInt(string key, int fallback = 0)
        {
            var s = Get(key, "");
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            return int.TryParse(s.Trim(), out var v) ? v : fallback;
        }

        bool GetBool(string key, bool fallback = false)
        {
            // CSV에서 오는 TRUE/FALSE/1/0/YES/NO + 앞뒤 공백까지 안전하게 처리
            var s = Get(key, fallback ? "TRUE" : "FALSE");
            s = (s ?? "").Trim(); // 공백 제거
            if (s.Length == 0) return fallback;

            var up = s.ToUpperInvariant();
            if (up == "TRUE" || up == "1" || up == "YES") return true;
            if (up == "FALSE" || up == "0" || up == "NO") return false;
            return fallback;
        }

        // nodeId, npcId가 유효하지 않으면 이 row는 무시 (헤더/쓰레기 행 방지)
        int nodeId = GetInt("nodeId", 0);
        if (nodeId <= 0) return null;

        int npcId = GetInt("npcId", 0);
        if (npcId <= 0) return null;

        var def = new NpcDialogueDef
        {
            nodeId = nodeId,
            npcId = npcId
        };

        // isEntryNode를 GetBool로 안정적으로 파싱
        def.isEntryNode = GetBool("isEntryNode", false);

        // speaker
        def.speaker = NormalizeKey(Get("speaker", "npc"));
        if (string.IsNullOrEmpty(def.speaker)) def.speaker = "npc";

        // 대사
        def.dialogueText = Get("dialogueText", "");

        // 표정
        def.expressionKey = NormalizeKey(Get("expressionKey", "normal"));
        if (string.IsNullOrEmpty(def.expressionKey)) def.expressionKey = "normal";

        // 링크-only로 진행 중이어도 구버전 호환 위해 유지
        def.nextNodeId = GetInt("nextNodeId", -1);

        def.priority = GetInt("priority", 0);
        def.once = GetBool("once", false);

        def.affectionStage = GetInt("affectionStage", -1);
        def.timeOfDay = NormalizeKey(Get("timeOfDay", ""));
        def.weather = NormalizeKey(Get("weather", ""));
        def.eventKey = NormalizeKey(Get("eventKey", ""));

        def.choiceText = (Get("choiceText", "") ?? "").Trim();
        def.choiceGroupId = GetInt("choiceGroupId", 0);

        // ===== systemKey 컬럼 로드 =====
        def.systemKey = NormalizeKey(Get("systemKey", ""));

        // 품질 체크(옵션): speaker가 system인데 systemKey가 비어있으면 경고(데이터 실수 잡기)
        if (string.Equals(def.speaker, "system", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(def.systemKey))
        {
            Debug.LogWarning($"[NpcDialogueDef] speaker=system but systemKey is empty. npcId={def.npcId}, nodeId={def.nodeId}");
        }

        return def;
    }

    private static string NormalizeKey(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
    }

    public bool MatchesCondition(int currentAffection, string currentTime, string currentWeather, string currentEvent)
    {
        currentTime = NormalizeKey(currentTime);
        currentWeather = NormalizeKey(currentWeather);
        currentEvent = NormalizeKey(currentEvent);

        // 호감도: -1이면 무시, >=0이면 정확히 일치
        if (affectionStage >= 0 && affectionStage != currentAffection)
            return false;

        // 시간: 빈칸이면 무시, 값 있으면 정확히 일치
        if (!string.IsNullOrEmpty(timeOfDay) && timeOfDay != currentTime)
            return false;

        // 날씨: 빈칸이면 무시, 값 있으면 정확히 일치  
        if (!string.IsNullOrEmpty(weather) && weather != currentWeather)
            return false;

        // 이벤트: 빈칸이면 무시, 값 있으면 정확히 일치
        if (!string.IsNullOrEmpty(eventKey) && eventKey != currentEvent)
            return false;

        return true;
    }


}
