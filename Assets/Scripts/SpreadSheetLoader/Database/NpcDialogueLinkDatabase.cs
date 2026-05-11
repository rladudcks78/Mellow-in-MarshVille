using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NpcDialogueLinkDatabase
{
    private readonly Dictionary<int, Dictionary<int, List<NpcDialogueLinkDef>>> linksByNpcFrom
        = new Dictionary<int, Dictionary<int, List<NpcDialogueLinkDef>>>();

    private HashSet<string> seenOnceLinks = new HashSet<string>();

    public int Count { get; private set; } = 0;

    public void Add(NpcDialogueLinkDef def)
    {
        if (def == null) return;
        if (def.npcId <= 0 || def.fromNodeId <= 0 || def.toNodeId <= 0) return;

        if (!linksByNpcFrom.TryGetValue(def.npcId, out var fromMap))
        {
            fromMap = new Dictionary<int, List<NpcDialogueLinkDef>>();
            linksByNpcFrom[def.npcId] = fromMap;
        }

        if (!fromMap.TryGetValue(def.fromNodeId, out var list))
        {
            list = new List<NpcDialogueLinkDef>();
            fromMap[def.fromNodeId] = list;
        }

        list.Add(def);
        Count++;
    }

    public void FinalizeLinks()
    {
        // 정렬 제거: 동적 필터링으로 대체
        Debug.Log($"[FinalizeLinks] {Count} links ready (dynamic sorting)");
    }


    private static string MakeSeenKey(int npcId, int fromNodeId, int toNodeId)
        => $"{npcId}:{fromNodeId}:{toNodeId}";

    private bool IsLinkSeen(int npcId, int fromNodeId, int toNodeId)
        => seenOnceLinks.Contains(MakeSeenKey(npcId, fromNodeId, toNodeId));

    public void MarkLinkAsSeen(int npcId, int fromNodeId, int toNodeId)
        => seenOnceLinks.Add(MakeSeenKey(npcId, fromNodeId, toNodeId));

    public HashSet<string> GetSeenLinks() => seenOnceLinks;
    public void SetSeenLinks(HashSet<string> seen) => seenOnceLinks = seen ?? new HashSet<string>();

    public bool TryPickNextNodeId(
        int npcId,
        int fromNodeId,
        int affectionStage,
        string timeOfDay,
        string weather,
        string eventKey,
        Func<int, QuestStateCsv> getQuestState,
        out int toNodeId)
    {
        toNodeId = -1;

        if (npcId <= 0 || fromNodeId <= 0) return false;
        if (!linksByNpcFrom.TryGetValue(npcId, out var fromMap)) return false;
        if (!fromMap.TryGetValue(fromNodeId, out var list) || list == null || list.Count == 0) return false;

        // 조건 먼저 필터링 → 우선순위 정렬
        var validLinks = list.Where(link =>
            link != null &&
            link.MatchesCondition(affectionStage, timeOfDay, weather, eventKey) &&
            (!link.once || !IsLinkSeen(link.npcId, link.fromNodeId, link.toNodeId))
        ).ToList();

        if (validLinks.Count == 0)
        {
            Debug.Log($"[TryPickNext] No valid links for NPC{npcId}:{fromNodeId}");
            return false;
        }

        // 우선순위로 정렬 (동적)
        validLinks.Sort((a, b) => b.priority.CompareTo(a.priority));

        // 퀘스트 조건 재확인 (동적)
        var finalCandidates = new List<NpcDialogueLinkDef>();
        int bestPriority = int.MinValue;

        foreach (var link in validLinks)
        {
            if (link.requiredQuestId > 0)
            {
                var qs = getQuestState?.Invoke(link.requiredQuestId);
                if (!link.MatchesQuestCondition(qs)) continue;
            }

            if (link.priority > bestPriority)
            {
                finalCandidates.Clear();
                bestPriority = link.priority;
                finalCandidates.Add(link);
            }
            else if (link.priority == bestPriority)
            {
                finalCandidates.Add(link);
            }
        }

        if (finalCandidates.Count == 0) return false;

        var picked = finalCandidates[UnityEngine.Random.Range(0, finalCandidates.Count)];
        toNodeId = picked.toNodeId;

        if (picked.once) MarkLinkAsSeen(picked.npcId, picked.fromNodeId, picked.toNodeId);

        Debug.Log($"[TryPickNext] Picked {picked.fromNodeId}→{toNodeId} (pri={picked.priority}, valid={validLinks.Count})");
        return true;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();

        public static List<T> Get()
            => pool.Count > 0 ? pool.Pop() : new List<T>(16);

        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            pool.Push(list);
        }
    }
}
