using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManagerCsv : MonoBehaviour
{
    public static QuestManagerCsv Instance;

    [Header("Loader/DB 참조")]
    [SerializeField] private QuestLoader questLoader;
    [SerializeField] private ItemLoader itemLoader;   // UI 표시용(현재 이 스크립트에선 직접 사용 안 함)

    [Header("런타임 참조(선택)")]
    [Tooltip("CollectItems 수락 직후 진행도 동기화(기존 보유량 반영) 등에 사용.")]
    [SerializeField] private InventorySystem inventorySystem;

    [Header("동시 진행 제한(기획값)")]
    [SerializeField] private int maxActiveMain = 1;
    [SerializeField] private int maxActiveSub = 5;

    [Header("CollectItems 동기화 옵션")]
    [Tooltip("퀘스트 수락 시, 이미 인벤에 있는 아이템 개수를 진행도에 반영할지 여부.")]
    [SerializeField] private bool syncCollectProgressOnAccept = true;

    // questId
    public event Action<int> OnQuestStateChanged;

    private readonly List<QuestStateCsv> activeStates = new List<QuestStateCsv>();
    private readonly HashSet<int> claimedQuestIds = new HashSet<int>();

    private Dictionary<int, QuestStateCsv> activeStatesDict = new Dictionary<int, QuestStateCsv>();
    private bool isInitialized = false;

    private QuestDatabase QuestDb => questLoader != null ? questLoader.QuestDb : null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        InitializeReferences();
        TryBindInventoryEvents(inventorySystem);
    }

    private void OnEnable()
    {
        InitializeReferences();
        TryBindInventoryEvents(inventorySystem);
    }


    private void OnDisable()
    {
        UnbindInventoryEvents(inventorySystem);
    }

    private void InitializeReferences()
    {
        if (isInitialized) return;

        questLoader ??= FindAnyObjectByType<QuestLoader>();
        itemLoader ??= FindAnyObjectByType<ItemLoader>();
        inventorySystem ??= FindAnyObjectByType<InventorySystem>();

        isInitialized = true;
    }


    // =========================
    // Inventory 이벤트 바인딩
    // =========================

    private void TryBindInventoryEvents(InventorySystem inv)
    {
        if (inv == null) return;

        // 중복 구독 방지
        inv.OnItemAdded -= OnInventoryItemAdded;
        inv.OnItemAdded += OnInventoryItemAdded;
    }

    private void UnbindInventoryEvents(InventorySystem inv)
    {
        if (inv == null) return;
        inv.OnItemAdded -= OnInventoryItemAdded;
    }

    private void OnInventoryItemAdded(int itemId, int addedAmount)
    {
        // 인벤에 실제로 들어간 양을 그대로 CollectItems 진행도에 반영
        NotifyCollectedItem(itemId, addedAmount);
    }

    // =========================
    // Ready / DB
    // =========================

    public bool IsReady()
    {
        return questLoader != null && questLoader.IsLoaded && QuestDb != null;
    }

    public bool TryGetDef(int questId, out QuestDef def)
    {
        def = null;
        if (!IsReady()) return false;
        return QuestDb.TryGet(questId, out def);
    }

    public IEnumerable<QuestDef> AllDefs()
    {
        if (!IsReady() || QuestDb == null) yield break;
        foreach (var def in QuestDb.All())
            yield return def;
    }

    // =========================
    // State 조회
    // =========================

    public bool IsRewardClaimed(int questId) => claimedQuestIds.Contains(questId);

    public bool IsActive(int questId)
    {
        return activeStatesDict.TryGetValue(questId, out var st) && !st.rewardClaimed;
    }

    public QuestStateCsv GetState(int questId)
    {
        activeStatesDict.TryGetValue(questId, out var st);
        return st;
    }

    public List<QuestStateCsv> GetActiveStatesSnapshot()
    {
        return new List<QuestStateCsv>(activeStates);
    }

    // =========================
    // 선행/제한
    // =========================

    public bool IsPrerequisiteSatisfied(int questId)
    {
        if (!TryGetDef(questId, out var def)) return false;
        if (def.prerequisiteQuestID <= 0) return true;
        return IsRewardClaimed(def.prerequisiteQuestID);
    }

    private int CountActiveByGroup(QuestGroup group)
    {
        int count = 0;
        foreach (var kvp in activeStatesDict)  // List 대신 Dict 반복
        {
            var st = kvp.Value;
            if (st.rewardClaimed) continue;

            if (TryGetDef(st.questId, out var def) && def.questGroup == group)
                count++;
        }
        return count;
    }

    public bool CanAccept(int questId, out string reason)
    {
        reason = "";

        if (!IsReady())
        {
            reason = "Quest DB가 아직 준비되지 않았어요.";
            return false;
        }

        if (!TryGetDef(questId, out var def))
        {
            reason = $"QuestDef가 없어요. questId={questId}";
            return false;
        }

        if (IsActive(questId))
        {
            reason = "이미 진행 중인 퀘스트예요.";
            return false;
        }

        if (IsRewardClaimed(questId))
        {
            reason = "이미 완료(보상 수령)한 퀘스트예요.";
            return false;
        }

        if (!IsPrerequisiteSatisfied(questId))
        {
            reason = "선행 퀘스트가 완료되지 않았어요.";
            return false;
        }

        if (def.questGroup == QuestGroup.Main)
        {
            if (CountActiveByGroup(QuestGroup.Main) >= maxActiveMain)
            {
                reason = "메인 퀘스트는 동시에 1개만 진행할 수 있어요.";
                return false;
            }
        }
        else if (def.questGroup == QuestGroup.Sub)
        {
            if (CountActiveByGroup(QuestGroup.Sub) >= maxActiveSub)
            {
                reason = "서브 퀘스트는 동시에 최대 5개까지 진행할 수 있어요.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// (레거시) "퀘스트와 관련이 있냐" 기준. UI(버튼 표시)에는 쓰지 않는 것을 권장.
    /// </summary>
    public bool HasAnyQuestForNpc(int npcId)
    {
        if (npcId <= 0) return false;
        if (!IsReady()) return false;

        // giver 기반
        foreach (var def in AllDefs())
            if (def != null && def.questGiverNpcId == npcId)
                return true;

        // 진행중 관련
        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st == null || st.rewardClaimed) continue;
            if (!TryGetDef(st.questId, out var qdef)) continue;

            bool related =
                qdef.questGiverNpcId == npcId ||
                qdef.targetNpcId == npcId ||
                qdef.deliveryTargetNpcId == npcId;

            if (related) return true;
        }

        return false;
    }

    /// <summary>
    /// (신규) "해당 NPC에서 퀘스트 UI 리스트에 실제로 표시될 게 있냐" 기준.
    /// - 신규 후보: giverNpcId == npcId
    /// - 진행중 후보: giver/target/deliveryTarget 관련이면 포함
    /// - 단, 조건 미충족은 숨김(QuestPopupUI와 같은 규칙)
    /// </summary>
    public bool HasAnyVisibleQuestForNpc(int npcId, InventorySystem inv)
    {
        if (npcId <= 0) return false;
        if (!IsReady()) return false;

        var candidateQuestIds = new HashSet<int>();

        // A) 신규 후보: giver
        foreach (var def in AllDefs())
        {
            if (def == null) continue;
            if (def.questID <= 0) continue;
            if (def.questGiverNpcId != npcId) continue;

            candidateQuestIds.Add(def.questID);
        }

        // B) 진행중 관련 후보: giver/target/deliveryTarget
        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st == null || st.rewardClaimed) continue;
            if (!TryGetDef(st.questId, out var def)) continue;

            bool related =
                def.questGiverNpcId == npcId ||
                def.targetNpcId == npcId ||
                def.deliveryTargetNpcId == npcId;

            if (related)
                candidateQuestIds.Add(st.questId);
        }

        foreach (int questId in candidateQuestIds)
        {
            if (IsRewardClaimed(questId)) continue;
            if (!TryGetDef(questId, out var def)) continue;

            bool canClaimHere = CanClaimRewardHere_Internal(questId, npcId);
            bool isActive = IsActive(questId);
            bool canAccept = CanAccept(questId, out _);
            bool canGiveHere = CanGiveDeliveryHere_Internal(def, questId, npcId, inv);

            // QuestPopupUI 필터와 동일: 하나라도 true면 "표시 대상"
            if (canClaimHere || isActive || canAccept || canGiveHere)
                return true;
        }

        return false;
    }

    private bool CanClaimRewardHere_Internal(int questId, int reportNpcId)
    {
        if (!TryGetDef(questId, out var def)) return false;

        var st = GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed) return false;
        if (!st.objectiveCompleted) return false;

        if (def.questGiverNpcId == reportNpcId) return true;
        if (def.goalType == QuestGoalType.Deliver && def.deliveryTargetNpcId == reportNpcId) return true;
        if (def.goalType == QuestGoalType.TalkToNPC && def.targetNpcId == reportNpcId) return true;

        return false;
    }

    private bool CanGiveDeliveryHere_Internal(QuestDef def, int questId, int hereNpcId, InventorySystem inv)
    {
        if (def.goalType != QuestGoalType.Deliver) return false;
        if (def.deliveryTargetNpcId != hereNpcId) return false;

        var st = GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed) return false;
        if (st.objectiveCompleted) return false;

        if (inv == null || !inv.IsReady) return false;

        int have = inv.CountItem(def.deliveryItemId);
        return have >= def.requiredAmount;
    }

    // =========================
    // Accept / Abandon / Claim
    // =========================

    public bool Accept(int questId)
    {
        if (!CanAccept(questId, out string reason))
        {
            Debug.LogWarning($"[QuestManagerCsv] Accept 실패: {reason}");
            return false;
        }

        var st = new QuestStateCsv(questId);
        activeStates.Add(st);
        activeStatesDict[questId] = st;

        // 수락 직후 CollectItems 진행도 동기화(이미 들고 있던 아이템 반영)
        if (syncCollectProgressOnAccept)
        {
            bool changed = SyncCollectProgressFromInventoryIfNeeded(questId, inventorySystem);

            if (changed)
                OnQuestStateChanged?.Invoke(questId);
        }

        Debug.Log($"[QuestManagerCsv] 퀘스트 수락: questId={questId}");
        OnQuestStateChanged?.Invoke(questId);
        return true;
    }

    public bool Abandon(int questId)
    {
        for (int i = 0; i < activeStates.Count; i++)
        {
            if (activeStates[i].questId == questId)
            {
                activeStates.RemoveAt(i);
                activeStatesDict.Remove(questId);
                Debug.Log($"[QuestManagerCsv] 퀘스트 포기: questId={questId}");
                OnQuestStateChanged?.Invoke(questId);
                return true;
            }
        }
        return false;
    }

    public bool TryClaimReward(int questId, int reportNpcId, InventorySystem inventory)
    {
        if (!TryGetDef(questId, out var def)) return false;

        var st = GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed) return false;

        bool canReport =
            def.questGiverNpcId == reportNpcId ||
            (def.goalType == QuestGoalType.Deliver && def.deliveryTargetNpcId == reportNpcId) ||
            (def.goalType == QuestGoalType.TalkToNPC && def.targetNpcId == reportNpcId);

        if (!canReport)
        {
            Debug.Log("[QuestManagerCsv] 보고 NPC가 퀘스트 제공자/배달 대상이 아니에요.");
            return false;
        }

        if (!st.objectiveCompleted)
        {
            Debug.Log("[QuestManagerCsv] 목표를 아직 달성하지 못했어요.");
            return false;
        }

        // 아이템 보상
        if (def.rewardItemId > 0 && def.rewardItemAmount > 0)
        {
            if (inventory == null || !inventory.IsReady)
            {
                Debug.LogWarning("[QuestManagerCsv] InventorySystem이 없거나 준비 전이라 아이템 보상 지급 불가.");
                return false;
            }

            int remain = inventory.TryAddFromExternal(def.rewardItemId, def.rewardItemAmount);
            if (remain != 0)
            {
                Debug.LogWarning("[QuestManagerCsv] 인벤토리 공간 부족으로 보상 지급 실패.");
                return false;
            }
        }

        if (def.goldReward > 0)
            Debug.Log($"[QuestManagerCsv] 보상: 골드 {def.goldReward}");

        if (def.affectionReward > 0 && RelationshipManager.Instance != null)
            RelationshipManager.Instance.IncreaseAffection(def.questGiverNpcId, def.affectionReward);

        st.rewardClaimed = true;
        claimedQuestIds.Add(questId);
        activeStatesDict[questId] = st;

        // 우정 퀘스트 완료 시 게이트 해제
        if (def.internalSubType == QuestInternalSubType.Friendship &&
            def.friendshipGate > 0 &&
            RelationshipManager.Instance != null)
        {
            RelationshipManager.Instance.SetFriendshipGateCleared(def.questGiverNpcId, def.friendshipGate, true);
        }

        Debug.Log($"[QuestManagerCsv] 보상 수령 완료: questId={questId}");
        OnQuestStateChanged?.Invoke(questId);
        return true;
    }

    // =========================
    // Progress 입력(API)
    // =========================

    public void NotifyTalkedToNpc(int npcId)
    {
        if (!IsReady()) return;
        if (npcId <= 0) return;

        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st.rewardClaimed) continue;

            if (!TryGetDef(st.questId, out var def)) continue;
            if (def.goalType != QuestGoalType.TalkToNPC) continue;
            if (st.objectiveCompleted) continue;
            if (def.targetNpcId != npcId) continue;

            st.currentAmount = Mathf.Min(def.requiredAmount, st.currentAmount + 1);

            if (st.currentAmount >= def.requiredAmount)
            {
                st.objectiveCompleted = true;
                Debug.Log($"[QuestManagerCsv] Talk 목표 달성: questId={st.questId}");
            }

            OnQuestStateChanged?.Invoke(st.questId);
        }
    }

    public void NotifyCollectedItem(int itemId, int addedAmount)
    {
        if (!IsReady()) return;
        if (itemId <= 0) return;
        if (addedAmount <= 0) return;

        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st.rewardClaimed) continue;

            if (!TryGetDef(st.questId, out var def)) continue;
            if (def.goalType != QuestGoalType.CollectItems) continue;
            if (st.objectiveCompleted) continue;
            if (def.targetItemId != itemId) continue;

            st.currentAmount = Mathf.Min(def.requiredAmount, st.currentAmount + addedAmount);

            if (st.currentAmount >= def.requiredAmount)
            {
                st.objectiveCompleted = true;
                Debug.Log($"[QuestManagerCsv] Collect 목표 달성: questId={st.questId}");
            }

            OnQuestStateChanged?.Invoke(st.questId);
        }
    }

    public void TryProcessDeliveryAtNpc(int npcId, InventorySystem inventory)
    {
        if (!IsReady()) return;
        if (npcId <= 0) return;
        if (inventory == null || !inventory.IsReady) return;

        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st.rewardClaimed) continue;

            if (!TryGetDef(st.questId, out var def)) continue;
            if (def.goalType != QuestGoalType.Deliver) continue;
            if (st.objectiveCompleted) continue;
            if (def.deliveryTargetNpcId != npcId) continue;

            int have = inventory.CountItem(def.deliveryItemId);
            if (have < def.requiredAmount) continue;

            bool removed = inventory.RemoveItem(def.deliveryItemId, def.requiredAmount);
            if (!removed) continue;

            st.currentAmount = def.requiredAmount;
            st.objectiveCompleted = true;

            Debug.Log($"[QuestManagerCsv] Deliver 목표 달성: questId={st.questId}, targetNpcId={npcId}");
            OnQuestStateChanged?.Invoke(st.questId);
        }
    }

    // =========================
    // NPC 버튼 행동 판단
    // =========================

    public enum NpcQuestAction { None, CanAccept, CanClaimReward, InProgress }

    public NpcQuestAction GetNpcQuestAction(int npcId, out int questId)
    {
        questId = -1;
        if (!IsReady()) return NpcQuestAction.None;

        // 1) 보고 가능 (giver / Deliver receiver / Talk target)
        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st.rewardClaimed) continue;

            if (!TryGetDef(st.questId, out var def)) continue;

            bool canReportHere =
                def.questGiverNpcId == npcId ||
                (def.goalType == QuestGoalType.Deliver && def.deliveryTargetNpcId == npcId) ||
                (def.goalType == QuestGoalType.TalkToNPC && def.targetNpcId == npcId);

            if (canReportHere && st.objectiveCompleted)
            {
                questId = st.questId;
                return NpcQuestAction.CanClaimReward;
            }
        }

        // 2) 신규 수락 가능: giverNpcId 기반
        foreach (var def in AllDefs())
        {
            if (def == null) continue;
            if (def.questGiverNpcId != npcId) continue;

            if (CanAccept(def.questID, out _))
            {
                questId = def.questID;
                return NpcQuestAction.CanAccept;
            }
        }

        // 3) 관련 진행중
        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st.rewardClaimed) continue;

            if (!TryGetDef(st.questId, out var def)) continue;

            bool related =
                def.questGiverNpcId == npcId ||
                def.targetNpcId == npcId ||
                def.deliveryTargetNpcId == npcId;

            if (related)
            {
                questId = st.questId;
                return NpcQuestAction.InProgress;
            }
        }

        return NpcQuestAction.None;
    }

    // =========================
    // 수락 직후 동기화
    // =========================

    private bool SyncCollectProgressFromInventoryIfNeeded(int questId, InventorySystem inv)
    {
        if (!TryGetDef(questId, out var def)) return false;
        if (def.goalType != QuestGoalType.CollectItems) return false;

        var st = GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed || st.objectiveCompleted) return false;

        if (inv == null || !inv.IsReady) return false;

        int beforeAmount = st.currentAmount;
        bool beforeCompleted = st.objectiveCompleted;

        int have = inv.CountItem(def.targetItemId);
        int synced = Mathf.Min(def.requiredAmount, have);

        st.currentAmount = Mathf.Max(st.currentAmount, synced);

        if (st.currentAmount >= def.requiredAmount)
        {
            st.objectiveCompleted = true;
            Debug.Log($"[QuestManagerCsv] (Accept Sync) Collect 목표 달성: questId={questId}");
        }

        return st.currentAmount != beforeAmount || st.objectiveCompleted != beforeCompleted;
    }

    // =========================
    // Save/Load 연결용 API
    // =========================

    public QuestSaveData CaptureSnapshot()
    {
        var data = new QuestSaveData();

        foreach (var qid in claimedQuestIds)
            data.claimedQuestIds.Add(qid);

        data.claimedQuestIds.Sort();

        for (int i = 0; i < activeStates.Count; i++)
        {
            var st = activeStates[i];
            if (st == null) continue;

            data.activeStates.Add(new QuestStateEntry
            {
                questId = st.questId,
                currentAmount = st.currentAmount,
                objectiveCompleted = st.objectiveCompleted,
                rewardClaimed = st.rewardClaimed
            });
        }

        return data;
    }

    public void RestoreSnapshot(QuestSaveData data)
    {
        activeStates.Clear();
        claimedQuestIds.Clear();

        if (data == null) return;

        if (data.claimedQuestIds != null)
        {
            for (int i = 0; i < data.claimedQuestIds.Count; i++)
            {
                int qid = data.claimedQuestIds[i];
                if (qid <= 0) continue;
                claimedQuestIds.Add(qid);
            }
        }

        if (data.activeStates != null)
        {
            for (int i = 0; i < data.activeStates.Count; i++)
            {
                var e = data.activeStates[i];
                if (e == null) continue;
                if (e.questId <= 0) continue;

                var st = new QuestStateCsv(e.questId);
                st.currentAmount = Mathf.Max(0, e.currentAmount);
                st.objectiveCompleted = e.objectiveCompleted;
                st.rewardClaimed = e.rewardClaimed;

                activeStates.Add(st);
            }
        }

        // Dictionary 재구성 및 동기화 추가
        activeStatesDict.Clear();
        foreach (var st in activeStates)
            activeStatesDict[st.questId] = st;

        // 복원 후 인벤 동기화 (CollectItems만)
        if (inventorySystem?.IsReady == true)
        {
            foreach (var kvp in activeStatesDict)
            {
                var st = kvp.Value;
                if (!st.rewardClaimed && !st.objectiveCompleted)
                    SyncCollectProgressFromInventoryIfNeeded(st.questId, inventorySystem);
            }
        }

        //active, claimed 갱신
        NotifyAllQuestStateChanged();
    }

    public void NotifyAllQuestStateChanged()
    {
        //active + claimed 전부 한번씩 갱신 트리거
        foreach (var st in activeStates)
            if (st != null) OnQuestStateChanged?.Invoke(st.questId);

        foreach (var id in claimedQuestIds)
            OnQuestStateChanged?.Invoke(id);
    }
}
