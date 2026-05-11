using System;

[Serializable]
public struct ItemStack
{
    public int itemId;
    public int amount;
    public int quality;

    public bool IsEmpty => itemId <= 0 || amount <= 0;

    public static ItemStack empty => new ItemStack { itemId = 0, amount = 0, quality = 0 };

    public ItemStack(int id, int amt, int q = 0)
    {
        itemId = id;
        amount = amt;
        quality = q;
    }
}
