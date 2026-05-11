using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GiftInventoryController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private NPCDialogueUI dialogueUI;

    [Header("Gift UI")]
    [Tooltip("Gift 모드에서만 표시됩니다.")]
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject gard;

    private bool active;
    private int targetNpcId = -1;

    // Gift 모드 진입 직후 UI 갱신용
    private Coroutine applyRoutine;

    public event System.Action OnGiftSessionEnded;

    private void Awake()
    {
        if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (inventorySystem == null) inventorySystem = FindAnyObjectByType<InventorySystem>();
        if (dialogueUI == null) dialogueUI = FindAnyObjectByType<NPCDialogueUI>();

        BindCloseButton();

        if (closeButton != null) closeButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SlotLeftClickInterceptor += InterceptLeftClick;
            inventoryUI.OnOpened += OnInventoryOpened;
            inventoryUI.OnClosed += OnInventoryClosed;
        }

        if (dialogueUI != null)
        {
            dialogueUI.LeftNpc += OnLeftNpc;
        }

        BindCloseButton();
    }

    private void OnDisable()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SlotLeftClickInterceptor -= InterceptLeftClick;
            inventoryUI.OnOpened -= OnInventoryOpened;
            inventoryUI.OnClosed -= OnInventoryClosed;
        }

        if (dialogueUI != null)
        {
            dialogueUI.LeftNpc -= OnLeftNpc;
        }

        if (closeButton != null) closeButton.onClick.RemoveListener(CancelGift);

        if (applyRoutine != null)
        {
            StopCoroutine(applyRoutine);
            applyRoutine = null;
        }
    }

    private void BindCloseButton()
    {
        if (closeButton == null) return;

        closeButton.onClick.RemoveListener(CancelGift);
        closeButton.onClick.AddListener(CancelGift);
    }

    public void BeginGiftSession(int npcId)
    {
        if (npcId <= 0) return;
        if (inventoryUI == null || inventorySystem == null) return;

        if (!inventorySystem.IsReady) return;
    
        active = true;
        targetNpcId = npcId;

        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (gard != null) gard.gameObject.SetActive(true);

        dialogueUI?.SetChildPopupOpen(true);

        inventoryUI.Open(InventoryUI.LayoutMode.Gift);

        // 바로 적용하지 않고 1프레임 뒤 적용(레이아웃/슬롯/그래픽 갱신 타이밍 보장)
        if (applyRoutine != null) StopCoroutine(applyRoutine); 
        applyRoutine = StartCoroutine(CoApplyFilterNextFrame());
    }

    private IEnumerator CoApplyFilterNextFrame()
    {
        yield return null; // 다음 프레임까지 대기(캔버스가 프레임 끝에 갱신되기 때문)

        if (!active) yield break;
        ApplyFilterAndBlock();
        Canvas.ForceUpdateCanvases();
    }

    private void EndGiftSession(bool closeInventory)
    {
        if (!active) return;

        active = false;
        targetNpcId = -1;

        inventoryUI?.ClearAllSlotBlocks();
        dialogueUI?.SetChildPopupOpen(false);

        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (gard != null) gard.gameObject.SetActive(false);

        if (applyRoutine != null)
        {
            StopCoroutine(applyRoutine);
            applyRoutine = null;
        }

        if (closeInventory && inventoryUI != null && inventoryUI.isOpen)
            inventoryUI.Close();

        OnGiftSessionEnded?.Invoke();
    }

    public void CancelGift()
    {
        if (!active) return;
        EndGiftSession(closeInventory: true);
    }

    private void OnLeftNpc(int npcId)
    {
        EndGiftSession(closeInventory: true);
    }

    private void OnInventoryOpened()
    {
        if (!active) return;

        // 열림 이벤트에서도 동일하게 1프레임 뒤 적용(안정)
        if (applyRoutine != null) StopCoroutine(applyRoutine);
        applyRoutine = StartCoroutine(CoApplyFilterNextFrame());
    }

    private void OnInventoryClosed()
    {
        if (!active) return;

        active = false;
        targetNpcId = -1;
        inventoryUI?.ClearAllSlotBlocks();
        dialogueUI?.SetChildPopupOpen(false);

        if (closeButton != null) closeButton.gameObject.SetActive(false);

        if (applyRoutine != null)
        {
            StopCoroutine(applyRoutine);
            applyRoutine = null;
        }
    }

    private bool InterceptLeftClick(int slotIndex)
    {
        if (!active) return false;
        
        if (GiftManager.Instance == null) return true;
        
        bool ok = GiftManager.Instance.TryGiftFromInventorySlot(targetNpcId, slotIndex);

        if (ok)
        {
            EndGiftSession(closeInventory: true);
        }
        else
        {
            ApplyFilterAndBlock();

            // 실패 후 즉시 반영
            Canvas.ForceUpdateCanvases();
        }

        return true;
    }

    private void ApplyFilterAndBlock()
    {
        if (inventoryUI == null || inventorySystem == null || !inventorySystem.IsReady) return;

        inventoryUI.ClearAllSlotBlocks();

        int n = inventoryUI.SlotCount;
        for (int i = 0; i < n; i++)
        {
            var stack = inventorySystem.Inventory.Get(i);

            if (stack.IsEmpty)
            {
                inventoryUI.SetSlotBlocked(i, true);
                continue;
            }

            int itemId = stack.itemId;
            bool giftable = IsGiftableByIdRule(itemId);

            inventoryUI.SetSlotBlocked(i, !giftable);
        }
    }

    private bool IsGiftableByIdRule(int itemId)
    {
        int group = itemId / 10000;
        if (group == 0)
        {
            int k = itemId / 1000;
            if (k == 1) group = 1;
            else if (k == 2) group = 2;
            else if (k == 3) group = 3;
            else if (k == 5) group = 5;
        }
        return group == 1 || group == 2 || group == 3 || group == 5;
    }
}
