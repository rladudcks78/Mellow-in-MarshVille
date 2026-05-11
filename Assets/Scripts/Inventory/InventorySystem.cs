using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;

public class InventorySystem : MonoBehaviour
{
    [SerializeField] private ItemLoader itemLoader;

    private ItemDatabase itemDb;
    private bool ready;

    //인벤토리 고정 규격
    private const int Cols = 10;
    private const int Rows = 4;
    private const int SlotCount = Cols * Rows;

    private ContainerGrid inventoryGrid;                // 실제 인벤토리 데이터                              
    private List<int> autoAddOrder;                     // 자동 수납 순서 캐시 (2~4행 -> 핫바)
    private SharedHeldSystem sharedHeld;                // 수납공간 종류에 따른 공유 손

    public int ActiveSlotIndex { get; private set; } = -1;
    public bool HasActive => ActiveSlotIndex >= 0;

    public bool IsReady => ready;
    public bool hasHeld => sharedHeld != null && sharedHeld.HasItem;
    public ItemStack heldStack => sharedHeld != null ? sharedHeld.Stack : default;
    public ContainerGrid Inventory => inventoryGrid;

    public event Action<ItemDef> OnActivateRequested;   //활성화 됨(손에 들었음) 알림용
    public event Action<int> OnActiveSlotChanged;       //활성 슬롯 번호

    public event Action<int, int> OnItemAdded;          // 아이템이 인벤토리에 "실제로 들어간" 순간을 알리는 이벤트


    private void Awake()
    {
        //인벤토리 모델 생성
        inventoryGrid = new ContainerGrid(SlotCount);
        autoAddOrder = BuildAutoAddOrder();

        sharedHeld = SharedHeldSystem.Instance;
        if (sharedHeld == null)
            sharedHeld = FindFirstObjectByType<SharedHeldSystem>();

        //핫바에서 활성화된 슬롯이 비어지면 자동 해제 이벤트 구독
        //인벤토리에서 아이템을 집어서 슬롯이 비는 경우 포함
        inventoryGrid.OnSlotChanged += OnInventorySlotChangedInternal;
        inventoryGrid.OnRebuilt += OnInventoryRebuiltInternal;

        //인스펙터 연결 안했으면 자동 탐색
        if (itemLoader == null)
            itemLoader = FindFirstObjectByType<ItemLoader>();

        //이벤트 등록
        itemLoader.Register(OnItemDbLoaded, OnItemDbFailed);
    }

    private void OnDestroy()
    {
        //메모리/ 중복 호출 방지
        if (itemLoader != null)
            itemLoader.Unregister(OnItemDbLoaded, OnItemDbFailed);

        if (inventoryGrid != null)
        {
            inventoryGrid.OnSlotChanged -= OnInventorySlotChangedInternal;
            inventoryGrid.OnRebuilt -= OnInventoryRebuiltInternal;
        }
    }

    /// <summary>
    /// 내부 슬롯 변경 감시
    /// 활성 슬롯이 비워지면 자동 해제
    /// </summary>
    /// <param name="index"></param>
    private void OnInventorySlotChangedInternal(int index)
    {
        if (index != ActiveSlotIndex) return;
        if (!inventoryGrid.IsValidIndex(index)) return;

        if (inventoryGrid.Get(index).IsEmpty)
        {
            ActiveSlotIndex = -1;
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
        }
    }

    /// <summary>
    /// 인벤토리 구조 재빌드 감시
    /// ActiveSlotIndex가 유효하지 않거나 비어있으면 해제
    /// </summary>
    private void OnInventoryRebuiltInternal()
    {
        if (ActiveSlotIndex < 0) return;

        if (!inventoryGrid.IsValidIndex(ActiveSlotIndex))
        {
            ActiveSlotIndex = -1;
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
            return;
        }

        if (inventoryGrid.Get(ActiveSlotIndex).IsEmpty)
        {
            ActiveSlotIndex = -1;
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
        }
    }

