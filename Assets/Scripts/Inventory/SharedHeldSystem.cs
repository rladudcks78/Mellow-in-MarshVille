using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class SharedHeldSystem : MonoBehaviour
{
    public static SharedHeldSystem Instance { get; private set; }

    public event Action OnChanged;

    [SerializeField] private bool _hasItem;
    [SerializeField] private ItemStack _stack;
    [SerializeField] private SlotRef _origin;

    public ItemStack Stack => _stack;
    public SlotRef Origin => _origin;
    public bool HasItem => _hasItem && !_stack.IsEmpty;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    public void Pick(ItemStack stack, SlotRef origin)
    {
        _stack = stack;
        _origin = origin;
        _hasItem = !_stack.IsEmpty;
        OnChanged?.Invoke();
    }

    public void SetAmount(int newAmount)
    {
        _stack.amount = newAmount;

        if(_stack.amount <= 0)
        {
            Clear();
            return;
        }

        _hasItem = true;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        _hasItem = false;
        _stack = default;
        _origin = default;
        OnChanged?.Invoke();
    }
}
