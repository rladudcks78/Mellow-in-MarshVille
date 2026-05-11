using System;
using System.Collections.Generic;
using UnityEngine;

public class ContainerGrid
{
    public event Action<int> OnSlotChanged;
    public event Action OnRebuilt;

    private ItemStack[] slots;

    public int slotCount => slots.Length;

    public bool IsEmpty(int index) => slots[index].IsEmpty;

    public bool TryTake(int index, out ItemStack taken)
    {
        taken = slots[index];
        if (taken.IsEmpty) return false;

        slots[index] = ItemStack.empty;
        OnSlotChanged?.Invoke(index);
        return true;
    }

    public ContainerGrid(int slotCount)
    {
        slots = new ItemStack[slotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = ItemStack.empty;
    }

    public ItemStack Get(int index) => slots[index];

    public void Set(int index, ItemStack value)
    {
        slots[index] = value;
        OnSlotChanged?.Invoke(index);
    }

    public void Clear(int index)
    {
        slots[index] = ItemStack.empty;
        OnSlotChanged?.Invoke(index);
    }

    public bool IsValidIndex(int index) => index >= 0 && index < slots.Length;

    public void ReSize(int newSize)
    {
        var newSlots = new ItemStack[newSize];
        for (int i = 0; i < newSlots.Length; i++) newSlots[i] = ItemStack.empty;

        int copy = Mathf.Min(slots.Length, newSize);
        Array.Copy(slots, newSlots, copy);

        slots = newSlots;
        OnRebuilt?.Invoke();
    }

    /// <summary>
    /// 자동 수납 + 스택 쌓기
    /// - 같은 아이템 스택이 있으면 먼저 채우기
    /// - 그 다음 빈 슬롯에 새 스택을 만들기
    /// - order 순서대로 진행 (2~4행 후 핫바)
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="amount"></param>
    /// <param name="maxStack"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    public int TryAdd(int itemId, int amount, int maxStack, IList<int> order, int quality = 0)
    {
        if (amount <= 0) return 0;
        if (itemId <= 0) return amount;
        if (maxStack <= 0) maxStack = 1;

        // 기존 스택 먼저 채우기
        for (int i = 0; i < order.Count; i++)
        {
            int idx = order[i];
            if (!IsValidIndex(idx)) continue;

            var s = slots[idx];
            if(s.IsEmpty) continue;
            if (s.itemId != itemId) continue;
            if (s.quality != quality) continue;
            if (s.amount >= maxStack) continue;

            int space = maxStack - s.amount;
            int add = Mathf.Min(space, amount);

            s.amount += add;
            slots[idx] = s;
            OnSlotChanged?.Invoke(idx);

            amount -= add;
            if (amount <= 0) return 0;
        }

        // 빈 슬롯에 새 스택 만들기
        for (int i = 0; i < order.Count; i++)
        {
            int idx = order[i];
            if ((!IsValidIndex(idx))) continue;

            var s = slots[idx];
            if (!s.IsEmpty) continue;

            int put = Mathf.Min(maxStack, amount);
            slots[idx] = new ItemStack(itemId, put, quality);
            OnSlotChanged?.Invoke(idx);

            amount -= put;
            if(amount <= 0) return 0;
        }

        //남은 수량
        return amount;
    }

    // 특정 수량만큼 아이템을 제거하는 함수
    public bool TryRemove(int index, int amount)
    {
        if (!IsValidIndex(index)) return false;
        if (slots[index].IsEmpty) return false;
        if (slots[index].amount < amount) return false;

        var s = slots[index];
        s.amount -= amount;

        if (s.amount <= 0)
        {
            slots[index] = ItemStack.empty;
        }
        else
        {
            slots[index] = s;
        }

        OnSlotChanged?.Invoke(index);
        return true;
    }
}
