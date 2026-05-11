using System;
using System.Collections.Generic;

/// <summary>
/// TODO: JSON 세이브/로드 시스템이 생기면 이 DTO를 그대로 직렬화/역직렬화해서 사용.
/// - JsonUtility는 Dictionary 직렬화가 불편하므로(유니티 기본 제약),
///   List 기반으로 저장하는 구조로 설계. (Newtonsoft/MemoryPack 등을 쓰면 바꿔도 됨)
/// </summary>
[Serializable]
public class GameSaveData
{
    public RelationshipSaveData relationship = new RelationshipSaveData();
    public QuestSaveData quests = new QuestSaveData();
}

[Serializable]
public class RelationshipSaveData
{
    // 게임 내 '오늘' 키 저장용 (인사/선물 1일 1회 등의 기준)
    public int currentDayKey = 0;

    // npcId별 호감도
    public List<NpcAffectionEntry> affections = new List<NpcAffectionEntry>();

    // npcId별 cleared friendship gates
    public List<NpcFriendshipGatesEntry> clearedGates = new List<NpcFriendshipGatesEntry>();
}

[Serializable]
public class NpcAffectionEntry
{
    public int npcId;
    public int affection;
}

[Serializable]
public class NpcFriendshipGatesEntry
{
    public int npcId;
    public List<int> gates = new List<int>(); // 20/40/60/80/100
}

[Serializable]
public class QuestSaveData
{
    public List<int> claimedQuestIds = new List<int>();
    public List<QuestStateEntry> activeStates = new List<QuestStateEntry>();
}

[Serializable]
public class QuestStateEntry
{
    public int questId;
    public int currentAmount;
    public bool objectiveCompleted;
    public bool rewardClaimed;
}
