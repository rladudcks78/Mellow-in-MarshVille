using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ItemLoader itemLoader;

    [Header("Capacity")]
    [SerializeField] private int baseSlots = 20;            //기본 20칸
    [SerializeField] private int slotsPerUpgrade = 20;      //업그레이드 1회당 20칸 증가
    [SerializeField] private int maxUpgradeCount = 2;       //총 2번 업그레이드 가능

    [Header("Init Retry")]
    [SerializeField] private bool retryFindItemLoader = true;
    [SerializeField] private float retryInterval = 0.2f;
    [SerializeField] private bool logRetry = false;
    private Coroutine findLoaderRoutine;
    private bool registeredToItemLoader;

    [Header("Debug")]
    [SerializeField] private bool logChanges;

    

    private List<int> addOrderCache;

    private ItemDatabase itemDb;
    private bool ready;

    private ContainerGrid storageGrid;
    private SharedHeldSystem sharedHeld;

    [SerializeField] private int upgradeCount = 0;

    public bool IsReady => ready;
    public ContainerGrid Storage => storageGrid;
    public int UpgradeCount => upgradeCount;
    public int Capacity => baseSlots + Mathf.Clamp(upgradeCount, 0, maxUpgradeCount) * slotsPerUpgrade;
    public bool CanUpgrade => upgradeCount < maxUpgradeCount;

    public event Action<int> OnSlotChanged;
    public event Action OnRebuilt;

    private void Awake()
    {
        sharedHeld = SharedHeldSystem.Instance;
        if (sharedHeld == null)
            sharedHeld = FindAnyObjectByType<SharedHeldSystem>();

        storageGrid = new ContainerGrid(Capacity);
        RebuildAddOrderCache();
        BindGridEvents(storageGrid);

        TryBindItemLoaderOnce();
    }

    private void Start()
    {
        if (!registeredToItemLoader && retryFindItemLoader && findLoaderRoutine == null)
        {
            findLoaderRoutine = StartCoroutine(Co_FindAndBindItemLoader());
        }
    }

    private void OnEnable()
    {
        if (!registeredToItemLoader)
        {
            TryBindItemLoaderOnce();

            if (!registeredToItemLoader && retryFindItemLoader && findLoaderRoutine == null)
                findLoaderRoutine = StartCoroutine(Co_FindAndBindItemLoader());
        }
    }

    private void OnDestroy()
    {
        if (findLoaderRoutine != null)
        {
            StopCoroutine(findLoaderRoutine);
            findLoaderRoutine = null;
        }

        if (registeredToItemLoader && itemLoader != null)
        {
            itemLoader.Unregister(OnItemDbLoaded, OnItemDbFailed);
            registeredToItemLoader = false;
        }

        UnbindGridEvents(storageGrid);
    }

    private bool IsUnityAlive(UnityEngine.Object obj)
    {
        return obj != null;
    }

    /// <summary>
    /// ItemLoader를 1회 탐색하고 바인딩 시도. 실패하면 false 반환
    /// </summary>
    /// <returns></returns>
    private bool TryBindItemLoaderOnce()
    {
        // 이미 정상 등록된 상태면 끝
        if (registeredToItemLoader && IsUnityAlive(itemLoader))
            return true;

        // 깨진 참조(Missing)였던 경우 정리
        if (!IsUnityAlive(itemLoader))
            itemLoader = null;

        // 1) 인스턴스 우선 (싱글톤이면 가장 빠르고 정확)
        if (itemLoader == null && ItemLoader.Instance != null)
            itemLoader = ItemLoader.Instance;

        // 2) 씬/비활성 포함 탐색 (Title->InGame 전환 타이밍 이슈 대응)
        if (itemLoader == null)
            itemLoader = FindFirstObjectByType<ItemLoader>(FindObjectsInactive.Include);

        // 못 찾음 -> retry 대상으로 남김
        if (itemLoader == null)
        {
            ready = false;

            if (logRetry)
                Debug.LogWarning("[StorageSystem] ItemLoader 탐색 실패 (Instance / FindFirstObjectByType 모두 실패)");

            return false;
        }

        // 혹시 이전에 다른 loader에 등록된 상태면 정리 (안전장치)
        if (registeredToItemLoader)
        {
            try
            {
                itemLoader.Unregister(OnItemDbLoaded, OnItemDbFailed);
            }
            catch { /* 무시 */ }

            registeredToItemLoader = false;
        }

        // 등록
        itemLoader.Register(OnItemDbLoaded, OnItemDbFailed);
        registeredToItemLoader = true;

        if (logRetry || logChanges)
        {
            Debug.Log($"[StorageSystem] ItemLoader 바인딩 성공 -> {itemLoader.gameObject.name} (activeInHierarchy={itemLoader.gameObject.activeInHierarchy})");
        }

        return true;
    }

    /// <summary>
    /// ItemLoader를 찾을 때 까지 주기적으로 재시도
    /// </summary>
    /// <returns></returns>
    private IEnumerator Co_FindAndBindItemLoader()
    {
        if(logRetry)
            Debug.Log("[StorageSystem] ItemLoader 탐색 재시작");

        var wait = new WaitForSeconds(Mathf.Max(0.05f, retryInterval));

        while (!registeredToItemLoader)
        {
            bool ok = TryBindItemLoaderOnce();
            if (ok)
            {
                findLoaderRoutine = null;
                yield break;
            }

            if(logRetry)
                Debug.Log("[StorageSystem] ItemLoader 탐색 실패, 재시도 대기...");

            yield return wait;
        }

        findLoaderRoutine = null;
    }

    private void OnItemDbLoaded(ItemDatabase db)
    {
        itemDb = db;
        ready = true;
        if (logChanges) Debug.Log("[StorageSystem] ItemDatabase 준비 완료");
    }

    private void OnItemDbFailed(string error)
    {
        ready = false;
        Debug.Log($"[StorageSystem] ItemDatabase 로드 실패 : {error}");
    }

    private void BindGridEvents(ContainerGrid grid)
    {
        if (grid == null) return;

        grid.OnSlotChanged += HandleSlotChanged;
        grid.OnRebuilt += HandleRebuilt;
    }

    private void UnbindGridEvents(ContainerGrid grid)
    {
        if (grid == null) return;

        grid.OnSlotChanged -= HandleSlotChanged;
        grid.OnRebuilt -= HandleRebuilt;
    }

    private void HandleSlotChanged(int index)
    {
        OnSlotChanged?.Invoke(index);
        if (logChanges) Debug.Log($"[StorageSystem] SlotChanged {index}");
    }

    private void HandleRebuilt()
    {
        OnRebuilt?.Invoke();
        if (logChanges) Debug.Log($"[StorageSystem] Rebuilt");
    }

    private int GetMaxStack(int itemId)
    {
        if (itemDb == null) return 1;
        if (!itemDb.TryGet(itemId, out var def)) return 1;
        return Mathf.Max(1, def.maxStack);
    }

    public bool TryGetDef(int itemId, out ItemDef def)
    {
        def = null;
        return ready && itemDb != null && itemDb.TryGet(itemId, out def);
    }

    /// <summary>
    /// 창고 업그레이드 구매 처리
    /// </summary>
    /// <returns></returns>
    public bool TryApplyUpgrade()
    {
        if (!CanUpgrade) return false;

        upgradeCount++;
        RebuildCapacity(Capacity);
        return true;
    }

    /// <summary>
    /// 세이브 로드 등에서 업그레이드 단계 강제 세팅용
    /// </summary>
    /// <param name="newCount"></param>
    public void SetUpgradeCount(int newCount)
    {
        newCount = Mathf.Clamp(newCount, 0, maxUpgradeCount);
        if (upgradeCount == newCount) return;

        upgradeCount = newCount;
        RebuildCapacity(Capacity);
    }

    private void RebuildCapacity(int newSlotCount)
    {
        newSlotCount = Mathf.Max(1, newSlotCount);

        var old = storageGrid;
        var next = new ContainerGrid(newSlotCount);

        int copyCount = Mathf.Min(old.slotCount, next.slotCount);
        for(int i = 0; i < copyCount; i++)
            next.Set(i, old.Get(i));

        UnbindGridEvents(old);
        storageGrid = next;
        RebuildAddOrderCache();
        BindGridEvents(storageGrid);

        OnRebuilt?.Invoke();
    }

    private void RebuildAddOrderCache()
    {
        if (storageGrid == null) return;

        if (addOrderCache == null)
            addOrderCache = new List<int>(storageGrid.slotCount);

        addOrderCache.Clear();
        for (int i = 0; i < storageGrid.slotCount; i++)
            addOrderCache.Add(i);
    }

    public int TryAddFromExternal(int itemId, int amount, int quality = 0)
    {
        if (!ready) return amount;
        if (amount <= 0) return 0;
        if (itemDb == null) return amount;
        if (storageGrid == null) return amount;

        if (!itemDb.TryGet(itemId, out var def))
            return amount;

        int maxStack = Mathf.Max(1, def.maxStack);

        if (addOrderCache == null || addOrderCache.Count != storageGrid.slotCount)
            RebuildAddOrderCache();

        return storageGrid.TryAdd(itemId, amount, maxStack, addOrderCache, quality);
    }

    public void OnSlotLeftClick(int slotIndex)
    {
        if (!ready) return;
        if (storageGrid == null || !storageGrid.IsValidIndex(slotIndex)) return;
        if (sharedHeld == null) return;

        if (!sharedHeld.HasItem)
        {
            if(storageGrid.TryTake(slotIndex, out var taken))
            {
                sharedHeld.Pick(taken, new SlotRef(ContainerKind.Storage, slotIndex));
            }
            return;
        }

        var origin = sharedHeld.Origin;
        var held = sharedHeld.Stack;
        var target = storageGrid.Get(slotIndex);

        if (origin.kind == ContainerKind.Storage && origin.index == slotIndex)
        {
            if (target.IsEmpty)
            {
                storageGrid.Set(slotIndex, held);
                sharedHeld.Clear();
                return;
            }

            if (target.itemId == held.itemId && target.quality == held.quality)
            {
                int maxStack = GetMaxStack(held.itemId);
                int space = maxStack - target.amount;
                if (space <= 0) return;

                int move = Mathf.Min(space, held.amount);
                target.amount += move;
                held.amount -= move;

                storageGrid.Set(slotIndex, target);
                if (held.amount <= 0) sharedHeld.Clear();
                else sharedHeld.Pick(held, origin);

                return;
            }
        }
        
            if(target.IsEmpty)
            {
                storageGrid.Set(slotIndex, held);
                sharedHeld.Clear();
                return;
            }

            if(target.itemId == held.itemId && target.quality == held.quality)
            {
                int maxStack = GetMaxStack(held.itemId);
                int space = maxStack - target.amount;
                if (space <= 0) return;

                int move = Mathf.Min(space, held.amount);
                target.amount += move;
                held.amount -= move;

                storageGrid.Set(slotIndex,target);

                if (held.amount <= 0) sharedHeld.Clear();
                else sharedHeld.Pick(held, origin);

                return;
            }

            storageGrid.Set(slotIndex, held);
            sharedHeld.Pick(target, new SlotRef(ContainerKind.Storage, slotIndex));
    }

    public void OnSlotRightClick(int slotIndex)
    {
        if (!ready) return;
        if (storageGrid == null || !storageGrid.IsValidIndex(slotIndex)) return;
        if (sharedHeld == null) return;

        if (!sharedHeld.HasItem) return;

        var origin = sharedHeld.Origin;
        var held = sharedHeld.Stack;
        var target = storageGrid.Get(slotIndex);

        if (target.IsEmpty)
        {
            storageGrid.Set(slotIndex, new ItemStack(held.itemId, 1, held.quality));

            held.amount -= 1;

            if (held.amount <= 0) sharedHeld.Clear();
            else sharedHeld.Pick(held, origin);

            return;
        }

        if(target.itemId == held.itemId && target.quality == held.quality)
        {
            int maxStack = GetMaxStack(held.itemId);
            if (target.amount >= maxStack) return;

            target.amount += 1;
            held.amount -= 1;

            storageGrid.Set(slotIndex, target);

            if (held.amount <= 0) sharedHeld.Clear();
            else sharedHeld.Pick(held, origin);

            return;
        }
    }

    public void ReturnHeldToOrigin()
    {
        if (sharedHeld == null) return;
        if (!sharedHeld.HasItem) return;

        var origin = sharedHeld.Origin;
        if (origin.kind != ContainerKind.Storage) return;

        int idx = origin.index;
        if(storageGrid == null || !storageGrid.IsValidIndex(idx))
        {
            sharedHeld.Clear();
            return;
        }

        var held = sharedHeld.Stack;
        var target = storageGrid.Get(idx);

        if (target.IsEmpty)
        {
            storageGrid.Set(idx, held);
            sharedHeld.Clear();
            return;
        }

        if(target.itemId == held.itemId)
        {
            int maxStack = GetMaxStack(held.itemId);
            int space = maxStack - target.amount;

            if(space > 0)
            {
                int move = Mathf.Min(space, held.amount);
                target.amount += move;
                held.amount -= move;
                storageGrid.Set(idx, target);
            }

            if (held.amount <= 0) sharedHeld.Clear();
            else sharedHeld.Pick(held, origin);

            return;
        }
    }

    public bool ConsumeAt(int slotIndex, int amount)
    {
        if (!IsReady) return false;
        if (amount <= 0) return false;
        if (!Storage.IsValidIndex(slotIndex)) return false;

        var s = Storage.Get(slotIndex);
        if (s.IsEmpty) return false;
        if (s.amount < amount) return false;

        s.amount -= amount;
        if (s.amount <= 0) Storage.Set(slotIndex, default);
        else Storage.Set(slotIndex, s);

        return true;
    }

    #region 요리모드


    /// <summary>
    /// 창고에 있는 특정 아이템 총 갯수 세기
    /// </summary>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public int CountItem(int itemId)
    {
        if (!IsReady) return 0;
        if (storageGrid == null) return 0;

        int total = 0;
        for(int i = 0; i < storageGrid.slotCount; i++)
        {
            var s = storageGrid.Get(i);
            if (!s.IsEmpty && s.itemId == itemId)
                total += s.amount;
        }
        return total;
    }

    public bool RemoveItem(int itemId, int amount)
    {
        if (!IsReady) return false;
        if (storageGrid == null) return false;
        if (amount <= 0) return true;

        int total = CountItem(itemId);
        if(total < amount)
        {
            Debug.LogWarning($"[StorageSystem] 아이템 부족 : {itemId}, 필요 = {amount}, 보유 = {total}");
            return false;
        }

        int remain = amount;

        //뒤에서부터 제거
        for (int i = storageGrid.slotCount - 1; i >= 0 && remain > 0; i--)
        {
            var s = storageGrid.Get(i);
            if (s.IsEmpty || s.itemId != itemId) continue;

            int take = Mathf.Min(s.amount, remain);
            s.amount -= take;
            remain -= take;

            if (s.amount <= 0) storageGrid.Set(i, default);
            else storageGrid.Set(i, s);
        }

        Debug.Log($"[StorageSystem] 아이템 제거 : itemId = {itemId}, amount = {amount}");
        return true;
    }

    #endregion
}
