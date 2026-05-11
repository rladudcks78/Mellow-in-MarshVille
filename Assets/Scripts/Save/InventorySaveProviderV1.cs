using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SaveGameV1.InventorySaveData <-> InventorySystem(ContainerGrid) 브릿지.
/// 현재 InventorySystem이 고정 10x4(40칸) 구조이므로,
/// - Capture: grid(40칸)를 InventorySaveData로 변환
/// - Apply: SaveData를 grid(최대 40칸)에 덮어쓰기
/// </summary>
public sealed class InventorySaveProviderV1 : MonoBehaviour, IInventorySaveProviderV1
{
    [Header("Drag your InventorySystem component")]
    [SerializeField] private InventorySystem inventorySystem;

    // 현재 프로젝트 인벤 규격(InventorySystem 내부 const와 동일)
    private const int DefaultCols = 10;
    private const int DefaultRows = 4;
    private const int DefaultSlotCount = DefaultCols * DefaultRows; // 40

    private void Awake()
    {
        if (inventorySystem == null)
            inventorySystem = FindAnyObjectByType<InventorySystem>();
    }

    public InventorySaveData CaptureInventory()
    {
        var inv = inventorySystem != null ? inventorySystem : FindAnyObjectByType<InventorySystem>();
        if (inv == null || inv.Inventory == null)
        {
            Debug.LogWarning("[InventorySaveProviderV1] InventorySystem/ContainerGrid not found. Returning default.");
            return InventorySaveData.CreateDefault();
        }

        var grid = inv.Inventory;

        var data = new InventorySaveData
        {
            cols = DefaultCols,
            rows = DefaultRows,
            slotCount = DefaultSlotCount,
            slots = new List<ItemStackSave>(DefaultSlotCount)
        };

        int n = Mathf.Min(grid.slotCount, DefaultSlotCount);
        for (int i = 0; i < n; i++)
            data.slots.Add(ItemStackSave.FromRuntime(grid.Get(i)));

        // grid가 더 짧거나(이상 케이스) 남는 칸은 Empty로 채움
        for (int i = n; i < DefaultSlotCount; i++)
            data.slots.Add(ItemStackSave.Empty);

        data.Normalize();
        return data;
    }

    public void ApplyInventory(InventorySaveData data)
    {
        if (data == null) return;
        data.Normalize();

        var inv = inventorySystem != null ? inventorySystem : FindAnyObjectByType<InventorySystem>();
        if (inv == null || inv.Inventory == null)
        {
            Debug.LogError("[InventorySaveProviderV1] InventorySystem/ContainerGrid not found. Apply skipped.");
            return;
        }

        var grid = inv.Inventory;

        // SaveData(보통 40칸) -> 실제 grid에 덮어쓰기
        int writeCount = Mathf.Min(grid.slotCount, data.slotCount, data.slots.Count);

        for (int i = 0; i < writeCount; i++)
            grid.Set(i, data.slots[i].ToRuntime());

        // 남는 실제 슬롯은 비우기(안전)
        for (int i = writeCount; i < grid.slotCount; i++)
            grid.Set(i, ItemStack.empty);
    }
}