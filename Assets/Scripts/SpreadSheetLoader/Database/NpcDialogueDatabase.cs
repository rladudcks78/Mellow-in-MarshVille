using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NpcDialogueDatabase
{
    private Dictionary<int, Dictionary<int, NpcDialogueDef>> nodesByNpc
        = new Dictionary<int, Dictionary<int, NpcDialogueDef>>();

    private Dictionary<int, List<NpcDialogueDef>> entryNodesByNpc
        = new Dictionary<int, List<NpcDialogueDef>>();

    private Dictionary<int, List<NpcDialogueDef>> monologueNodesByNpc
        = new Dictionary<int, List<NpcDialogueDef>>();

    // [ADD] [npcId][systemKey] = system line node list
    private Dictionary<int, Dictionary<string, List<NpcDialogueDef>>> systemNodesByNpc
        = new Dictionary<int, Dictionary<string, List<NpcDialogueDef>>>();

    private List<NpcDialogueDef> allNodes = new List<NpcDialogueDef>();

    private HashSet<string> seenOnceNodes = new HashSet<string>();

    public int Count => allNodes.Count;

    public void Add(NpcDialogueDef def)
    {
        if (def == null) return;

        if (def.npcId <= 0)
        {
            Debug.LogWarning($"[NpcDialogueDatabase] Skip invalid npcId. nodeId={def.nodeId}");
            return;
        }

        if (def.nodeId <= 0)
        {
            Debug.LogWarning($"[NpcDialogueDatabase] Skip invalid nodeId. npcId={def.npcId}");
            return;
        }

        allNodes.Add(def);

        Dictionary<int, NpcDialogueDef> nodeMap = null;

        if (nodesByNpc.ContainsKey(def.npcId))
        {
            nodeMap = nodesByNpc[def.npcId];
        }
        else
        {
            nodeMap = new Dictionary<int, NpcDialogueDef>();
            nodesByNpc[def.npcId] = nodeMap;

            entryNodesByNpc[def.npcId] = new List<NpcDialogueDef>();
            monologueNodesByNpc[def.npcId] = new List<NpcDialogueDef>();

            // [ADD]
            systemNodesByNpc[def.npcId] = new Dictionary<string, List<NpcDialogueDef>>();
        }

        nodeMap[def.nodeId] = def;

        if (def.isEntryNode)
            entryNodesByNpc[def.npcId].Add(def);

        if (def.speaker == "monologue")
            monologueNodesByNpc[def.npcId].Add(def);

        // [ADD] systemKey 인덱싱
        if (def.IsSystemLineNode)
        {
            var key = (def.systemKey ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key))
            {
                var map = systemNodesByNpc[def.npcId];
                if (!map.TryGetValue(key, out var list) || list == null)
                {
                    list = new List<NpcDialogueDef>();
                    map[key] = list;
                }
                list.Add(def);
            }
        }
    }

    public void FinalizeEntryNodes()
    {
        // GetEntryNode에서 동적 필터링+정렬
        Debug.Log($"[NpcDialogueDatabase] Finalized {entryNodesByNpc.Count} NPCs (dynamic sorting)");

        // system nodes만 정렬 유지 (필터링 없음)
        foreach (var npcId in systemNodesByNpc.Keys.ToList())
        {
            var map = systemNodesByNpc[npcId];
            if (map == null) continue;
            foreach (var key in map.Keys.ToList())
            {
                var list = map[key];
                if (list == null) continue;
                map[key] = list.OrderByDescending(n => n.priority).ToList();
            }
        }
    }


    public NpcDialogueDef GetEntryNode(int npcId, int affectionStage, string timeOfDay, string weather, string eventKey)
    {
        if (npcId <= 0 || !entryNodesByNpc.TryGetValue(npcId, out var list) || list == null)
            return null;

        // 동적 필터링 + 우선순위 정렬
        var validNodes = list
            .Where(n => n != null && n.MatchesCondition(affectionStage, timeOfDay, weather, eventKey))
            .Where(n => !n.once || !IsNodeSeen(n.npcId, n.nodeId))
            .OrderByDescending(n => n.priority)
            .ToList();

        if (validNodes.Count == 0) return null;

        var picked = validNodes[0];  // 최고 우선순위
        Debug.Log($"[EntryNode] NPC{npcId}: picked {picked.nodeId} (pri={picked.priority}, valid={validNodes.Count})");
        return picked;
    }


    public NpcDialogueDef GetMonologueNode(int npcId, int affectionStage, string timeOfDay, string weather, string eventKey)
    {
        if (npcId <= 0 || !monologueNodesByNpc.TryGetValue(npcId, out var list) || list == null)
            return null;

        var validNodes = list
            .Where(n => n != null && n.MatchesCondition(affectionStage, timeOfDay, weather, eventKey))
            .Where(n => !n.once || !IsNodeSeen(n.npcId, n.nodeId))
            .OrderByDescending(n => n.priority)
            .ToList();

        if (validNodes.Count == 0) return null;
        return validNodes[Random.Range(0, validNodes.Count)];
    }


    public NpcDialogueDef GetNode(int npcId, int nodeId)
    {
        if (npcId <= 0 || nodeId <= 0) return null;

        if (nodesByNpc.ContainsKey(npcId) && nodesByNpc[npcId].ContainsKey(nodeId))
            return nodesByNpc[npcId][nodeId];

        return null;
    }

    public NpcDialogueDef GetSystemNode(int npcId, string systemKey, int affectionStage, string timeOfDay, string weather, string eventKey)
    {
        // 1. systemKey 빈칸 허용: 기본 "" 처리
        string key = string.IsNullOrWhiteSpace(systemKey) ? "" : systemKey.Trim().ToLowerInvariant();

        if (!systemNodesByNpc.TryGetValue(npcId, out var keyMap) || keyMap == null)
            return null;

        if (!keyMap.TryGetValue(key, out var list) || list == null || list.Count == 0)
        {
            // 2. systemKey 매칭 실패 시 "default" fallback 시도
            if (keyMap.TryGetValue("default", out var defaultList) && defaultList != null && defaultList.Count > 0)
            {
                list = defaultList;
            }
            else return null;
        }

        // 3. 조건 기본값 대체 (빈칸 허용)
        string safeTime = timeOfDay ?? "" ?? "any";
        string safeWeather = weather ?? "" ?? "any";
        string safeEvent = eventKey ?? "" ?? "any";

        return list
            .Where(n => n != null && n.MatchesCondition(affectionStage, safeTime, safeWeather, safeEvent))
            .Where(n => !n.once || !IsNodeSeen(n.npcId, n.nodeId))
            .OrderByDescending(n => n.priority)
            .FirstOrDefault();
    }


    public List<NpcDialogueDef> GetChoiceGroup(int npcId, int choiceGroupId, int affectionStage, string timeOfDay, string weather, string eventKey)
    {
        if (npcId <= 0 || choiceGroupId <= 0 || !nodesByNpc.TryGetValue(npcId, out var nodes))
            return new List<NpcDialogueDef>();

        var validChoices = nodes.Values
            .Where(n => n != null && n.choiceGroupId == choiceGroupId)
            .Where(n => n.MatchesCondition(affectionStage, timeOfDay, weather, eventKey))
            .Where(n => !n.once || !IsNodeSeen(n.npcId, n.nodeId))
            .OrderByDescending(n => n.priority)
            .ThenBy(n => n.nodeId)
            .ToList();

        Debug.Log($"[ChoiceGroup] NPC{npcId}, group{choiceGroupId}: {validChoices.Count} valid");
        return validChoices;
    }


    public List<NpcDialogueDef> GetChoiceGroup(int npcId, int choiceGroupId)
    {
        return GetChoiceGroup(npcId, choiceGroupId, -1, "", "", "");
    }

    private static string MakeSeenKey(int npcId, int nodeId) => $"{npcId}:{nodeId}";

    private bool IsNodeSeen(int npcId, int nodeId)
    {
        if (npcId <= 0 || nodeId <= 0) return false;
        return seenOnceNodes.Contains(MakeSeenKey(npcId, nodeId));
    }

    public void MarkNodeAsSeen(int npcId, int nodeId)
    {
        if (npcId <= 0 || nodeId <= 0) return;
        seenOnceNodes.Add(MakeSeenKey(npcId, nodeId));
    }

    public void MarkNodeAsSeen(int nodeId)
    {
        Debug.LogWarning($"[NpcDialogueDatabase] MarkNodeAsSeen(nodeId) is deprecated. Use MarkNodeAsSeen(npcId, nodeId). nodeId={nodeId}");
    }

    public HashSet<string> GetSeenNodes() => seenOnceNodes;

    public void SetSeenNodes(HashSet<string> nodes)
    {
        seenOnceNodes = nodes ?? new HashSet<string>();
    }
}
