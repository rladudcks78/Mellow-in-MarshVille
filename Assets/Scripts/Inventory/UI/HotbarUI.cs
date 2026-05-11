using System.Collections;
using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventorySystem inv;
    [SerializeField] private SpriteResolver spriteResolver;

    [Header("Build")]
    [SerializeField] private Transform slotGrid;
    [SerializeField] private HotbarSlotUI slotPrefab;

    [Header("Selection")]
    [SerializeField] private RectTransform selectionFrame;

    [Header("Option")]
    [SerializeField] private bool clickToSelectEnabled = true;   // CHANGED
    public bool ClickToSelectEnabled => clickToSelectEnabled;     // CHANGED

    private HotbarSlotUI[] slots = new HotbarSlotUI[10];
    private bool built; // ������ ����

    private void OnEnable()
    {
        // �̹� ��� �Ϸ�� �������ø�
        if (built)
        {
            RefreshAll();
            UpdateSelection(inv != null ? inv.ActiveSlotIndex : -1);
            BindEvents();
            return;
        }

        StartCoroutine(WaitAndBuild());
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private IEnumerator WaitAndBuild()
    {
        if (inv == null)
            inv = FindFirstObjectByType<InventorySystem>();

        if (inv == null)
        {
            Debug.LogError("[HotbarUI] InventorySystem ����");
            yield break;
        }

        while (!inv.IsReady)
            yield return null;

        BuildSlots();
        BindEvents();
        RefreshAll();

        UpdateSelection(inv.ActiveSlotIndex);

        built = true;
    }

    private void BuildSlots()
    {
        if (slotGrid == null || slotPrefab == null)
        {
            Debug.LogError("[HotbarUI] slotGrid / slotPrefab ���� �ʿ�");
            return;
        }

        //���� �ڽ� ����
        for (int i = slotGrid.childCount - 1; i >= 0; i--)
            Destroy(slotGrid.GetChild(i).gameObject);

        for (int i = 0; i < 10; i++)
        {
            var s = Instantiate(slotPrefab, slotGrid);
            s.Init(this, i);  // i == �κ��丮 0~9
            slots[i] = s;
        }
    }

    private void BindEvents()
    {
        if (inv == null || inv.Inventory == null) return;

        UnbindEvents(); // �ߺ� ���� ����

        inv.Inventory.OnSlotChanged += OnInventorySlotChanged;
        inv.Inventory.OnRebuilt += OnInventoryRebuilt;
        inv.OnActiveSlotChanged += UpdateSelection;
    }

    private void UnbindEvents()
    {
        if (inv == null || inv.Inventory == null) return;

        inv.Inventory.OnSlotChanged -= OnInventorySlotChanged;
        inv.Inventory.OnRebuilt -= OnInventoryRebuilt;
        inv.OnActiveSlotChanged -= UpdateSelection;
    }

    private void OnInventorySlotChanged(int index)
    {
        if (index < 0 || index >= 10) return;
        RefreshOne(index);
    }

    private void OnInventoryRebuilt()
    {
        RefreshAll();
        UpdateSelection(inv != null ? inv.ActiveSlotIndex : -1);
    }

    /// <summary>
    /// ���� Ŭ������ ȣ���ϴ� API
    /// </summary>
    /// <param name="inventoryIndex"></param>
    public void RequestSelect(int inventoryIndex)
    {
        if (inv == null || !inv.IsReady) return;
        inv.SetActiveSlot(inventoryIndex);
    }

    private void RefreshAll()
    {
        if (inv == null || !inv.IsReady) return;

        for (int i = 0; i < 10; i++)
            RefreshOne(i);
    }

    private void RefreshOne(int index)
    {
        if (inv == null || slots[index] == null) return;

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

    private void UpdateSelection(int activeSlotIndex)
    {
        if (selectionFrame == null) return;

        // �ֹ� ���� �� ������ ����
        if (activeSlotIndex < 0 || activeSlotIndex >= 10)
        {
            selectionFrame.gameObject.SetActive(false);
            return;
        }

        var targetSlot = slots[activeSlotIndex];
        if (targetSlot == null)
        {
            selectionFrame.gameObject.SetActive(false);
            return;
        }

        selectionFrame.gameObject.SetActive(true);
        selectionFrame.SetParent(targetSlot.transform, false);
        selectionFrame.SetAsLastSibling();

        selectionFrame.anchorMin = Vector2.zero;
        selectionFrame.anchorMax = Vector2.one;
        selectionFrame.anchoredPosition = Vector2.zero;
        selectionFrame.sizeDelta = Vector2.zero;

        selectionFrame.localScale = Vector3.one; // ���̾ƿ�/������ ���� ����
    }
}
