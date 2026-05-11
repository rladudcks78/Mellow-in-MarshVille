using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// NPC 클릭 상호작용 진입점.
/// - 역할: "NPC가 클릭됐고, 플레이어가 범위 안"인지 확인한 뒤 UI를 연다.
/// - 퀘스트/상점/선물/인사는 대화 UI의 "상호작용 선택지"에서 처리한다.
/// </summary>
public class NPCInteractionCsv : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private int npcId = 0;
    [Tooltip("미지정(0)이면 NpcDef.interactionRange를 사용합니다.")]
    [SerializeField] private float interactionRange = 0f;

    [Header("UI (대화)")]
    [Tooltip("Canvas 안의 NPCDialogueUI를 연결하세요. 미연결 시 Start에서 1회 자동 탐색합니다.")]
    [SerializeField] private NPCDialogueUI dialogueUI;

    [Header("UI (퀘스트 팝업)")]
    [SerializeField] private QuestPopupUI questPopupUI;


    [Header("UI (상점/선물)")]
    [SerializeField] private GameObject shopPopupRoot;   // 레거시/폴백용
    [SerializeField] private GameObject giftPopupRoot;
    [SerializeField] private GiftInventoryController giftInventoryController;

    [Header("Shop 연결 (실사용)")]
    [SerializeField] private UIInteract uiInteract;
    [SerializeField] private ShopUIController shopUIController;

    // 팝업 상태 감시용(닫힘 이벤트가 아직 없어서 임시)
    private bool shopWasOpen = false;
    private bool giftWasOpen = false;

    // 클릭/거리 판정용
    private int playerLayer;
    private LayerMask playerLayerMask;
    private Camera mainCamera;

    [SerializeField] private TimeManager timeManager;

    private void Start()
    {
        // Player 레이어 마스크 준비
        playerLayerMask = LayerMask.GetMask("Player");
        if (playerLayerMask == 0)
        {
            Debug.LogWarning("[NPCQuestInteractionCsv] 'Player' 레이어를 찾지 못했습니다.");
        }

        // 메인 카메라 캐시
        mainCamera = Camera.main;

        if (timeManager == null)
            timeManager = TimeManager.Instance != null ? TimeManager.Instance : FindAnyObjectByType<TimeManager>();

        if (uiInteract == null)
            uiInteract = FindAnyObjectByType<UIInteract>();

        if (shopUIController == null)
            shopUIController = FindAnyObjectByType<ShopUIController>();

        // NpcDef에서 interactionRange를 가져올 수 있으면 적용(데이터와 프리팹 값 불일치 방지)
        TryApplyInteractionRangeFromNpcDef();

        // 대화 UI 1회 자동 탐색
        if (dialogueUI == null)
            dialogueUI = FindAnyObjectByType<NPCDialogueUI>();

        if (questPopupUI == null)
            questPopupUI = FindAnyObjectByType<QuestPopupUI>();

        // 대화 UI 이벤트 구독(선택지로부터 "퀘스트/상점/선물" 요청이 들어오면 여기서 처리)
        if (dialogueUI != null)
        {
            dialogueUI.QuestRequested -= OnQuestRequested;
            dialogueUI.QuestRequested += OnQuestRequested;

            dialogueUI.ShopRequested -= OnShopRequested;
            dialogueUI.ShopRequested += OnShopRequested;

            dialogueUI.GiftRequested -= OnGiftRequested;
            dialogueUI.GiftRequested += OnGiftRequested;

            dialogueUI.LeftNpc -= OnLeftNpc;
            dialogueUI.LeftNpc += OnLeftNpc;
        }

        if (questPopupUI != null)
        {
            questPopupUI.Closed -= OnQuestPopupClosed;
            questPopupUI.Closed += OnQuestPopupClosed;
        }

        if (giftInventoryController != null)
        {
            // GiftInventoryController.EndGiftSession 이벤트 바인딩
            giftInventoryController.OnGiftSessionEnded += () => OnGiftPopupClosed();
        }
        // 초기 팝업 상태 캐시
        shopWasOpen = IsShopPopupActuallyOpen();
        giftWasOpen = IsGiftPopupActuallyOpen();
    }

    private void OnDestroy()
    {
        // 구독 해제(씬 전환/파괴 시 메모리/참조 문제 방지)
        if (dialogueUI != null)
        {
            dialogueUI.QuestRequested -= OnQuestRequested;
            dialogueUI.ShopRequested -= OnShopRequested;
            dialogueUI.GiftRequested -= OnGiftRequested;
            dialogueUI.LeftNpc -= OnLeftNpc;
        }

        if (questPopupUI != null)
        {
            questPopupUI.Closed -= OnQuestPopupClosed;
        }
    }

    private void Update()
    {
        // 팝업 상태 폴링 (상점/선물 닫힘 감지)
        PollChildPopupStates();

        if (Mouse.current == null) return;

        // UI 위 클릭이면 NPC 클릭으로 처리하지 않음
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // 대화 UI가 이미 열려 있으면, 다른 NPC 클릭으로 대화가 덮어써지는 것 막기
        if (dialogueUI != null && dialogueUI.IsOpen)
            return;

        CheckMouseClick();
    }

    private void CheckMouseClick()
    {
        if (mainCamera == null) return;

        // 1. 마우스 위치를 월드 좌표로 변환합니다.
        Vector2 mousePos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        // 2. 마우스 클릭 위치에 있는 오브젝트를 감지합니다.
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        // 클릭된 게 없거나, 이 스크립트가 붙은 NPC가 아니면 무시합니다.
        if (hit.collider == null) return;
        if (hit.collider.gameObject != gameObject) return;

        // --- 거리 판정 로직 수정 시작 ---

        // 3. NPC의 중심점(발밑이 아닌 콜라이더의 중앙)을 가져옵니다.
        Vector2 npcCenter = hit.collider.bounds.center;

        // 4. NPC 중심점을 기준으로 주변에 플레이어(Player 레이어)가 있는지 검사합니다.
        Collider2D playerCollider = Physics2D.OverlapCircle(npcCenter, interactionRange, playerLayerMask);

        if (playerCollider == null)
        {
            Debug.Log($"너무 멀어요! 현재 설정 사거리: {interactionRange}");
            return;
        }

        // --- 거리 판정 로직 수정 끝 ---

        OnInteract();
    }

    private void TryApplyInteractionRangeFromNpcDef()
    {
        // 인스펙터에서 값을 넣어두면(>0) 그 값을 사용, 비어있으면(<=0) DB 값을 적용
        if (interactionRange > 0f) return;

        if (NpcLoader.Instance == null) return;
        if (!NpcLoader.Instance.IsLoaded) return;
        if (NpcLoader.Instance.NpcDb == null) return;

        if (NpcLoader.Instance.NpcDb.TryGet(npcId, out var def) && def != null)
        {
            if (def.interactionRange > 0f)
            {
                interactionRange = def.interactionRange;
            }
            else
            {
                interactionRange = 2f;
            }
        }
    }

    private void OnInteract()
    {
        // NPC 클릭 진입 시 호감도 UI 표시
        RelationshipManager.Instance?.ShowAffectionUI(npcId);

        // 대화 UI 열기(이 안에서: 인사말(노드) → 마지막에 상호작용 메뉴)
        if (dialogueUI == null)
        {
            Debug.LogWarning("[NPCQuestInteractionCsv] NPCDialogueUI가 씬에 없습니다.");
            return;
        }

        dialogueUI.StartDialogue(npcId);

        if (timeManager != null)
            timeManager.IsTimeStopped = true;
    }

    // =========================================================
    // DialogueUI -> 요청 이벤트 처리 (팝업 연결 지점)
    // =========================================================

    private void OnQuestRequested(int requestedNpcId)
    {
        // 여러 NPC가 같은 UI를 공유하므로, 요청이 내 npcId에서 왔는지 확인
        if (requestedNpcId != npcId) return;

        if (questPopupUI == null)
        {
            Debug.LogWarning("[NPCQuestInteractionCsv] QuestPopupUI가 씬에 없습니다.");
            return;
        }

        // 모달 ON: 퀘스트 팝업 열려 있는 동안 바깥 클릭/C키로 대화창 닫히지 않게
        dialogueUI?.SetChildPopupOpen(true);

        // 팝업 UI 오픈
        questPopupUI.OpenForNpc(npcId);
    }

    private void OnQuestPopupClosed()
    {
        // 모달 OFF
        dialogueUI?.SetChildPopupOpen(false);
        dialogueUI?.RestartEntryNodeAfterPopup(npcId, "quest");
    }

    private void OnShopRequested(int requestedNpcId)
    {
        if (requestedNpcId != npcId) return;

        // 상점 세션 동안 대화 UI가 바깥 클릭/C키로 닫히지 않게
        dialogueUI?.SetChildPopupOpen(true);

        bool opened = false;

        if (uiInteract == null)
            uiInteract = FindAnyObjectByType<UIInteract>();

        if (shopUIController == null)
            shopUIController = FindAnyObjectByType<ShopUIController>();

        // 1순위: UIInteract 경유 (인벤토리 동시 오픈/닫기까지 같이 처리)
        if (uiInteract != null)
        {
            opened = uiInteract.OpenShopForNpc(npcId);
        }
        // 2순위 폴백: ShopUIController 직접 오픈 (권장 X, 임시)
        else if (shopUIController != null)
        {
            opened = shopUIController.OpenForNpc(npcId);
        }
        // 3순위 레거시 폴백
        else if (shopPopupRoot != null)
        {
            shopPopupRoot.SetActive(true);

            // 감시 상태 업데이트
            shopWasOpen = true;
            opened = true;
        }

        if (!opened)
        {
            Debug.LogWarning($"[NPCInteractionCsv] 상점 열기 실패: npcId={npcId}");
            dialogueUI?.SetChildPopupOpen(false);
            shopWasOpen = false;
            return;
        }

        shopWasOpen = IsShopPopupActuallyOpen();
    }

    private void OnGiftRequested(int requestedNpcId)
    {
        if (requestedNpcId != npcId) return;

        // 선물 세션 동안 대화 UI가 바깥 클릭/C키로 닫히지 않게
        dialogueUI?.SetChildPopupOpen(true);

        if (GiftManager.Instance != null)
        {
            GiftManager.Instance.BeginGiftSessionForNpc(npcId);

            // giftPopupRoot를 실제 UI 루트로 쓴다면 여기서 켤 수 있게(현재 구조가 GiftManager가 UI를 여는지 불명이라 안전하게 조건부)
            if (giftPopupRoot != null)
            {
                giftPopupRoot.SetActive(true);
                giftWasOpen = true;
            }

            // giftPopupRoot를 실제 UI 루트로 쓴다면 여기서 켤 수 있게
            if (giftPopupRoot != null)
            {
                giftPopupRoot.SetActive(true);
                giftWasOpen = true;
            }
        }
        else
        {
            Debug.LogWarning("[NPCQuestInteractionCsv] GiftManager가 씬에 없습니다.");

            // 선물 시스템이 없으면 모달을 풀어준다
            dialogueUI?.SetChildPopupOpen(false);
        }
    }

    // 나중에 닫기 버튼에서 이 함수를 직접 호출하도록 연결하면 폴링이 필요 없어짐
    public void OnShopPopupClosed()
    {
        dialogueUI?.SetChildPopupOpen(false);
        shopWasOpen = false;
    }

    // 나중에 닫기 버튼에서 이 함수를 직접 호출하도록 연결하면 폴링이 필요 없어짐
    public void OnGiftPopupClosed()
    {
        dialogueUI?.SetChildPopupOpen(false);
        if (dialogueUI != null)
        {
            dialogueUI.StartDialogue(npcId);  // npcId 사용
        }
        giftWasOpen = false;
    }

    private void OnLeftNpc(int requestedNpcId)
    {
        if (requestedNpcId != npcId) return;

        if (timeManager != null)
            timeManager.IsTimeStopped = false;

        // 떠나기 시 호감도 UI 숨김
        RelationshipManager.Instance?.HideAffectionUI();

        // 실제 상점 UI가 열려있으면 같이 닫기
        if (uiInteract == null)
            uiInteract = FindAnyObjectByType<UIInteract>();

        if (uiInteract != null)
        {
            uiInteract.CloseShop();
        }
        else if (shopUIController != null && shopUIController.isOpen)
        {
            shopUIController.CloseShop();
        }

        // 레거시 루트도 정리
        if (shopPopupRoot != null) shopPopupRoot.SetActive(false);

        dialogueUI?.SetChildPopupOpen(false);

        shopWasOpen = false;
        giftWasOpen = false;
    }

    private bool IsShopPopupActuallyOpen()
    {
        if (shopUIController != null)
            return shopUIController.isOpen;

        if (shopPopupRoot != null)
            return shopPopupRoot.activeSelf;

        return false;
    }

    private bool IsGiftPopupActuallyOpen()
    {
        return giftPopupRoot != null && giftPopupRoot.activeSelf;
    }

    private void PollChildPopupStates()
    {
        bool shopNowOpen = IsShopPopupActuallyOpen();
        if (shopWasOpen && !shopNowOpen)
        {
            OnShopPopupClosed();
        }
        shopWasOpen = shopNowOpen;

        bool giftNowOpen = IsGiftPopupActuallyOpen();
        if (giftWasOpen && !giftNowOpen)
        {
            OnGiftPopupClosed();
        }
        giftWasOpen = giftNowOpen;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
