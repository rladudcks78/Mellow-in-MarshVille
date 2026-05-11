using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StorageUI : MonoBehaviour
{
    public enum LayoutMode
    {
        Normal,
        Cooking
    }

    [Header("Refs")]
    [SerializeField] private StorageSystem storage;
    [SerializeField] private SpriteResolver spriteResolver;
    [SerializeField] private TooltipUI tooltip;
    [SerializeField] private HeldItemUI heldUI;
    [SerializeField] private CookingUI cooking;

    [Header("Build")]
    [SerializeField] private Transform slotGrid;
    [SerializeField] private StorageSlotUI slotPrefab;

    [Header("Open/Close Roots")]
    [SerializeField] private GameObject root;

    [Header("Layout Target")]
    [SerializeField] private RectTransform storageBackground; // 인스펙터에서 직접 지정 가능하게

    [Header("Layout Presets")]
    [SerializeField] private Vector2 normalPos = new Vector2(587f, 0f);
    [SerializeField] private Vector2 normalSize = new Vector2(560f, 800f);

    [SerializeField] private Vector2 cookingPos = new Vector2(-580f, 25f);
    [SerializeField] private Vector2 cookingSize = new Vector2(620f, 350f);

    [Header("Grid")]
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private int columns = 5;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    private SharedHeldSystem shareHeld;
    private StorageSlotUI[] slots;
    private Vector2 lastPointerPos;

    private LayoutMode currentMode = LayoutMode.Normal;

    public int hoveredIndex { get; private set; } = -1;
    public bool isOpen => (root != null ? root.activeSelf : gameObject.activeSelf);


    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);

        shareHeld = SharedHeldSystem.Instance;
        if (shareHeld == null)
            shareHeld = FindFirstObjectByType<SharedHeldSystem>();

        if (grid == null && slotGrid != null)
            grid = slotGrid.GetComponent<GridLayoutGroup>();

        // storageBackground 자동 연결(인스펙터에서 비워뒀을 때만)
        if (storageBackground == null && root != null)
            storageBackground = root.GetComponent<RectTransform>();

        // 스크롤 기본 세팅은 여기서 1회 고정
        SetupScroll();
        SetupGrid();
    }

    private void Start()
    {
        StartCoroutine(WaitAndBuild());
    }

    private IEnumerator WaitAndBuild()
    {
        if (storage == null)
            storage = FindFirstObjectByType<StorageSystem>();

        if (storage == null)
        {
            Debug.LogError("[StorageUI] StorageSystem 없음");
            yield break;
        }

        while (!storage.IsReady)
            yield return null;

        BuildSlots();

        storage.OnSlotChanged += OnSlotChanged;
        storage.OnRebuilt += OnRebuilt;

        if (shareHeld != null)
            shareHeld.OnChanged += UpdateHeldUI;

        ApplyLayout(currentMode);
        RefreshAll();
        UpdateHeldUI();
    }

    private void OnDestroy()
    {
        if (storage != null)
        {
            storage.OnSlotChanged -= OnSlotChanged;
            storage.OnRebuilt -= OnRebuilt;
        }

        if (shareHeld != null)
            shareHeld.OnChanged -= UpdateHeldUI;
    }

    private void BuildSlots()
    {
        if (slotGrid == null || slotPrefab == null) return;

        int count = storage.Storage.slotCount;
        slots = new StorageSlotUI[count];

        for (int i = slotGrid.childCount - 1; i >= 0; i--)
            Destroy(slotGrid.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var s = Instantiate(slotPrefab, slotGrid);
            s.Init(this, i);
            slots[i] = s;
        }
    }

    private void OnSlotChanged(int index)
    {
        if (slots == null) return;
        if (index < 0 || index >= slots.Length) return;
        RefreshOne(index);
    }

    private void OnRebuilt()
    {
        BuildSlots();
        RefreshAll();

        if (slots == null || hoveredIndex >= slots.Length)
            hoveredIndex = -1;

        ApplyLayout(currentMode);
        UpdateHeldUI();
    }

    private void RefreshAll()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
            RefreshOne(i);
    }

    private void RefreshOne(int index)
    {
        if (storage == null || slots == null || slots[index] == null) return;

        var stack = storage.Storage.Get(index);
        if (stack.IsEmpty)
        {
            slots[index].Clear();
            return;
        }

        if (!storage.TryGetDef(stack.itemId, out var def))
        {
            slots[index].Clear();
            return;
        }

        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        slots[index].Set(sp, stack.amount);
    }

    public void Open(LayoutMode mode)
    {
        if(!cooking.isOpen && SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Storage_Open);

        tooltip?.Hide();
        currentMode = mode;

        root.SetActive(true);

        ApplyLayout(mode);
        UpdateHeldUI();
    }

    private void ApplyLayout(LayoutMode mode)
    {
        if (storageBackground == null) return;

        // 레이아웃만 모드에 따라 변경
        if (mode == LayoutMode.Normal)
        {
            storageBackground.anchoredPosition = normalPos;
            storageBackground.sizeDelta = normalSize;
        }
        else // Cooking
        {
            storageBackground.anchoredPosition = cookingPos;
            storageBackground.sizeDelta = cookingSize;
        }

        // 스크롤은 항상 켜져있으니, 열 때마다 맨 위로만 올려줌
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        UpdateContentHeight();

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null && scrollRect.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
    }

    private void SetupGrid()
    {
        if (grid == null) return;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
    }

    private void SetupScroll()
    {
        if (scrollRect != null)
        {
            // ScrollRect는 꺼버리지 말고, 항상 vertical만 쓰는 구조로 고정
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // 창고는 항상 보이게 할 거라 했으니 Permanent 고정
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        if (verticalScrollbar != null)
        {
            verticalScrollbar.gameObject.SetActive(true);
        }
    }

    private void UpdateContentHeight()
    {
        if (grid == null || slotGrid == null) return;

        var contentRT = slotGrid as RectTransform;
        if (contentRT == null) return;

        int slotCount = storage != null && storage.IsReady ? storage.Storage.slotCount : (slots != null ? slots.Length : 0);
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
        //if(!cooking.isOpen) SoundManager.Instance.PlaySfx(SfxId.Storage_Close);

        tooltip?.Hide();
        hoveredIndex = -1;

        storage?.ReturnHeldToOrigin();
        root.SetActive(false);

        UpdateHeldUI();
    }

    public void OnSlotHoverEnter(int index)
    {
        if (storage == null || !storage.IsReady) return;
        hoveredIndex = index;

        if (shareHeld != null && shareHeld.HasItem) return;

        var stack = storage.Storage.Get(index);
        if (stack.IsEmpty)
        {
            tooltip?.Hide();
            return;
        }

        if (!storage.TryGetDef(stack.itemId, out var def))
        {
            tooltip?.Hide();
            return;
        }

        // SFX 재생
        SoundManager.Instance?.PlaySfx(SfxId.Inv_Hover);

        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        tooltip?.Show(sp, def.name, def.spritePath, stack.amount);
        tooltip?.SetPosition(lastPointerPos);
    }

    public void OnSlotHoverExit(int index)
    {
        if (hoveredIndex == index)
            hoveredIndex = -1;

        tooltip?.Hide();
    }

    public void SetPointerPos(Vector2 screenPos)
    {
        lastPointerPos = screenPos;
        if (tooltip != null && tooltip.isOpen && hoveredIndex >= 0)
            tooltip.SetPosition(screenPos);
    }

    public void OnSlotLeftClick(int index)
    {
        tooltip?.Hide();
        storage?.OnSlotLeftClick(index);
        UpdateHeldUI();
    }

    public void OnSlotRightClick(int index)
    {
        tooltip?.Hide();
        storage?.OnSlotRightClick(index);
        UpdateHeldUI();
    }

    private void UpdateHeldUI()
    {
        if (heldUI == null) return;
        if (shareHeld == null || !shareHeld.HasItem)
        {
            heldUI.Hide();
            return;
        }

        var h = shareHeld.Stack;

        if (storage == null || !storage.TryGetDef(h.itemId, out var def))
        {
            heldUI.Hide();
            return;
        }

        var sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;
        heldUI.Show(sp, h.amount);
    }
}
