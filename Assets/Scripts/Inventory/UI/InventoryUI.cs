using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventorySystem inv;
    [SerializeField] private SpriteResolver spriteResolver;
    [SerializeField] private TooltipUI tooltip;
    [SerializeField] private HeldItemUI hasHeld;
    [SerializeField] private ConfirmPopupUI confirmPopup;
    [SerializeField] private CookingUI cooking;

    [Header("Build")]
    [SerializeField] private Transform gridParent;
    [SerializeField] private SlotUI slotPrefab;

    [Header("Open/Close")]
    [SerializeField] private GameObject root;

    [Header("Overlay")]
    [SerializeField] private GameObject overlay;

    [Header("Layout")]
    [SerializeField] private RectTransform inventoryBackground;

    [Header("Layout Presets")]
    [SerializeField] private Vector2 normalPos = new Vector2(0f, 0f);
    [SerializeField] private Vector2 normalSize = new Vector2(1130f, 470f);

    [SerializeField] private Vector2 withStoragePos = new Vector2(-305f, -27f);
    [SerializeField] private Vector2 withStorageSize = new Vector2(1130f, 470f);

    [SerializeField] private Vector2 cookingPos = new Vector2(-580f, 330f);
    [SerializeField] private Vector2 cookingSize = new Vector2(620f, 250f);

    [SerializeField] private Vector2 giftPos = new Vector2(0f, 150f);
    [SerializeField] private Vector2 giftSize = new Vector2(1130f, 470f);

    [SerializeField] private Vector2 shopPos = new Vector2(450f, 125f);
    [SerializeField] private Vector2 shopSize = new Vector2(700f, 650f);

    [Header("Grid")]
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private int columnsNormal = 10;
    [SerializeField] private int columnsCooking = 5;
    [SerializeField] private int columnsGift = 10;
    [SerializeField] private int columnsShop = 5;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    public enum LayoutMode
    {
        Normal,
        WithStorage,
        Cooking,
        Gift,
        Shop
    }
    private LayoutMode currentLayout = LayoutMode.Normal;

    private SlotUI[] slots;
    private Vector2 lastPointerPos;
    private bool rightDragActive;
    private HashSet<int> rightDragVisited = new HashSet<int>();

    public bool isOpen => root != null && root.activeSelf;
    public int hoveredIndex { get; private set; } = -1;

    // [ADD] 외부 컨트롤러 훅
    // index -> true면 처리 완료(기본 인벤 로직 스킵)
    public System.Func<int, bool> SlotLeftClickInterceptor;

    public event System.Action OnOpened;
    public event System.Action OnClosed;

    public LayoutMode CurrentLayout => currentLayout;
    public InventorySystem InventorySystem => inv;
    public int SlotCount => (slots != null) ? slots.Length : 0;

    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);

        if (overlay != null) overlay.SetActive(false);

        if (grid == null && gridParent != null)
            grid = gridParent.GetComponent<GridLayoutGroup>();
    }

    private void Start()
    {
        StartCoroutine(WaitAndBuild());
    }

    private IEnumerator WaitAndBuild()
    {
        if (inv == null)
            inv = FindFirstObjectByType<InventorySystem>();
        if(inv == null)
        {
            Debug.LogError("[InventoryUI] InventorySystem을 찾지 못함. 인스펙터에 연결해야함.");
            yield break;
        }

        while (!inv.IsReady)
            yield return null;

        BuildSlots();

        inv.Inventory.OnSlotChanged += OnSlotChanged;
        inv.Inventory.OnRebuilt += OnRebuilt;

        ApplyLayout(currentLayout);
        RefreshAll();
    }

    private void Update()
    {
        if (root.activeSelf && hasHeld != null)
            hasHeld.TickFollowMouse();
    }

    private void OnDestroy()
    {
        if (inv != null && inv.Inventory != null)
        {
            inv.Inventory.OnSlotChanged -= OnSlotChanged;
            inv.Inventory.OnRebuilt -= OnRebuilt;
        }
    }

    private void BuildSlots()
    {
        int count = inv.Inventory.slotCount;
        slots = new SlotUI[count];

        //기존 자식 삭제
        for (int i = gridParent.childCount - 1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var s = Instantiate(slotPrefab, gridParent);
            s.Init(this, i);
            slots[i] = s;
        }
    }

    private void OnSlotChanged(int index)
    {
        RefreshOne(index);
    }

    private void OnRebuilt()
    {
        BuildSlots();
        RefreshAll();
        ApplyLayout(currentLayout);
    }

    private void RefreshAll()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
            RefreshOne(i);
    }

    private void RefreshOne(int index)
    {
        var stack = inv.Inventory.Get(index);
        if (stack.IsEmpty)
        {
            slots[index].Clear();
            return;
        }

        if (!inv.TryGetDef(stack.itemId, out var def))
        {
            slots[index].Clear();
            return;
        }

        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        slots[index].Set(sp, stack.amount);
    }

    // [ADD] 슬롯 차단(회색 + 클릭 막기)
    public void SetSlotBlocked(int index, bool blocked)
    {
        if (slots == null) return;
        if (index < 0 || index >= slots.Length) return;
        if (slots[index] == null) return;

        slots[index].SetBlocked(blocked);
    }

    public void ClearAllSlotBlocks()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].SetBlocked(false);
        }
    }

    private bool IsBlockedIndex(int index)
    {
        if (slots == null) return false;
        if (index < 0 || index >= slots.Length) return false;
        if (slots[index] == null) return false;
        return slots[index].IsBlocked;
    }

    public void OnSlotLeftClick(int index)
    {
        Debug.Log($"InventoryUI: {index}번 슬롯 왼쪽 클릭됨");

        if (IsBlockedIndex(index)) return;

        if (confirmPopup != null && confirmPopup.isOpen) return;

        // 외부가 처리하면 기본 로직 스킵
        if (SlotLeftClickInterceptor != null && SlotLeftClickInterceptor.Invoke(index))
            return;

        //아이템을 집으면 툴팁 끄기
        tooltip?.Hide();

        inv.OnSlotLeftClick(index);
        UpdateHeldUI();
    }

    public void OnSlotRightClick(int index)
    {
        if (IsBlockedIndex(index)) return;

        if (confirmPopup != null && confirmPopup.isOpen) return;

        //아이템을 집으면 툴팁 끄기
        tooltip?.Hide();

        inv.OnSlotRightClick(index);
        UpdateHeldUI();
    }

    public void BeginRightDrag()
    {
        if (!isOpen) return;
        if (inv == null || !inv.IsReady) return;
        if (!inv.hasHeld) return;

        rightDragActive = true;
        rightDragVisited.Clear();

        TryRightDragPlace(hoveredIndex);
    }

    public void EndRightDrag()
    {
        rightDragActive = false;
        rightDragVisited.Clear();
    }

    private void TryRightDragPlace(int index)
    {
        if (!rightDragActive) return;
        if (index < 0) return;
        if(!inv.hasHeld) { EndRightDrag(); return; }

        //드래그 중 같은 슬롯 중복 처리 방지
        if (!rightDragVisited.Add(index)) return;

        //기존 우클릭 1개 놓기
        OnSlotRightClick(index);

        //들고있는거 다 떨어지면 종료
        if(!inv.hasHeld) EndRightDrag();
    }

    public void OnClickOutside()
    {
        if (confirmPopup != null && confirmPopup.isOpen) return;

        //손 비었으면 닫기
        if (!inv.hasHeld)
        {
            Close();
            return;
        }

        confirmPopup.Show(
            "아이템을 버릴까요?",
            confirm: () =>
            {
                inv.DropHeld();
                UpdateHeldUI();
            },
            cancel: () =>
            {
                inv.ReturnHeldToOrigin();
                UpdateHeldUI();
            }
            );

    }

    public void OnSlotHoverEnter(int index)
    {
        if (inv == null || !inv.IsReady) return;
        if (confirmPopup != null && confirmPopup.isOpen) return;

        hoveredIndex = index;

        SetHoveredSlot(index);

        //드래그 중엔 툴팁 막기
        if(rightDragActive && inv.hasHeld)
        {
            TryRightDragPlace(index);
        }

        // 들고있는 중이면 막기
        if (inv.hasHeld) return;

        var stack = inv.Inventory.Get(index);
        if (stack.IsEmpty)
        {
            tooltip?.Hide();
            return;
        }

        //SFX 재생
        SoundManager.Instance?.PlaySfx(SfxId.Inv_Hover, 0.5f);

        if (!inv.TryGetDef(stack.itemId, out var def))
        {
            tooltip?.Hide();
            return;
        }


        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        tooltip?.Show(sp, def.name, def.description, stack.amount, stack.quality);
        tooltip?.SetPosition(lastPointerPos);
    }

    public void OnSlotHoverExit(int index)
    {
        if (inv == null || !inv.IsReady) return;
        if(confirmPopup != null && confirmPopup.isOpen) return;
        if (hoveredIndex == index)
            hoveredIndex = -1;

        ClearHoveredSlot(index);
        tooltip?.Hide();
    }

    public void Open(LayoutMode layout = LayoutMode.Normal)
    {
        if(!cooking.isOpen) SoundManager.Instance?.PlaySfx(SfxId.Inv_Open);

        currentLayout = layout;

        confirmPopup?.Hide();
        tooltip?.Hide();

        if (overlay != null) overlay.SetActive(true);
        root.SetActive(true);

        ApplyLayout(currentLayout);
        UpdateHeldUI();

        OnOpened?.Invoke();
               
    }

    private void ApplyLayout(LayoutMode layout)
    {
        if (inventoryBackground == null) return;

        switch (layout)
        {
            case LayoutMode.Normal:
                inventoryBackground.anchoredPosition = normalPos;
                inventoryBackground.sizeDelta = normalSize;
                SetGridColumns(columnsNormal);
                SetScroll(false);
                break;

            case LayoutMode.WithStorage:
                inventoryBackground.anchoredPosition = withStoragePos;
                inventoryBackground.sizeDelta = withStorageSize;
                SetGridColumns(columnsNormal);
                SetScroll(false);
                break;

            case LayoutMode.Cooking:
                inventoryBackground.anchoredPosition = cookingPos;
                inventoryBackground.sizeDelta = cookingSize;
                SetGridColumns(columnsCooking);
                SetScroll(true);
                break;

            case LayoutMode.Gift:
                inventoryBackground.anchoredPosition = giftPos;
                inventoryBackground.sizeDelta = giftSize;
                SetGridColumns(columnsGift);
                SetScroll(true);
                break;

            case LayoutMode.Shop:
                inventoryBackground.anchoredPosition = shopPos;
                inventoryBackground.sizeDelta = shopSize;
                SetGridColumns(columnsShop);
                SetScroll(true);
                break;
        }

        UpdateContentHeight();

        Canvas.ForceUpdateCanvases();
        if(scrollRect != null && scrollRect.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
    }

    private void SetGridColumns(int columns)
    {
        if (grid == null) return;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
    }

    private void SetScroll(bool enabled)
    {
        if(scrollRect != null)
        {
            //scrollRect.enabled = enabled;
            //scrollRect.horizontal = false;
            scrollRect.vertical = enabled;

            if (enabled)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        //인벤 스크롤바는 요리모드에서만
        if(verticalScrollbar != null)
            verticalScrollbar.gameObject.SetActive(enabled);
    }

    private void UpdateContentHeight()
    {
        if (grid == null || gridParent == null) return;

        var contentRT = gridParent as RectTransform;
        if (contentRT == null) return;

        int slotCount = inv != null && inv.IsReady ? inv.Inventory.slotCount : (slots != null ? slots.Length : 0);
        int cols = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.CeilToInt(slotCount / (float)cols);

        float h =
            grid.padding.top + grid.padding.bottom +
            rows * grid.cellSize.y +
            Mathf.Max(0, rows - 1) * grid.spacing.y;

        var size = contentRT.sizeDelta;
        size.y = h;
        contentRT.sizeDelta = size;
    }

    public void Close()
    {
        if(!cooking.isOpen && SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Inv_Close);

        confirmPopup?.Hide();
        tooltip?.Hide();

        //인벤토리 닫을 때 손에 들린 아이템 원위치
        inv.ReturnHeldToOrigin();

        root.SetActive(false);
        if (overlay != null) overlay.SetActive(false);

        UpdateHeldUI();

        OnClosed?.Invoke();
    }

    private void UpdateHeldUI()
    {
        if (hasHeld == null) return;

        if (!inv.hasHeld)
        {
            hasHeld.Hide();
            return;
        }

        var h = inv.heldStack;
        if (!inv.TryGetDef(h.itemId, out var def))
        {
            hasHeld.Hide();
            return;
        }

        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        hasHeld.Show(sp, h.amount);
    }

    public void SetHoveredSlot(int index)
    {
        //tooltip 처리용
        hoveredIndex = index;
    }
    public void ClearHoveredSlot(int index)
    {
        //툴팁 숨기기용
        if (hoveredIndex == index)
            hoveredIndex = -1;
    }

    public void SetPointerPos(Vector2 screenPos)
    {
        lastPointerPos = screenPos;

        if(tooltip != null && tooltip.isOpen && hoveredIndex >= 0)
            tooltip.SetPosition(screenPos);
    }
}
