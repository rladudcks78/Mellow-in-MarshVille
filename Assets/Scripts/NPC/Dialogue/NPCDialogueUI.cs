using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Facade(기존 API 유지):
/// - NPCInteractionCsv가 참조하는 API/이벤트 유지
/// - MenuController의 SystemLineRequested(systemKey)를 받아 CSV에서 system 노드를 찾아 Flow로 표시
/// - 선물 결과 문구도 Flow의 "시스템 문구 출력 루틴"으로 통일(타이핑 완료 후 자동 메뉴 복귀)
/// </summary>
public class NPCDialogueUI : MonoBehaviour
{
    public event Action<int> QuestRequested;
    public event Action<int> ShopRequested;
    public event Action<int> GiftRequested;
    public event Action<int> LeftNpc;

    [Header("Refs")]
    [SerializeField] private NPCDialogueView view;
    [SerializeField] private NPCDialogueFlowController flow;
    [SerializeField] private NPCInteractionMenuController menu;

    [Header("닫기 옵션")]
    [SerializeField] private bool closeOnOutsideClick = true;
    [SerializeField] private bool closeOnCKey = true;

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;

    private bool childPopupOpen = false;

    private Canvas rootCanvas;
    private Camera uiCamera;

    private int currentNpcId = -1;

    public bool IsOpen => flow != null && flow.IsOpen;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        if (view == null) view = GetComponentInChildren<NPCDialogueView>(true);
        if (flow == null) flow = GetComponentInChildren<NPCDialogueFlowController>(true);
        if (menu == null) menu = GetComponentInChildren<NPCInteractionMenuController>(true);

        if (menu != null)
        {
            menu.QuestRequested += id => QuestRequested?.Invoke(id);
            menu.ShopRequested += id => ShopRequested?.Invoke(id);
            menu.GiftRequested += id => GiftRequested?.Invoke(id);
            menu.LeftNpc += id => LeaveByUser();

            // systemKey 요청(선물 이미 줌/선물 선택 안내/상점 안내/퀘스트 안내 등)
            menu.SystemLineRequested += OnSystemLineRequested;
        }

        view?.CloseAndClear();
    }

    private void OnDestroy()
    {
        if (menu != null)
            menu.SystemLineRequested -= OnSystemLineRequested;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 자식 팝업(퀘스트/상점/선물 UI)이 열려있으면 입력 잠금
        if (childPopupOpen)
            return;

        if (closeOnCKey && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            LeaveByUser();
            return;
        }

        if (closeOnOutsideClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (view != null && view.WindowRect != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                bool inside = RectTransformUtility.RectangleContainsScreenPoint(view.WindowRect, mousePos, uiCamera);
                if (!inside)
                {
                    LeaveByUser();
                    return;
                }
            }
        }

        bool pressed =
            (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (pressed)
            flow?.OnContinueOrSkipPressed();
    }

    // =========================================================
    // 외부 API 유지
    // =========================================================

    public void StartDialogue(int npcId)
    {
        currentNpcId = npcId;
        childPopupOpen = false;

        string displayName = $"NPC({npcId})";
        Sprite portrait = null;

        var loader = NpcLoader.Instance;
        if (loader != null && loader.IsLoaded && loader.NpcDb != null &&
            loader.NpcDb.TryGet(npcId, out var def) && def != null)
        {
            displayName = def.npcDisplayName;
            portrait = def.portrait;
        }

        if (debugMode)
            Debug.Log($"[NPCDialogueUI] StartDialogue npcId={npcId}, name={displayName}");

        flow?.SetChildPopupOpen(false);
        flow?.StartDialogue(npcId, displayName, portrait);
    }

    public void SetChildPopupOpen(bool open)
    {
        childPopupOpen = open;
        flow?.SetChildPopupOpen(open);
    }

    public void RestartEntryNodeAfterPopup(int npcId, string eventType = "")
    {
        flow?.RestartEntryNode(npcId, eventType);
    }

    public void Close()
    {
        if (currentNpcId > 0)
            LeftNpc?.Invoke(currentNpcId);

        childPopupOpen = false;
        currentNpcId = -1;

        flow?.ForceClose();
    }

    private void LeaveByUser()
    {
        if (childPopupOpen) return;

        if (currentNpcId > 0)
            LeftNpc?.Invoke(currentNpcId);

        childPopupOpen = false;
        currentNpcId = -1;

        flow?.ForceClose();
    }

    // =========================================================
    // systemKey -> CSV node -> Flow 표시
    // =========================================================

    private void OnSystemLineRequested(string systemKey)
    {
        Debug.Log($"[SystemLine] key='{systemKey}'");

        if (!IsOpen) return;
        if (string.IsNullOrWhiteSpace(systemKey)) return;

        var mgr = NPCDialogueManager.Instance;
        if (mgr == null || !mgr.IsReady) return;

        var node = mgr.GetSystemNode(currentNpcId, systemKey);

        if (node != null)
        {
            flow?.ShowSystemTextThenReturnToMenu(node.dialogueText);
        }
        else
        {
            Debug.LogWarning($"[시스템 null] {systemKey}");
            flow?.ShowInteractionMenuNow(false);  // 프롬프트 없이 메뉴
        }
    }


    // =========================================================
    // 선물 결과 출력(API 유지) - Flow로 통일
    // =========================================================

    public void ShowGiftResultLine(int npcId, GiftTaste taste, int delta)
    {
        if (flow == null || menu == null) return;

        // 커스텀 반응(선물 반응 CSV/DB)
        if (menu.TryGetGiftReactionLine(npcId, taste, out var custom) && !string.IsNullOrEmpty(custom))
        {
            flow.ShowSystemTextThenReturnToMenu($"{custom} (호감도 {delta:+#;-#;0})");
            return;
        }

        // fallback 공용 문구
        string msg = taste switch
        {
            GiftTaste.Like => $"정말 좋아해요! (호감도 {delta:+#;-#;0})",
            GiftTaste.Dislike => $"별로 좋아하지 않아요... (호감도 {delta:+#;-#;0})",
            GiftTaste.NoThanks => $"음... (호감도 {delta:+#;-#;0})",
            _ => $"고마워요! (호감도 {delta:+#;-#;0})"
        };

        flow.ShowSystemTextThenReturnToMenu(msg);
    }

    public void ShowSystemLine(string line)
    {
        // 기존 API도 Flow로 통일
        flow?.ShowSystemTextThenReturnToMenu(line);
    }
}
