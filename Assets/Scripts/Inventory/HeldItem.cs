using System;
using UnityEngine;

[Serializable]
public struct HeldItem
{
    public ItemStack stack;         //손에 들린 아이템
    public SlotRef origin;     //원래 있던 슬롯 

    public bool HasItem => !stack.IsEmpty;

    public void Pick(ItemStack picked, SlotRef from)
    {
        stack = picked;
        origin = from;
    }

    public void Clear()
    {
        stack = ItemStack.empty;
        origin = new SlotRef(ContainerKind.Inventory, -1);
    }
}