    private void OnItemDbLoaded(ItemDatabase db)
    {
        itemDb = db;
        ready = true;

        //여기서부터 itemDb 사용가능
        Debug.Log("[InventorySystem] ItemDatabase 준비 완료");
    }

    private void OnItemDbFailed(string error)
    {
        ready = false;
        Debug.Log($"[InventorySystem] ItemDatabase 로드 실패 : {error}");
    }

    /// <summary>
    /// 아이템 획득 (자동 수납 + 스택)
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryPickup(int itemId, int amount, int quality = 0)
    {
        if (!ready)
        {
            Debug.LogWarning("[InventorySystem] 아직 ItemDB 준비 전이라 Pickup 불가");
            return false;
        }

        if (amount <= 0) return true;

        if (!itemDb.TryGet(itemId, out var def))
        {
            Debug.LogError($"[InventorySystem] ItemDef 없음 : {itemId}");
            return false;
        }

        int maxStack = def.maxStack;
        if (maxStack <= 0) maxStack = 1;

        int remain = inventoryGrid.TryAdd(itemId, amount, maxStack, autoAddOrder, quality);

        int added = amount - remain;
        // 더해진 수량 이벤트로 통지
        if (added > 0)
            OnItemAdded?.Invoke(itemId, added);

        return remain == 0;
    }


    /// <summary>
    /// 자동 수납 순서 생성
    /// - 2~4행 먼저
    /// - 그 다음 핫바
    /// </summary>
    /// <returns></returns>
    private List<int> BuildAutoAddOrder()
    {
        var order = new List<int>(SlotCount);

        // 2~4행 먼저
        for (int r = 1; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                order.Add(r * Cols + c);
            }
        }

        //핫바
        for (int c = 0; c < Cols; c++)
        {
            order.Add(0 * Cols + c);
        }

