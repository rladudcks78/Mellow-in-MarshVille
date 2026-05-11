using UnityEngine;

public static class WeaponMatchUp
{
    public enum WeaponType
    {
        None,
        Spatula,
        Scoop,
        Whisk
    }

    public static WeaponType Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return WeaponType.None;

        string s = raw.Trim().ToLowerInvariant();

        return s switch
        {
            "spatula"   => WeaponType.Spatula,
            "scoop"     => WeaponType.Scoop,
            "whisk"     => WeaponType.Whisk,
            _           => WeaponType.None
        }; 
    }

    public static float GetMultiplier(string weaponTypeRaw, BodyType bodyType)
    {
        var w = Parse(weaponTypeRaw);
        if (w == WeaponType.None) return 0f;

        return (w, bodyType) switch
        {
            (WeaponType.Scoop,      BodyType.Hard)     => 1.5f,
            (WeaponType.Spatula,    BodyType.Medium)   => 1.5f,
            (WeaponType.Whisk,      BodyType.Soft)     => 1.5f,

            (WeaponType.Spatula,    BodyType.Hard)     => 1.0f,
            (WeaponType.Whisk,      BodyType.Medium)   => 1.0f,
            (WeaponType.Scoop,      BodyType.Soft)     => 1.0f,

            (WeaponType.Whisk,      BodyType.Hard)     => 0.5f,
            (WeaponType.Scoop,      BodyType.Medium)   => 0.5f,
            (WeaponType.Spatula,    BodyType.Soft)     => 0.5f,

            _ => 1.0f
        };
    }
}
