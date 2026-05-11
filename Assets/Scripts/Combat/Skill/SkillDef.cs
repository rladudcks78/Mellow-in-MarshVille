using System;

[Serializable]
public class SkillDef
{
    public string weaponType;
    public int level;
    public string skillName;

    public float damageMultiplier;
    public float cooldownSec;

    public float castLockSec;
    public float hitDelaySec;

    public float overrideOffset;
    public float overrideBoxW;
    public float overrideBoxH;
}