        return order;
    }

    #region 인벤토리 상호작용 로직
    private int GetMaxStack(int itemId)
    {
        if (!itemDb.TryGet(itemId, out var def)) return 1;
        return Mathf.Max(1, def.maxStack);
    }

    /// <summary>
    /// 좌클릭 규칙
    /// - 손이 비었으면 : 슬롯에서 집기(그 슬롯 비우기)
    /// - 손에 들렸으면 :
    /// 1) 빈 슬롯 : 전체 놓기
    /// 2) 같은 아이템 : 가능한 만큼 합치기
    /// 3) 다른 아이템 : 스왑 후 손 비움
    /// </summary>
    /// <param name="slotIndex"></param>
    public void OnSlotLeftClick(int slotIndex)
    {
        if (!ready) return;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return;

        // 손이 비었으면 집기
        if (!sharedHeld.HasItem)
        {
            if (inventoryGrid.TryTake(slotIndex, out var taken))
            {
                sharedHeld.Pick(taken, new SlotRef(ContainerKind.Inventory, slotIndex));
            }
            return;
        }

        var origin = sharedHeld.Origin;
        var held = sharedHeld.Stack;
        var target = inventoryGrid.Get(slotIndex);

        // 손에 들린 상태
        // 원래 슬롯을 다시 클릭하면 원위치
        if (origin.kind == ContainerKind.Inventory && origin.index == slotIndex)
        {
            // 빈 슬롯이면 전체 놓기
            if (target.IsEmpty)
            {
                inventoryGrid.Set(slotIndex, held);
                sharedHeld.Clear();
                return;
            }

            // 같은 아이템이면 합치기(maxStack 안 넘을 만큼)
            if (target.itemId == held.itemId && target.quality == held.quality)
            {
                int maxStack = GetMaxStack(held.itemId);
                int space = maxStack - target.amount;
                if ((space <= 0)) return;   //꽉 차있으면 아무것도 안함

                int move = Mathf.Min(space, held.amount);
                target.amount += move;
                held.amount -= move;

                inventoryGrid.Set(slotIndex, target);

                if (held.amount <= 0) sharedHeld.Clear();
                else sharedHeld.Pick(held, origin);         //남아있으면 계속 들고있음

                return;
            }

            //원래 위치에 다른 아이템 생기는 경우 스왑(안전장치)
            inventoryGrid.Set(slotIndex, held);
            sharedHeld.Pick(target, origin);
            return;
        }

        // 정상 케이스 : 대상 -> 원래자리, 손 -> 대상자리, 손 비움

        // 빈 슬롯이면 전체 놓기
        if (target.IsEmpty)
        {
            inventoryGrid.Set(slotIndex, held);
            sharedHeld.Clear();
            return;
        }

        // 같은 아이템이면 합치기(maxStack 안 넘을 만큼)
        if (target.itemId == held.itemId && target.quality == held.quality)
        {
            int maxStack = GetMaxStack(held.itemId);
            int space = maxStack - target.amount;
            if ((space <= 0)) return;   //꽉 차있으면 아무것도 안함

            int move = Mathf.Min(space, held.amount);
            target.amount += move;
            held.amount -= move;

            inventoryGrid.Set(slotIndex, target);

            if (held.amount <= 0) sharedHeld.Clear();
            else sharedHeld.Pick(held, origin);
            return;
        }

        //다른 아이템이면 스왑
        if (origin.kind == ContainerKind.Inventory && inventoryGrid.IsValidIndex(origin.index))
        {
            int originIndex = origin.index;

            var originSlotNow = inventoryGrid.Get(originIndex);

            if (originSlotNow.IsEmpty)
            {
                inventoryGrid.Set(originIndex, target);
                inventoryGrid.Set(slotIndex, held);
                sharedHeld.Clear();
                return;
            }

            //origin슬롯이 이미 차있으면 대상 아이템을 origin으로 못보냄 -> 빈칸 찾아 넣고 진행
            int empty = FindFirstEmptyIndex();
            if (empty >= 0)
            {
                inventoryGrid.Set(empty, target);
                inventoryGrid.Set(slotIndex, held);
                sharedHeld.Clear();
                return;
            }

            return;
        }
    }
    private int FindFirstEmptyIndex()
    {
        for(int i = 0; i < inventoryGrid.slotCount; i++)
        {
            if (inventoryGrid.Get(i).IsEmpty) return i;
        }
        return -1;
    }


    public void OnSlotRightClick(int slotIndex)
    {
        if (!ready) return;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return;

        //손이 비어있으면 무시 (버튼 뜨는건 UI쪽에서 하기)
        if (!sharedHeld.HasItem) return;

        var held = sharedHeld.Stack;
        var target = inventoryGrid.Get(slotIndex);

        // 빈 슬롯이면 1개 놓기
        if (target.IsEmpty)
        {
            inventoryGrid.Set(slotIndex, new ItemStack(held.itemId, 1, held.quality));

            sharedHeld.SetAmount(held.amount - 1);

            return;
        }

        // 같은 아이템이면 1개 합치기
        if (target.itemId == held.itemId && target.quality == held.quality)
        {
            int maxStack = GetMaxStack(held.itemId);
            if (target.amount >= maxStack) return;

            target.amount += 1;
            inventoryGrid.Set(slotIndex, target);

            sharedHeld.SetAmount(held.amount - 1);
            return;
        }

        //다른 아이템이면 아무 일도 없음
    }

    /// <summary>
    /// 인벤 닫기/ 드롭 취소 시 들고있던 아이템 원래 자리로 복귀시켜주는 함수 (합치기 x)
    /// </summary>
    public void ReturnHeldToOrigin()
    {
        if (!sharedHeld.HasItem) return;

        var origin = sharedHeld.Origin;
        var held = sharedHeld.Stack;

        if (origin.kind != ContainerKind.Inventory) return;

        int idx = origin.index;
        if (!inventoryGrid.IsValidIndex(idx))
        {
            sharedHeld.Clear();
            return;
        }
        var cur = inventoryGrid.Get(idx);

        if (cur.IsEmpty)
        {
            inventoryGrid.Set(idx, held);
            sharedHeld.Clear();
            return;
        }

        if(cur.itemId == held.itemId)
        {
            int maxStack = GetMaxStack(held.itemId);
            int space = maxStack - cur.amount;
            if(space <= 0)
            {
                int empty = FindFirstEmptyIndex();
                if(empty >= 0)
                {
                    inventoryGrid.Set(empty, held);
                    sharedHeld.Clear();
                }
                return;
            }

            int move = Mathf.Min(space, held.amount);
            cur.amount += move;
            held.amount -= move;

            inventoryGrid.Set(idx, cur);

            if (held.amount <= 0) sharedHeld.Clear();
            else sharedHeld.Pick(held, origin);
            return;
        }

        int e2 = FindFirstEmptyIndex();
        if(e2>= 0)
        {
            inventoryGrid.Set(e2, held);
            sharedHeld.Clear();
        }
    }
    #endregion

    public bool TryGetDef(int itemId, out ItemDef def)
    {
        def = null;
        return ready && itemDb != null && itemDb.TryGet(itemId, out def);
    }

    public void DropHeld()
    {
        if (!sharedHeld.HasItem) return;
        Debug.Log($"[InventorySystem] DropHeld itemId = {sharedHeld.Stack.itemId} amount = {sharedHeld.Stack.amount}");
        sharedHeld.Clear();
    }


    public void SetActiveSlot(int slotIndex)
    {
        if (!ready) return;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return;

        var stack = inventoryGrid.Get(slotIndex);

        //빈 슬롯이면 활성 해제
        if (stack.IsEmpty)
        {
            ActiveSlotIndex = -1;
            OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
            return;
        }

        ActiveSlotIndex = slotIndex;
        OnActiveSlotChanged?.Invoke(ActiveSlotIndex);

        // 플레이어가 "손에 듦" 상태를 바꾸는 트리거로만 사용
        if (itemDb != null && itemDb.TryGet(stack.itemId, out var def))
            OnActivateRequested?.Invoke(def);
    }

    public bool TryGetActive(out int slotIndex, out ItemStack stack, out ItemDef def)
    {
        slotIndex = ActiveSlotIndex;
        stack = default;
        def = null;

        if (!ready) return false;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return false;

        stack = inventoryGrid.Get(slotIndex);
        if (stack.IsEmpty) return false;

        return itemDb != null && itemDb.TryGet(stack.itemId, out def);
    }

    public void ClearActiveSlot()
    {
        if (ActiveSlotIndex < 0) return;

        ActiveSlotIndex = -1;
        OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
    }

    public void ToggleActiveSlot(int slotIndex)
    {
        if (!ready) return;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return;

        //같은 슬롯을 다시 누르면 비활성화
        if(ActiveSlotIndex == slotIndex)
        {
            ClearActiveSlot();
            return;
        }

        var stack = inventoryGrid.Get(slotIndex);

        //빈 슬롯이면 활성 해제
        if (stack.IsEmpty)
        {
            ClearActiveSlot();
            return;
        }

        //새 슬롯 활성화
        ActiveSlotIndex = slotIndex;
        OnActiveSlotChanged?.Invoke(ActiveSlotIndex);
        
        //손에 듦 트리거
        if(itemDb != null && itemDb.TryGet(stack.itemId, out var def))
            OnActivateRequested?.Invoke(def);
    }

    public bool ConsumeAt(int slotIndex, int amount)
    {
        if (!ready) return false;
        if (!inventoryGrid.IsValidIndex(slotIndex)) return false;
        if (amount <= 0) return false;

        var s = inventoryGrid.Get(slotIndex);
        if (s.IsEmpty) return false;
        if (s.amount < amount) return false;

        s.amount -= amount;
        if (s.amount <= 0) inventoryGrid.Set(slotIndex, default);
        else inventoryGrid.Set(slotIndex, s);

        return true;
    }

    public int TryAddFromExternal(int itemId, int amount, int quality = 0)
    {
        if (!ready) return amount;
        if (amount <= 0) return 0;
        if (itemDb == null) return amount;

        if (!itemDb.TryGet(itemId, out var def))
            return amount;

        int maxStack = Mathf.Max(1, def.maxStack);

        int remain = inventoryGrid.TryAdd(itemId, amount, maxStack, autoAddOrder, quality);

        int added = amount - remain;
        if (added > 0)
            OnItemAdded?.Invoke(itemId, added);

        return remain;
    }


    #region

    /// <summary>
    /// 특정 아이템의 총 개수 세기
    /// </summary>
    /// <param name="itemId">확인할 아이템 ID</param>
    /// <returns>인벤토리에 있는 해당 아이템의 총 개수</returns>
    public int CountItem(int itemId)
    {
        if (!ready) return 0;

        int totalCount = 0;

        // 모든 슬롯을 순회하면서 해당 아이템 개수 세기
        for (int i = 0; i < inventoryGrid.slotCount; i++)
        {
            var stack = inventoryGrid.Get(i);
            if (!stack.IsEmpty && stack.itemId == itemId)
            {
                totalCount += stack.amount;
            }
        }

        return totalCount;
    }

    /// <summary>
    /// 특정 아이템 제거하기
    /// </summary>
    /// <param name="itemId">제거할 아이템 ID</param>
    /// <param name="amount">제거할 개수</param>
    /// <returns>성공하면 true, 아이템이 부족하면 false</returns>
    public bool RemoveItem(int itemId, int amount)
    {
        if (!ready) return false;
        if (amount <= 0) return true; // 0개 제거는 성공으로 처리

        // 먼저 충분한 개수가 있는지 확인
        int totalCount = CountItem(itemId);
        if (totalCount < amount)
        {
            Debug.LogWarning($"[InventorySystem] 아이템 부족: itemId={itemId}, 필요={amount}, 보유={totalCount}");
            return false;
        }

        // 제거 시작 (뒤에서부터 제거)
        int remainToRemove = amount;

        for (int i = inventoryGrid.slotCount - 1; i >= 0 && remainToRemove > 0; i--)
        {
            var stack = inventoryGrid.Get(i);

            if (!stack.IsEmpty && stack.itemId == itemId)
            {
                int removeFromThisSlot = Mathf.Min(stack.amount, remainToRemove);

                stack.amount -= removeFromThisSlot;
                remainToRemove -= removeFromThisSlot;

                // 슬롯이 비었으면 비우기
                if (stack.amount <= 0)
                {
                    inventoryGrid.Set(i, default);
                }
                else
                {
                    inventoryGrid.Set(i, stack);
                }
            }
        }

        Debug.Log($"[InventorySystem] 아이템 제거: itemId={itemId}, amount={amount}");
        return true;
    }

    public bool TryRemoveFromSlot(int slotIndex, int amount)
    {
        if (!IsReady) return false;
        if (amount <= 0) return false;

        // ContainerGrid는 Count가 아니라 slotCount 사용
        if (slotIndex < 0 || slotIndex >= inventoryGrid.slotCount) return false;

        var stack = inventoryGrid.Get(slotIndex);
        if (stack.IsEmpty) return false;
        if (stack.amount < amount) return false;

        int nextAmount = stack.amount - amount;

        if (nextAmount <= 0)
            inventoryGrid.Set(slotIndex, default);
        else
            inventoryGrid.Set(slotIndex, new ItemStack(stack.itemId, nextAmount, stack.quality));

        // SaveNow()는 현재 InventorySystem에 없으므로 호출 제거
        return true;
    }

    #endregion


    //테스트용
    private void Update()
    {
        if (!ready) return;

        // P누르면 각종 아이템 얻기 테스트
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            TryPickup(1001, 10);
            TryPickup(1002, 10);
            TryPickup(3001, 10);
            TryPickup(1003, 10);
            TryPickup(1004, 10);
            TryPickup(3003, 10);
            TryPickup(8001, 10);
            TryPickup(8002, 10);
            TryPickup(8003, 10);
            TryPickup(1005, 10);
            TryPickup(2001, 10);
            TryPickup(1006, 10);
            TryPickup(1007, 10);
        }
        
    }
}
