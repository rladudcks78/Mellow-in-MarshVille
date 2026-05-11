using System;
using UnityEngine;

/// <summary>
/// 플레이어 골드 어댑터
/// - PlayerData.gold(long)와 직접 연동
/// - Shop / UI에서는 이 클래스만 참조
/// </summary>
public class PlayerGold : MonoBehaviour
{
    [SerializeField] private PlayerData playerData;

    public event Action<long> OnGoldChanged;

    public long CurrentGold
    {
        get
        {
            EnsureRef();
            return playerData != null ? playerData.gold : 0L;
        }
    }

    private void Awake()
    {
        EnsureRef();
    }

    private void EnsureRef()
    {
        if (playerData == null)
            playerData = FindAnyObjectByType<PlayerData>();
    }

    public bool HasEnough(long amount)
    {
        if (amount <= 0) return true;
        return CurrentGold >= amount;
    }

    public bool HasEnough(int amount)
    {
        return HasEnough((long)amount);
    }

    public bool TrySpend(long amount)
    {
        EnsureRef();

        if (playerData == null) return false;
        if (amount < 0) return false;
        if (playerData.gold < amount) return false;

        playerData.gold -= amount;
        OnGoldChanged?.Invoke(playerData.gold);
        return true;
    }

    public bool TrySpend(int amount)
    {
        return TrySpend((long)amount);
    }

    public void Add(long amount)
    {
        EnsureRef();

        if (playerData == null) return;
        if (amount <= 0) return;

        playerData.gold += amount;
        OnGoldChanged?.Invoke(playerData.gold);
    }

    public void Add(int amount)
    {
        Add((long)amount);
    }

    public void ForceRefreshEvent()
    {
        OnGoldChanged?.Invoke(CurrentGold);
    }
}