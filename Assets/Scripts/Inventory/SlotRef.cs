using System;

[Serializable]
public struct SlotRef
{
    public ContainerKind kind;
    public int index;

    public SlotRef(ContainerKind kind, int index)
    {
        this.kind = kind;
        this.index = index;
    }

    public bool IsValid => index >= 0;
}
