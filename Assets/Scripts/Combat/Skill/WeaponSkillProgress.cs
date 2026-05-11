using System;
using UnityEngine;

public class WeaponSkillProgress : MonoBehaviour
{
    public event Action OnChanged;

    [Header("Unlocked Skill Level (1~5)")]
    [Range(1, 5)] [SerializeField] private int spatulaUnlocked = 1;
    [Range(1, 5)] [SerializeField] private int scoopUnlocked = 1;
    [Range(1, 5)] [SerializeField] private int whiskUnlocked = 1;

    private const int MinLevel = 1;
    private const int MaxLevel = 5;

    public int GetUnlockedLevel(WeaponMatchUp.WeaponType type)
    {
        return type switch
        {
            WeaponMatchUp.WeaponType.Spatula => spatulaUnlocked,
            WeaponMatchUp.WeaponType.Scoop => scoopUnlocked,
            WeaponMatchUp.WeaponType.Whisk => whiskUnlocked,
            _ => MinLevel
        };
    }

    public void SetUnlockedLevel(WeaponMatchUp.WeaponType weaponType, int level, bool invokeEvent = true)
    {
        level = Mathf.Clamp(level, MinLevel, MaxLevel);
        bool changed = false;

        switch (weaponType)
        {
            case WeaponMatchUp.WeaponType.Spatula:
                if (spatulaUnlocked != level) { spatulaUnlocked = level; changed = true; }
                break;

            case WeaponMatchUp.WeaponType.Scoop:
                if (scoopUnlocked != level) { scoopUnlocked = level; changed = true; }
                break;

            case WeaponMatchUp.WeaponType.Whisk:
                if (whiskUnlocked != level) { whiskUnlocked = level; changed = true; }
                break;

            default:
                return;
        }

        if (changed && invokeEvent)
            OnChanged?.Invoke();
    }

    public bool TryUnlockLevel(WeaponMatchUp.WeaponType weaponType, int level, bool invokeEvent = true)
    {
        level = Mathf.Clamp(level, MinLevel, MaxLevel);

        int cur = GetUnlockedLevel(weaponType);
        if (level <= cur) return false;

        SetUnlockedLevel(weaponType, level, invokeEvent);
        return true;
    }

    public bool TryUpgradeOneStep(WeaponMatchUp.WeaponType weaponType, bool invokeEvent = true)
    {
        int cur = GetUnlockedLevel(weaponType);
        if (cur >= MaxLevel) return false;

        SetUnlockedLevel(weaponType, cur + 1, invokeEvent);
        return true;
    }

    public bool IsUnlocked(WeaponMatchUp.WeaponType weaponType, int skillLevel)
    {
        skillLevel = Mathf.Clamp(skillLevel, MinLevel, MaxLevel);
        return GetUnlockedLevel(weaponType) >= skillLevel;
    }
}
