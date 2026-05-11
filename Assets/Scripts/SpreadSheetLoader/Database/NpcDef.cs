using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcDef
{
    [Header("식별")]
    public int npcId;

    [Header("표시용")]
    public string npcDisplayName;
    public Sprite portrait;

    [Header("상호작용")]
    public float interactionRange = 3f;

    [Header("퀘스트")]
    public List<int> questIds = new List<int>(); // "101|201|301" 같은 문자열을 파싱해서 넣음

    [Header("상점")]
    public bool isShopNpc = false;
    public string shopType; // "Weapon/Tool/Potion/CookingMaterial" 등 문자열로

    [Header("대화")]
    public string portraitPath;  // CSV에서 "Portraits/Baker" 같은 경로
    public List<string> greetingsByAffection = new List<string>();  // 호감도별 인사말

    public static NpcDef FromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "")
            => row != null && row.TryGetValue(key, out var v) ? v : fallback;

        int GetInt(string key, int fallback = 0)
            => int.TryParse(Get(key), out var v) ? v : fallback;

        float GetFloat(string key, float fallback = 0f)
            => float.TryParse(Get(key), out var v) ? v : fallback;

        bool GetBool(string key, bool fallback = false)
        {
            var s = Get(key, fallback ? "TRUE" : "FALSE");
            if (bool.TryParse(s, out var v)) return v;
            return fallback;
        }

        int id = GetInt("npcId", 0);
        if (id <= 0) return null;

        var def = new NpcDef();
        def.npcId = id;
        def.npcDisplayName = Get("npcDisplayName", $"NPC_{id}");

        def.interactionRange = GetFloat("interactionRange", 2f);

        // questIds: "101|201|301"
        def.questIds = CsvUtil.ParseIntList(Get("questIds", ""), '|');

        def.isShopNpc = GetBool("isShopNpc", false);
        def.shopType = Get("shopType", "").Trim();

        // 초상화 경로 파싱
        def.portraitPath = Get("portraitPath", "");
        if (!string.IsNullOrEmpty(def.portraitPath))
        {
            def.portrait = Resources.Load<Sprite>(def.portraitPath);
            if (def.portrait == null)
                Debug.LogWarning($"[NpcDef] 초상화 로드 실패: {def.portraitPath}");
        }

        // 인사말 파싱 (호감도 0~10 단계별, "|"로 구분)
        string greetingsStr = Get("greetings", "");
        if (!string.IsNullOrEmpty(greetingsStr))
        {
            def.greetingsByAffection = new List<string>(greetingsStr.Split('|'));
        }

        // 기본 인사말이 없으면 디폴트
        if (def.greetingsByAffection.Count == 0)
        {
            def.greetingsByAffection.Add("안녕하세요!");
        }

        // 최소 방어
        if (def.interactionRange <= 0f) def.interactionRange = 2f;

        return def;
    }
}

