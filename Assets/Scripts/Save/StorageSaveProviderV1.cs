using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SaveGameV1.StorageSaveData <-> StorageSystem(ContainerGrid) 브릿지.
/// - Capture: StorageSystem(StorageGrid) -> StorageSaveData
/// - Apply  : StorageSaveData -> StorageSystem(SetUpgradeCount + grid overwrite)
/// </summary>
public sealed class StorageSaveProviderV1 : MonoBehaviour, IStorageSaveProviderV1
{
    [Header("Drag your StorageSystem component")]
    [SerializeField] private StorageSystem storageSystem;

    private void Awake()
    {
        if (storageSystem == null)
            storageSystem = FindAnyObjectByType<StorageSystem>();
    }

    public StorageSaveData CaptureStorage()
    {
        var sys = storageSystem != null ? storageSystem : FindAnyObjectByType<StorageSystem>();
        if (sys == null || sys.Storage == null)
        {
            Debug.LogWarning("[StorageSaveProviderV1] StorageSystem/ContainerGrid not found. Returning default.");
            return StorageSaveData.CreateDefault();
        }

        var grid = sys.Storage;
        int cap = Mathf.Max(1, grid.slotCount);

        var data = new StorageSaveData
        {
            upgradeCount = Mathf.Max(0, sys.UpgradeCount),
            capacity = cap,
            slots = new List<ItemStackSave>(cap)
        };

        for (int i = 0; i < cap; i++)
            data.slots.Add(ItemStackSave.FromRuntime(grid.Get(i)));

        data.Normalize();
        return data;
    }

    public void ApplyStorage(StorageSaveData data)
    {
        if (data == null) return;
        data.Normalize();

        var sys = storageSystem != null ? storageSystem : FindAnyObjectByType<StorageSystem>();
        if (sys == null)
        {
            Debug.LogError("[StorageSaveProviderV1] StorageSystem not found. Apply skipped.");
            return;
        }

        // 1) 업그레이드 단계 먼저 적용 -> 내부에서 Capacity/그리드 리빌드됨
        sys.SetUpgradeCount(Mathf.Max(0, data.upgradeCount));

        var grid = sys.Storage;
        if (grid == null)
        {
            Debug.LogError("[StorageSaveProviderV1] Storage grid is null after SetUpgradeCount. Apply skipped.");
            return;
        }

        // 2) 슬롯 덮어쓰기 (save가 더 크면 초과분은 버림, grid가 더 크면 나머지는 비움)
        int writeCount = Mathf.Min(grid.slotCount, data.slots.Count);

        for (int i = 0; i < writeCount; i++)
            grid.Set(i, data.slots[i].ToRuntime());

        for (int i = writeCount; i < grid.slotCount; i++)
            grid.Set(i, ItemStack.empty);

        // StorageSystem은 grid 이벤트를 외부로 래핑(OnSlotChanged/OnRebuilt)하고 있으니
        // 별도 Refresh 호출이 없어도 UI가 그 이벤트에 붙어있으면 갱신됨.
    }
}