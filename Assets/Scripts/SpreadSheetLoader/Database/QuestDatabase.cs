using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 정의(정적 데이터) DB.
/// - id->def 딕셔너리
/// </summary>
public class QuestDatabase
{
    private readonly Dictionary<int, QuestDef> byId = new();

    public int Count => byId.Count;

    public void Build(IEnumerable<QuestDef> defs)
    {
        byId.Clear();

        if (defs == null) return;

        foreach (var def in defs)
        {
            if (def == null) continue;

            if (def.questID <= 0)
                throw new Exception($"Invalid questID : {def.questID}");

            // 중복 questID는 최신 것으로 덮어쓰기
            byId[def.questID] = def;
        }
    }

    public bool TryGet(int questID, out QuestDef def) => byId.TryGetValue(questID, out def);

    public QuestDef Get(int questID)
    {
        if (!byId.TryGetValue(questID, out var def))
            throw new KeyNotFoundException($"QuestDef not found: {questID}");
        return def;
    }

    public IEnumerable<QuestDef> All()
    {
        foreach (var kv in byId)
            yield return kv.Value;
    }
}

