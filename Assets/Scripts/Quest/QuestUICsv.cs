using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuestUICsv : MonoBehaviour
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
    [SerializeField] private Button closeButton;

    [Header("표시용 로더(선택)")]
    [SerializeField] private ItemLoader itemLoader;

    [Header("옵션(임시 입력)")]
    [SerializeField] private bool toggleOnQ = true;
    [SerializeField] private bool closeOnEsc = true;

    private int selectedQuestId = -1;
    private readonly List<GameObject> spawnedRows = new();

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private void Start()
    {
        if (itemLoader == null)
            itemLoader = ItemLoader.Instance != null ? ItemLoader.Instance : FindAnyObjectByType<ItemLoader>();

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
        if (toggleOnQ && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
        }

        if (!IsOpen) return;

        if (closeOnEsc && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open()
    {
        selectedQuestId = -1;

        if (rootPanel != null) rootPanel.SetActive(true);
        if (titleText != null) titleText.text = "퀘스트 목록";

        if (detailPanel != null) detailPanel.SetActive(false);
        SetActionButtonVisible(false, "");

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnAbandonClicked);
        }

        RebuildList();
    }

    public void Close()
    {
        ClearList();

        if (rootPanel != null) rootPanel.SetActive(false);

        selectedQuestId = -1;
        if (detailPanel != null) detailPanel.SetActive(false);
        SetActionButtonVisible(false, "");

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

        // 전체 목록 정책:
        // - activeStates에 있는 것만(진행 중 + 목표달성(보고 가능) 포함)
        // - 보상 수령 완료는 제외
        var actives = qm.GetActiveStatesSnapshot();
        actives.Sort((a, b) => a.questId.CompareTo(b.questId));

        int shown = 0;

        for (int i = 0; i < actives.Count; i++)
        {
            var st = actives[i];
            if (st == null) continue;
            if (st.rewardClaimed) continue;
            if (!qm.TryGetDef(st.questId, out var def)) continue;

            string title = !string.IsNullOrWhiteSpace(def.questName) ? def.questName : $"Quest {st.questId}";
            string status = st.objectiveCompleted ? "보고 가능" : "진행 중";

            SpawnQuestRow(title, status, st.questId);
            shown++;
        }

        if (shown == 0)
            SpawnInfoRow("(진행 중인 퀘스트가 없어요)");

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

        var go = Instantiate(questRowPrefab, listContainer);
        go.SetActive(true);
        spawnedRows.Add(go);

        var row = go.GetComponent<QuestRowUI>();
        if (row != null)
            row.Setup(message, "", -1, _ => { });
    }

    private void SpawnQuestRow(string title, string status, int questId)
    {
        if (questRowPrefab == null || listContainer == null) return;

        var go = Instantiate(questRowPrefab, listContainer);
        go.SetActive(true);
        spawnedRows.Add(go);

        var row = go.GetComponent<QuestRowUI>();
        if (row != null)
            row.Setup(title, status, questId, OnQuestRowClicked);
    }

    private void ClearList()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
            if (spawnedRows[i] != null) Destroy(spawnedRows[i]);

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

        var st = qm.GetState(questId);
        if (st == null || st.rewardClaimed)
        {
            if (detailPanel != null) detailPanel.SetActive(false);
            SetActionButtonVisible(false, "");
            return;
        }

        if (detailPanel != null) detailPanel.SetActive(true);

        if (questNameText != null)
            questNameText.text = !string.IsNullOrWhiteSpace(def.questName) ? def.questName : $"Quest {questId}";

        if (questDescText != null)
            questDescText.text = !string.IsNullOrWhiteSpace(def.questDescription) ? def.questDescription : "(설명 없음)";

        if (questGoalText != null)
            questGoalText.text = $"진행도: {st.currentAmount}/{def.requiredAmount}";

        if (questRewardText != null)
            questRewardText.text = BuildQuestRewardText(def);

        if (questStateText != null)
            questStateText.text = st.objectiveCompleted ? "보고 가능" : "진행 중";

        // 요구사항: 포기만 가능(보고 가능이어도 포기 가능)
        SetActionButtonVisible(true, "포기하기");
    }

    private string BuildQuestRewardText(QuestDef def)
    {
        return QuestUiTextUtil.BuildQuestRewardText(def, itemLoader);
    }

    private string ResolveItemName(int itemId)
    {
        return QuestUiTextUtil.ResolveItemName(itemId, itemLoader);
    }

    private void SetActionButtonVisible(bool visible, string label)
    {
        if (actionButton != null) actionButton.gameObject.SetActive(visible);
        if (visible && actionButtonText != null) actionButtonText.text = label;
    }

    private void OnAbandonClicked()
    {
        if (selectedQuestId <= 0) return;

        var qm = QuestManagerCsv.Instance;
        if (qm == null || !qm.IsReady()) return;

        if (!qm.IsActive(selectedQuestId))
            return;

        bool ok = qm.Abandon(selectedQuestId);
        if (!ok) return;

        selectedQuestId = -1;
        if (detailPanel != null) detailPanel.SetActive(false);
        SetActionButtonVisible(false, "");
        RebuildList();
    }
}
