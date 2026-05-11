/// <summary>
/// 퀘스트가 메인/서브인지 구분.
/// </summary>
public enum QuestGroup
{
    Unknown = 0,
    Main = 1,
    Sub = 2
}

/// <summary>
/// 서브 퀘스트의 내부 타입.
/// 서브는 내부적으로 일반/스토리/우정이며, 플레이어는 직접 확인 불가
/// </summary>
public enum QuestInternalSubType
{
    Unknown = 0,
    General = 1,
    Story = 2,
    Friendship = 3
}

/// <summary>
/// 난이도.
/// 퀘스트 완료 호감도 증가는 난이도에 따라 차등
/// </summary>
public enum QuestDifficulty
{
    Unknown = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3
}

public enum QuestGoalType
{
    Unknown = 0,
    TalkToNPC = 1,
    CollectItems = 2,
    Deliver = 3
}