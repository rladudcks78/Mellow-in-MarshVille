using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private CombatSystem combatSystem;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private SkillLoader skillLoader;
    [SerializeField] private WeaponSkillProgress progress;

    private SkillDatabase skillDb;

    private readonly Dictionary<WeaponMatchUp.WeaponType, float> nextSkillTime = new();
    private Coroutine castRoutine;

    public event Action<float> OnSkillCooldownStarted;      //HUD에 알려주기용

    private void Awake()
    {
        if (inventorySystem == null) inventorySystem = FindAnyObjectByType<InventorySystem>();
        if (combatSystem == null) combatSystem = GetComponent<CombatSystem>();
        if (combatSystem == null) combatSystem = FindAnyObjectByType<CombatSystem>();
        if (playerMove == null) playerMove = GetComponent<PlayerMove>();
        if (skillLoader == null) skillLoader = FindAnyObjectByType<SkillLoader>();
        if (progress == null) progress = FindAnyObjectByType<WeaponSkillProgress>();
    }

    private void OnEnable()
    {
        if (skillLoader != null)
            skillLoader.Register(OnSkillDbLoaded, OnSkillDbFailed);
    }
    private void OnDisable()
    {
        if (skillLoader != null)
            skillLoader.Unregister(OnSkillDbLoaded, OnSkillDbFailed);
    }

    private void OnSkillDbLoaded(SkillDatabase db)
    {
        skillDb = db;
        print("[SkillSystem] SkillDatabase 준비 완료");
    }

    private void OnSkillDbFailed(string err)
    {
        print($"[SkillSystem] SkillDatabase 로드 실패 : {err}");
        skillDb = null;
    }

    public bool TryUseSkill(Vector3 playerPos, Vector2 facingDir, ItemDef weaponDef)
    {
        if (weaponDef == null) return false;
        if (combatSystem == null) return false;
        if (combatSystem.IsAttacking) return false;

        var w = WeaponMatchUp.Parse(weaponDef.weaponType);
        if (w == WeaponMatchUp.WeaponType.None) return false;

        int unlocked = (progress != null) ? progress.GetUnlockedLevel(w) : 1;
        unlocked = Mathf.Clamp(unlocked, 1, 5);

        // DB에서 스킬 가져오기 (없으면 fallback)
        SkillDef def = null;
        if (skillDb != null)
        {
            skillDb.TryGet(weaponDef.weaponType, unlocked, out def);
        }

        // fallback (시트 아직 없거나 row 누락)
        float damageMul = (def != null) ? def.damageMultiplier : 3f;
        float cooldown = (def != null) ? def.cooldownSec : 2f;
        float castLock = (def != null) ? def.castLockSec : 0.35f;
        float hitDelay = (def != null) ? def.hitDelaySec : 0.10f;

        // 쿨타임
        float now = Time.time;
        if (nextSkillTime.TryGetValue(w, out float next) && now < next)
            return false;

        // 오버라이드 값 처리
        float? overrideOffset = null;
        Vector2? overrideBox = null;

        if (def != null)
        {
            if (def.overrideOffset > 0f) overrideOffset = def.overrideOffset;
            if (def.overrideBoxW > 0f && def.overrideBoxH > 0f)
                overrideBox = new Vector2(def.overrideBoxW, def.overrideBoxH);
        }

        bool started = combatSystem.TryPerformSkillAttack(
            playerPos, facingDir, weaponDef,
            damageMul,
            hitDelay,
            overrideOffset,
            overrideBox
        );

        if (!started) return false;

        nextSkillTime[w] = now + Mathf.Max(0f, cooldown);

        OnSkillCooldownStarted?.Invoke(Mathf.Max(0f, cooldown));

        if (castRoutine != null) StopCoroutine(castRoutine);
        castRoutine = StartCoroutine(CastLockRoutine(castLock));

        return true;
    }
    
    private IEnumerator CastLockRoutine(float lockSec)
    {
        if (playerMove == null || lockSec <= 0f) yield break;

        bool wasEnabled = playerMove.enabled;
        if (wasEnabled) playerMove.enabled = false;

        float t = 0f;
        while (t < lockSec)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (wasEnabled) playerMove.enabled = true;
    }

}