using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuestPopupUI : MonoBehaviour
{
    public event Action Closed;

    [Header("루트")]
    [SerializeField] private GameObject rootPanel;

    [Header("상단")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("목록")]
    [Tooltip("Scroll View/Viewport/Content를 연결하세요.")]
    [SerializeField] private Transform listContainer;
    [Tooltip("QuestRowPrefab(프리팹 루트)에 QuestRowUI.cs가 붙어 있어야 합니다.")]
    [SerializeField] private GameObject questRowPrefab;

    [Header("상세")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questDescText;
    [SerializeField] private TextMeshProUGUI questGoalText;
    [SerializeField] private TextMeshProUGUI questRewardText;
    [SerializeField] private TextMeshProUGUI questStateText;

    [Header("버튼")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private Button leaveButton;

    [Header("런타임 참조")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private NpcLoader npcLoader;
    [SerializeField] private ItemLoader itemLoader;

    [Header("옵션")]
    [SerializeField] private bool closeOnEsc = true;

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;

    private int currentNpcId = -1;
    private int selectedQuestId = -1;

    private enum ActionMode { None, Accept, Claim, Abandon, DeliverGive }
    private ActionMode currentActionMode = ActionMode.None;

    private readonly List<GameObject> spawnedRows = new();
    private readonly Queue<GameObject> rowPool = new Queue<GameObject>();

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private void Start()
    {
        if (inventorySystem == null) inventorySystem = FindAnyObjectByType<InventorySystem>();
        if (npcLoader == null) npcLoader = NpcLoader.Instance != null ? NpcLoader.Instance : FindAnyObjectByType<NpcLoader>();
        if (itemLoader == null) itemLoader = ItemLoader.Instance != null ? ItemLoader.Instance : FindAnyObjectByType<ItemLoader>();

        Close();
    }

    private void OnEnable()
    {
        if (QuestManagerCsv.Instance != null)
            QuestManagerCsv.Instance.OnQuestStateChanged += OnQuestStateChanged;
    }

    private void OnDisable()
    {
        if (QuestManagerCsv.Instance != null)
            QuestManagerCsv.Instance.OnQuestStateChanged -= OnQuestStateChanged;
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (closeOnEsc && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private GameObject GetRow()
    {
        GameObject go;
        if (rowPool.Count > 0)
        {
            go = rowPool.Dequeue();
        }
        else
        {
            go = Instantiate(questRowPrefab, listContainer);
        }

        go.transform.SetParent(listContainer, false);  // 부모 재설정
        go.SetActive(true);
        return go;
    }

    public void OpenForNpc(int npcId)
    {
        currentNpcId = npcId;
        selectedQuestId = -1;
        currentActionMode = ActionMode.None;

        if (rootPanel != null) rootPanel.SetActive(true);

        if (titleText != null) titleText.text = $"{ResolveNpcName(npcId)}의 퀘스트";

        if (detailPanel != null) detailPanel.SetActive(false);
        SetActionButtonVisible(false, "");

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(Close);
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnActionButtonClicked);
        }

        RebuildList();

        if (debugMode)
            Debug.Log($"[QuestPopupUI] OpenForNpc npcId={npcId}");
    }

    public void Close()
    {
        ClearList();

        if (rootPanel != null) rootPanel.SetActive(false);

        currentNpcId = -1;
        selectedQuestId = -1;

        Closed?.Invoke();
    }

    private void OnQuestStateChanged(int questId)
    {
        if (!IsOpen) return;

        RebuildList();

        if (selectedQuestId == questId)
            ShowDetail(questId);
    }

    private void RebuildList()
    {
        ClearList();

        var qm = QuestManagerCsv.Instance;
        if (qm == null || !qm.IsReady())
        {
            SpawnInfoRow("(퀘스트 DB 준비 전)");
            ForceListLayout();
            return;
        }

        var candidateQuestIds = new HashSet<int>();

        // 신규: giverNpcId == currentNpcId
        foreach (var def in qm.AllDefs())
        {
            if (def == null) continue;
            if (def.questID <= 0) continue;
            if (def.questGiverNpcId != currentNpcId) continue;

            candidateQuestIds.Add(def.questID);
        }

        // 진행 중: giver/target/deliveryTarget 중 하나라도 currentNpcId면 추가
        var actives = qm.GetActiveStatesSnapshot();
        for (int i = 0; i < actives.Count; i++)
        {
            var st = actives[i];
            if (st == null) continue;
            if (st.rewardClaimed) continue;

            if (!qm.TryGetDef(st.questId, out var def)) continue;

            bool related =
                def.questGiverNpcId == currentNpcId ||
                def.targetNpcId == currentNpcId ||
                def.deliveryTargetNpcId == currentNpcId;

            if (related)
                candidateQuestIds.Add(st.questId);
        }

        var list = new List<int>(candidateQuestIds);
        list.Sort();

        for (int i = 0; i < list.Count; i++)
        {
            int questId = list[i];

            if (qm.IsRewardClaimed(questId))
                continue;

            if (!qm.TryGetDef(questId, out var def))
                continue;

            bool canClaimHere = CanClaimRewardHere(qm, questId, currentNpcId);
            bool isActive = qm.IsActive(questId);
            bool canAccept = qm.CanAccept(questId, out _);
            bool canGiveHere = CanGiveDeliveryHere(qm, def, questId, currentNpcId);

            // 조건 미충족은 조용히 숨김(DeliverGive 포함)
            if (!canClaimHere && !isActive && !canAccept && !canGiveHere)
                continue;

            string title = !string.IsNullOrWhiteSpace(def.questName) ? def.questName : $"Quest {questId}";
            string status = ResolveQuestStatusLabel(qm, questId, currentNpcId);

            SpawnQuestRow(title, status, questId);
        }

        ForceListLayout();
    }

    private void ForceListLayout()
    {
        if (listContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void SpawnInfoRow(string message)
    {
        if (questRowPrefab == null || listContainer == null) return;

        var go = GetRow();  // 반환값 사용
        spawnedRows.Add(go);

        var row = go.GetComponent<QuestRowUI>();
        if (row != null)
            row.Setup(message, "", -1, _ => { });
    }

    private void SpawnQuestRow(string title, string status, int questId)
    {
        if (questRowPrefab == null || listContainer == null) return;

        var go = GetRow();  // 반환값 사용
        spawnedRows.Add(go);

        var row = go.GetComponent<QuestRowUI>();
        if (row != null)
            row.Setup(title, status, questId, OnQuestRowClicked);
    }

    private void ClearList()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            var go = spawnedRows[i];
            if (go == null) continue;

            go.SetActive(false);
            go.transform.SetParent(null, false);
            rowPool.Enqueue(go);
        }
        spawnedRows.Clear();
    }


    private void OnQuestRowClicked(int questId)
    {
        if (questId <= 0) return;

        selectedQuestId = questId;
        ShowDetail(questId);
    }

    private void ShowDetail(int questId)
    {
        var qm = QuestManagerCsv.Instance;
        if (qm == null || !qm.IsReady()) return;

        if (!qm.TryGetDef(questId, out var def))
            return;

        if (detailPanel != null) detailPanel.SetActive(true);

        if (questNameText != null)
            questNameText.text = !string.IsNullOrWhiteSpace(def.questName) ? def.questName : $"Quest {questId}";
        if (questDescText != null)
            questDescText.text = !string.IsNullOrWhiteSpace(def.questDescription) ? def.questDescription : "(설명 없음)";

        if (questGoalText != null) questGoalText.text = BuildQuestGoalText(qm, def, questId);
        if (questRewardText != null) questRewardText.text = BuildQuestRewardText(def);

        // Deliver 대상 NPC에서 아이템 부족이면: 버튼 숨김 + 부족 문구
        if (TryGetDeliveryNeedText(qm, def, questId, currentNpcId, out string needText))
        {
            currentActionMode = ActionMode.None;
            if (questStateText != null) questStateText.text = needText;
            SetActionButtonVisible(false, "");
            return;
        }

        bool canGiveHere = CanGiveDeliveryHere(qm, def, questId, currentNpcId);
        bool canClaimHere = CanClaimRewardHere(qm, questId, currentNpcId);
        bool canAccept = qm.CanAccept(questId, out _);
        bool isActive = qm.IsActive(questId);

        if (canGiveHere)
        {
            currentActionMode = ActionMode.DeliverGive;
            if (questStateText != null) questStateText.text = "전달 가능";
            SetActionButtonVisible(true, "아이템 주기");
        }
        else if (canClaimHere)
        {
            currentActionMode = ActionMode.Claim;
            if (questStateText != null) questStateText.text = "보고 가능";
            SetActionButtonVisible(true, "보상 받기");
        }
        else if (canAccept)
        {
            currentActionMode = ActionMode.Accept;
            if (questStateText != null) questStateText.text = "수락 가능";
            SetActionButtonVisible(true, "수락하기");
        }
        else if (isActive)
        {
            currentActionMode = ActionMode.Abandon;
            if (questStateText != null) questStateText.text = "진행 중";
            SetActionButtonVisible(true, "포기하기");
        }
        else
        {
            currentActionMode = ActionMode.None;
            if (questStateText != null) questStateText.text = "조건 미충족";
            SetActionButtonVisible(false, "");
        }
    }

    private void SetActionButtonVisible(bool visible, string label)
    {
        if (actionButton != null) actionButton.gameObject.SetActive(visible);
        if (visible && actionButtonText != null) actionButtonText.text = label;
    }

    private void OnActionButtonClicked()
    {
        if (selectedQuestId <= 0) return;

        var qm = QuestManagerCsv.Instance;
        if (qm == null || !qm.IsReady()) return;

        if (currentActionMode == ActionMode.Accept)
        {
            bool ok = qm.Accept(selectedQuestId);
            if (!ok) return;

            ShowDetail(selectedQuestId);
            RebuildList();
        }
        else if (currentActionMode == ActionMode.Claim)
        {
            bool ok = qm.TryClaimReward(selectedQuestId, currentNpcId, inventorySystem);
            if (!ok) return;

            selectedQuestId = -1;
            currentActionMode = ActionMode.None;

            if (detailPanel != null) detailPanel.SetActive(false);
            RebuildList();
        }
        else if (currentActionMode == ActionMode.Abandon)
        {
            bool ok = qm.Abandon(selectedQuestId);
            if (!ok) return;

            selectedQuestId = -1;
            currentActionMode = ActionMode.None;

            if (detailPanel != null) detailPanel.SetActive(false);
            RebuildList();
        }
        else if (currentActionMode == ActionMode.DeliverGive)
        {
            // 아이템 차감 + objectiveCompleted 처리
            qm.TryProcessDeliveryAtNpc(currentNpcId, inventorySystem);

            // 즉시 UI 갱신 -> 이제 "보상 받기"로 전환 가능
            ShowDetail(selectedQuestId);
            RebuildList();
        }
    }

    private bool CanGiveDeliveryHere(QuestManagerCsv qm, QuestDef def, int questId, int hereNpcId)
    {
        if (def.goalType != QuestGoalType.Deliver) return false;
        if (def.deliveryTargetNpcId != hereNpcId) return false;

        var st = qm.GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed) return false;
        if (st.objectiveCompleted) return false;

        if (inventorySystem == null || !inventorySystem.IsReady) return false;

        int have = inventorySystem.CountItem(def.deliveryItemId);
        return have >= def.requiredAmount;
    }

    private bool TryGetDeliveryNeedText(QuestManagerCsv qm, QuestDef def, int questId, int hereNpcId, out string text)
    {
        text = "";
        if (def.goalType != QuestGoalType.Deliver) return false;
        if (def.deliveryTargetNpcId != hereNpcId) return false;

        var st = qm.GetState(questId);
        if (st == null || st.rewardClaimed) return false;
        if (st.objectiveCompleted) return false;

        if (inventorySystem == null || !inventorySystem.IsReady) return false;

        int have = inventorySystem.CountItem(def.deliveryItemId);
        int need = def.requiredAmount;

        if (have >= need) return false;

        text = $"아이템 부족 ({have}/{need})";
        return true;
    }

    private string BuildQuestGoalText(QuestManagerCsv qm, QuestDef def, int questId)
    {
        var st = qm.GetState(questId);
        int cur = st != null ? st.currentAmount : 0;

        switch (def.goalType)
        {
            case QuestGoalType.TalkToNPC:
                return $"목표: {ResolveNpcName(def.targetNpcId)}와 대화하기 ({cur}/{def.requiredAmount})";

            case QuestGoalType.CollectItems:
                return $"목표: {ResolveItemName(def.targetItemId)} 모으기 ({cur}/{def.requiredAmount})";

            case QuestGoalType.Deliver:
                {
                    bool done = (st != null && st.objectiveCompleted);
                    string progress = done ? "1/1" : "0/1";
                    return $"목표: {ResolveNpcName(def.deliveryTargetNpcId)}에게 {ResolveItemName(def.deliveryItemId)} 전달 ({progress})";
                }

            default:
                return $"목표: {def.goalType} ({cur}/{def.requiredAmount})";
        }
    }

    private string BuildQuestRewardText(QuestDef def)
    {
        return QuestUiTextUtil.BuildQuestRewardText(def, itemLoader);
    }

    private string ResolveQuestStatusLabel(QuestManagerCsv qm, int questId, int npcId)
    {
        if (!qm.TryGetDef(questId, out var def)) return "";

        if (CanGiveDeliveryHere(qm, def, questId, npcId)) return "전달 가능";
        if (CanClaimRewardHere(qm, questId, npcId)) return "보고 가능";
        if (qm.IsActive(questId)) return "진행 중";
        if (qm.CanAccept(questId, out _)) return "수락 가능";
        return "조건 미충족";
    }

    private bool CanClaimRewardHere(QuestManagerCsv qm, int questId, int reportNpcId)
    {
        if (!qm.TryGetDef(questId, out var def)) return false;

        var st = qm.GetState(questId);
        if (st == null) return false;
        if (st.rewardClaimed) return false;
        if (!st.objectiveCompleted) return false;

        // 기본: giver에서 보고 가능
        if (def.questGiverNpcId == reportNpcId) return true;

        // Deliver: receiver에서도 보고 가능
        if (def.goalType == QuestGoalType.Deliver && def.deliveryTargetNpcId == reportNpcId) return true;

        // TalkToNPC: target에서도 보고 가능
        if (def.goalType == QuestGoalType.TalkToNPC && def.targetNpcId == reportNpcId) return true;

        return false;
    }

    private string ResolveNpcName(int npcId)
    {
        if (npcId <= 0) return "(알 수 없음)";

        var loader = NpcLoader.Instance != null ? NpcLoader.Instance : npcLoader;
        if (loader == null) loader = FindAnyObjectByType<NpcLoader>();
        npcLoader = loader;

        if (loader != null && loader.IsLoaded && loader.NpcDb != null &&
            loader.NpcDb.TryGet(npcId, out var def) && def != null)
            return def.npcDisplayName;

        return $"NPC({npcId})";
    }

    private string ResolveItemName(int itemId)
    {
        return QuestUiTextUtil.ResolveItemName(itemId, itemLoader);
    }

    private List<int> ResolveNpcQuestIds(int npcId)
    {
        var loader = NpcLoader.Instance != null ? NpcLoader.Instance : npcLoader;
        if (loader == null) loader = FindAnyObjectByType<NpcLoader>();
        npcLoader = loader;

        if (loader != null && loader.IsLoaded && loader.NpcDb != null &&
            loader.NpcDb.TryGet(npcId, out var def) && def != null && def.questIds != null)
            return def.questIds;

        return new List<int>();
    }

}
