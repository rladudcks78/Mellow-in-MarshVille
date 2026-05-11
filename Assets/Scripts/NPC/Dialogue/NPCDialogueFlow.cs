using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCDialogueFlowController : MonoBehaviour
{
    public enum UiStage
    {
        None,
        DialogueNode,
        CsvChoices,
        InteractionMenu
    }

    [Header("Refs")]
    [SerializeField] private NPCDialogueView view;
    [SerializeField] private NPCInteractionMenuController menu;

    [Header("continue")]
    [SerializeField] private string continueLabel = "계속 (Space)";

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;

    private UiStage stage = UiStage.None;
    private bool waitingForContinue = false;

    private int currentNpcId = -1;
    private string currentNpcDisplayName = "";
    private Sprite currentNpcPortrait = null;

    private NpcDialogueDef currentNode;
    private bool childPopupOpen = false;

    // 시스템 문구(그래프 진행과 무관) 표시 상태
    private bool isShowingSystemLine = false;
    private bool pendingReturnToMenuAfterSystemLine = false;

    public bool IsOpen { get; private set; }
    public UiStage Stage => stage;

    private void Awake()
    {
        if (view == null) view = GetComponentInChildren<NPCDialogueView>(true);
        if (menu == null) menu = GetComponentInChildren<NPCInteractionMenuController>(true);

        if (view != null)
        {
            view.TypingCompleted -= OnTypingCompleted;
            view.TypingCompleted += OnTypingCompleted;
        }
    }

    public void SetChildPopupOpen(bool open) => childPopupOpen = open;

    public void StartDialogue(int npcId, string npcDisplayName, Sprite portrait)
    {
        currentNpcId = npcId;
        currentNpcDisplayName = npcDisplayName ?? $"NPC({npcId})";
        currentNpcPortrait = portrait;

        IsOpen = true;
        stage = UiStage.DialogueNode;
        waitingForContinue = false;

        // 대화 그래프 진행 상태 초기화
        currentNode = null;

        // 시스템 문구 상태 초기화
        isShowingSystemLine = false;
        pendingReturnToMenuAfterSystemLine = false;

        if (view != null)
        {
            view.Open();
            view.SetPortrait(currentNpcPortrait);
            view.SetSpeakerName(currentNpcDisplayName);
            view.SetChoicePanel(false);
            view.SetContinue(false, "");
            view.ClearButtons();
        }

        NpcDialogueDef startNode = null;
        if (NPCDialogueManager.Instance != null && NPCDialogueManager.Instance.IsReady)
            startNode = NPCDialogueManager.Instance.StartDialogue(npcId);

        if (debugMode)
            Debug.Log($"[Flow] StartDialogue npcId={npcId}, hasStartNode={(startNode != null)}");

        if (startNode != null) ShowNode(startNode);
        else
        {
            if (view != null) view.SetDialogueInstant("...");
            ShowInteractionMenuNow(setPromptText: true);
        }
    }

    public void ForceClose()
    {
        IsOpen = false;
        stage = UiStage.None;
        waitingForContinue = false;
        childPopupOpen = false;

        currentNode = null;
        currentNpcId = -1;

        isShowingSystemLine = false;
        pendingReturnToMenuAfterSystemLine = false;

        NPCDialogueManager.Instance?.EndDialogue();
        view?.CloseAndClear();
    }

    public void OnContinueOrSkipPressed()
    {
        if (!IsOpen) return;
        if (childPopupOpen) return;
        if (view == null) return;

        // 타이핑 중이면 스킵
        if (view.IsTyping)
        {
            view.SkipTyping();
            return;
        }

        if (isShowingSystemLine)
            return;

        if (!waitingForContinue)
            return;

        waitingForContinue = false;
        view.SetContinue(false, "");

        // 링크-only 핵심: 항상 AdvanceDialogue() 시도
        var next = (NPCDialogueManager.Instance != null) ? NPCDialogueManager.Instance.AdvanceDialogue() : null;

        if (next != null)
        {
            ShowNode(next);
            return;
        }

        ShowInteractionMenuNow(setPromptText: true);
    }

    /// <summary>
    /// 외부(facade)에서 "지금 당장 메뉴로"가 필요할 때 호출.
    /// </summary>
    public void ShowInteractionMenuNow(bool setPromptText)
    {
        ShowInteractionMenuInternal(setPromptText);
    }

    /// <summary>
    /// nodeId로 시스템 문구 노드를 찾아 표시.
    /// - 그래프 진행(currentNode)과 분리된 1회성 문구 출력용
    /// </summary>
    public void ShowSystemNodeLine(int nodeId)
    {
        if (!IsOpen || view == null) return;

        if (nodeId <= 0)
        {
            ShowInteractionMenuNow(setPromptText: false);
            return;
        }

        var mgr = NPCDialogueManager.Instance;
        NpcDialogueDef node = (mgr != null) ? mgr.GetNodeById(nodeId) : null;

        if (node == null)
        {
            ShowInteractionMenuNow(setPromptText: false);
            return;
        }

        ShowSystemTextInternal(node.dialogueText, false);
    }

    /// <summary>
    /// 텍스트를 시스템 문구로 표시하고(타이핑 적용),
    /// 완료 시 메뉴로 복귀(returnToMenuAfter=true)시키는 용도.
    /// </summary>
    public void ShowSystemTextThenReturnToMenu(string text)
    {
        string preview = text ?? "(null)";
        if (preview.Length > 50)
            preview = preview.Substring(0, 50) + "...";
        Debug.Log($"[Flow] 출력: {preview}");

        ShowSystemTextInternal(text, true);
    }

    private void ShowSystemTextInternal(string text, bool returnToMenuAfter)
    {
        if (!IsOpen || view == null) return;

        stage = UiStage.DialogueNode;
        waitingForContinue = false;

        view.ClearButtons();
        view.SetChoicePanel(false);
        view.SetSpeakerName(currentNpcDisplayName);

        if (string.IsNullOrEmpty(text))
        {
            pendingReturnToMenuAfterSystemLine = returnToMenuAfter;
            StartCoroutine(AutoAdvanceAfterEmpty(returnToMenuAfter));
            return;
        }

        var mgr = NPCDialogueManager.Instance;
        var currentNode = mgr?.GetCurrentNode();

        // 라우터면 즉시 다음 노드 진행 (대사 없이!)
        if (currentNode?.IsRouterNode == true)
        {
            Debug.Log("[시스템 라우터] 즉시 AdvanceDialogue");
            var next = mgr.AdvanceDialogue();
            if (next != null)
            {
                ShowNode(next);  // 다음 실제 노드 표시
                return;
            }
        }

        // 비라우터: CONTINUE 버튼으로 대사 표시
        isShowingSystemLine = true;
        pendingReturnToMenuAfterSystemLine = returnToMenuAfter;
        view.SetContinue(true, continueLabel);
        view.PlayDialogue(text);
    }


    private System.Collections.IEnumerator AutoAdvanceAfterEmpty(bool returnToMenuAfter)
    {
        yield return new WaitForSeconds(0.3f);

        if (!NPCDialogueManager.Instance?.IsReady ?? true)  // 준비 확인 추가
        {
            if (returnToMenuAfter) ShowInteractionMenuNow(true);
            yield break;
        }

        var next = NPCDialogueManager.Instance.AdvanceDialogue();
        if (next != null)
        {
            ShowNode(next);
        }
        else if (returnToMenuAfter)
        {
            ShowInteractionMenuNow(true);
        }
    }


    private void ShowNode(NpcDialogueDef node)
    {
        if (node == null)
        {
            Debug.LogWarning($"[Flow] null 노드 → 메뉴");
            StartCoroutine(DelayedMenu(1f));  // 즉시 메뉴 아닌 지연
            return;
        }

        var mgr = NPCDialogueManager.Instance;
        if (mgr == null)
        {
            ShowInteractionMenuNow(false);
            return;
        }

        // [핵심] Manager에 현재 노드 전달
        mgr.currentNode = node;  // ← 이 한 줄!

        Debug.Log($"[Flow] ShowNode {node.nodeId} to Manager");

        // 라우터 처리
        if (node.IsRouterNode)
        {
            var nextNode = mgr.AdvanceDialogue();
            ShowNode(nextNode);  // 재귀
            return;
        }

        stage = UiStage.DialogueNode;
        currentNode = node;

        waitingForContinue = false;

        // 시스템 문구 모드 해제
        isShowingSystemLine = false;
        pendingReturnToMenuAfterSystemLine = false;

        if (view == null) return;

        view.ClearButtons();
        view.SetChoicePanel(false);
        view.SetContinue(false, "");

        if (node.speaker == "player")
        {
            view.SetSpeakerName(menu != null ? menu.PlayerDisplayName : "나");
        }
        else
        {
            view.SetSpeakerName(currentNpcDisplayName);
        }

        view.PlayDialogue(node.dialogueText);
    }
    private IEnumerator DelayedMenu(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowInteractionMenuNow(false);  // setPromptText=false로 최소 메뉴
    }


    public void RestartEntryNode(int npcId, string eventType = "")
    {
        if (currentNpcId != npcId) return;

        StartDialogueInternal(npcId, currentNpcDisplayName, currentNpcPortrait, eventType);
    }

    private void StartDialogueInternal(int npcId, string npcDisplayName, Sprite portrait, string eventKey = "")
    {
        Debug.Log($"[DEBUG_FLOW] StartInternal: npcId={npcId}, event='{eventKey}', aff=?");

        currentNpcId = npcId;
        currentNpcDisplayName = npcDisplayName ?? $"NPC{npcId}";
        currentNpcPortrait = portrait;

        IsOpen = true;
        stage = UiStage.DialogueNode;
        waitingForContinue = false;
        childPopupOpen = false;
        currentNode = null;
        isShowingSystemLine = false;
        pendingReturnToMenuAfterSystemLine = false;

        if (view != null)
        {
            view.Open();
            view.SetPortrait(currentNpcPortrait);
            view.SetSpeakerName(currentNpcDisplayName);
            view.SetChoicePanel(false);
            view.SetContinue(false, "");
            view.ClearButtons();
        }

        NpcDialogueDef startNode = null;
        if (NPCDialogueManager.Instance?.IsReady == true)
        {
            // [수정] 직접 dialogueDb 사용
            var mgr = NPCDialogueManager.Instance;
            int aff = mgr.GetCurrentAffectionStage(npcId);
            string time = mgr.GetCurrentTimeOfDay();
            string weather = mgr.GetCurrentWeather();

            startNode = mgr.DialogueDb.GetEntryNode(npcId, aff, time, weather, eventKey);
        }

        if (!string.IsNullOrEmpty(eventKey))
        {
            NPCDialogueManager.Instance.SetEventContext(eventKey);
        }

        if (startNode != null)
        {
            ShowNode(startNode);
        }
        else
        {
            ShowInteractionMenuNow(true);
        }
    }

    private void OnTypingCompleted()
    {
        if (!IsOpen) return;
        if (view == null) return;

        // 시스템 문구: 완료 시 메뉴로
        if (isShowingSystemLine)
        {
            bool backToMenu = pendingReturnToMenuAfterSystemLine;

            isShowingSystemLine = false;
            pendingReturnToMenuAfterSystemLine = false;

            if (NPCDialogueManager.Instance != null &&
            NPCDialogueManager.Instance.GetCurrentNode()?.IsRouterNode == true)
            {
                Debug.Log("[시스템] 라우터 감지 → AdvanceDialogue");
                var next = NPCDialogueManager.Instance.AdvanceDialogue();
                if (next != null)
                {
                    ShowNode(next);
                    return;
                }
            }

            if (backToMenu)
                ShowInteractionMenuNow(setPromptText: false);

            return;
        }

        // 안전장치: currentNode가 없으면 메뉴
        if (currentNode == null)
        {
            ShowInteractionMenuNow(setPromptText: true);
            return;
        }

        // 선택지 노드면 선택지 표시
        if (currentNode.choiceGroupId > 0)
        {
            ShowCsvChoices(currentNode.choiceGroupId);
        }
        else
        {
            // 일반 노드도 라우터 체크
            waitingForContinue = true;
            view.SetContinue(true, continueLabel);
        }

        // 링크-only: 계속 항상 표시 (눌렀을 때 AdvanceDialogue 결과로 진행)
        waitingForContinue = true;
        view.SetContinue(true, continueLabel);

        if (currentNode == null && !waitingForContinue)
        {
            StartCoroutine(DelayedMenu(0.5f));  // 연속 null 방지
            return;
        }
    }

    private void ShowCsvChoices(int choiceGroupId)
    {
        stage = UiStage.CsvChoices;
        waitingForContinue = false;

        if (view == null) return;

        view.ClearButtons();
        view.SetChoicePanel(true);
        view.SetContinue(false, "");

        var mgr = NPCDialogueManager.Instance;
        var choices = (mgr != null) ? mgr.GetChoices(choiceGroupId) : new List<NpcDialogueDef>();

        if (choices == null || choices.Count == 0)
        {
            ShowInteractionMenuNow(setPromptText: true);
            return;
        }

        foreach (var choiceNode in choices)
        {
            string label = string.IsNullOrEmpty(choiceNode.choiceText) ? "(선택)" : choiceNode.choiceText;

            view.SpawnButton(label, (btn, txt) =>
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (childPopupOpen) return;

                    view.ClearButtons();

                    var next = mgr.SelectChoice(choiceNode.nodeId);
                    if (next != null) ShowNode(next);
                    else ShowInteractionMenuNow(setPromptText: true);
                });
            });
        }
    }

    private void ShowInteractionMenuInternal(bool setPromptText)
    {
        stage = UiStage.InteractionMenu;
        waitingForContinue = false;

        // 그래프/시스템 상태 모두 해제
        currentNode = null;
        isShowingSystemLine = false;
        pendingReturnToMenuAfterSystemLine = false;

        if (view == null || menu == null) return;

        view.ClearButtons();
        view.SetChoicePanel(true);
        view.SetContinue(false, "");
        view.SetSpeakerName(currentNpcDisplayName);

        childPopupOpen = false;

        menu.BuildMenu(view, currentNpcId, childPopupOpen, setPromptText);
    } 
}
