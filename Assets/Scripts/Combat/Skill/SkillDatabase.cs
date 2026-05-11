using System.Collections.Generic;
using System;

public class SkillDatabase
{
    private readonly Dictionary<(string weaponType, int level), SkillDef> _map = new();

    public int count => _map.Count;

    public void Build(IEnumerable<SkillDef> defs)
    {
        _map.Clear();

        foreach(var def in defs)
        {
            if (def == null) continue;
            if (string.IsNullOrWhiteSpace(def.weaponType)) throw new Exception("Invalid weaponType");
            if (def.level < 1 || def.level > 5) throw new Exception($"Invalid skill level : {def.level}");

            var key = (def.weaponType.Trim().ToLowerInvariant(), def.level);
            _map[key] = def;

        }
    }

    public bool TryGet(string weaponTypeRaw, int level, out SkillDef def)
    {
        def = null;
        if (string.IsNullOrWhiteSpace(weaponTypeRaw)) return false;
        var key = (weaponTypeRaw.Trim().ToLowerInvariant(), level);
        return _map.TryGetValue(key, out def);
    }
}
