using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatSystem : MonoBehaviour
{
    [Header("Data References")]
    [SerializeField] private PlayerData _playerData; // 스탯 데이터
    [SerializeField] private InventorySystem _inventorySystem;  //인벤토리 시스템
    [SerializeField] private LayerMask _enemyLayer;  // 몬스터 레이어 

    // 기즈모에서 바라보는 방향을 알기 위해 PlayerMove 참조
    private PlayerMove _playerMove;

    private bool _isAttacking;
    private float _nextAttackTime;

    public bool IsAttacking => _isAttacking;

    private void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        if (_inventorySystem == null)
            _inventorySystem = FindFirstObjectByType<InventorySystem>();
    }

    /// <summary>
    /// InteractionController에서 호출되는 공격 진입점
    /// </summary>
    public void PerformAttack(Vector3 playerPos, Vector2 facingDir)
    {
        //인벤토리/DB 준비 안됐으면 공격 처리 안하기
        if (_inventorySystem == null || !_inventorySystem.IsReady)
        {
            print("[CombatSystem] InventorySystem 준비 전 - 공격 무시");
            return;
        }

        //현재 활성 아이템(핫바 or 인벤토리 우클릭 사용하기) 가져오기
        if (!TryGetActiveItem(out var activeDef))
        {
            //활성 아이템 자체가 없으면 데미지 0
            print("[CombatSystem] 활성 아이템 없음 -> 데미지 0");
            return;
        }

        //weaponType 빈칸이면 무기 아니니까 데미지 0
        if (string.IsNullOrWhiteSpace(activeDef.weaponType))
        {
            print($"[CombatSystem] 무기 아님 : {activeDef.name}");
            return;
        }

        float atkSpeed = Mathf.Max(0.001f, activeDef.attackSpeed <= 0f ? 1f : activeDef.attackSpeed);
        float cooldown = 1f / atkSpeed;

        if (Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + cooldown;

        if (_isAttacking) return;

        StartCoroutine(AttackRoutine(playerPos, facingDir, activeDef));
    }

    public bool TryPerformSkillAttack(
        Vector3 playerPos,
        Vector2 facingDir,
        ItemDef weaponDef,
        float damageMultiplier,
        float hitDelaySec,
        float? overrideOffset = null,
        Vector2? overrideBoxSize = null)
    {
        if (_isAttacking) return false;
        if (weaponDef == null) return false;
        if (string.IsNullOrWhiteSpace(weaponDef.weaponType)) return false;

        StartCoroutine(SkillRoutine(playerPos, facingDir, weaponDef, damageMultiplier, hitDelaySec, overrideOffset, overrideBoxSize));
        return true;
    }

    private bool TryGetActiveItem(out ItemDef def)
    {
        def = null;

        // ActiveSlotIndex 기반
        if (_inventorySystem != null && _inventorySystem.TryGetActive(out _, out _, out var activeDef))
        {
            def = activeDef;
            return def != null;
        }

        return false;
    }

    private IEnumerator AttackRoutine(Vector3 playerPos, Vector2 dir, ItemDef weaponDef)
    {
        _isAttacking = true;

        // 선딜레이
        if (_playerData != null && _playerData.attackDelay > 0f)
            yield return new WaitForSeconds(_playerData.attackDelay);

        //판정 박스 계산
        float offset = (_playerData != null) ? _playerData.attackOffset : 1f;
        Vector2 boxSize = (_playerData != null) ? _playerData.attackBoxSize : Vector2.one;

        Vector3 hitCenter = playerPos + (Vector3)dir * offset;

        Collider2D[] hitEnemies = Physics2D.OverlapBoxAll(hitCenter, boxSize, 0f, _enemyLayer);

        if (hitEnemies.Length > 0)
        {
            //동일 적이 collider 여러개면 중복 맞는거 방지
            var damaged = new HashSet<IDamageable>();

            foreach (var col in hitEnemies)
            {
                if (col == null) continue;

                //EnemyController가 부모에 붙어있는 케이스
                var enemyCtrl = col.GetComponentInParent<EnemyController>();
                BodyType bodyType = (enemyCtrl != null) ? enemyCtrl.BodyTypeValue : BodyType.Medium;

                if (!col.TryGetComponent<IDamageable>(out var dmg))
                {
                    //부모에 Idamagabled이 있을 수 있음
                    dmg = col.GetComponentInParent<IDamageable>();
                }

                if (dmg == null) continue;
                if (damaged.Contains(dmg)) continue;

                float multiplier = WeaponMatchUp.GetMultiplier(weaponDef.weaponType, bodyType);
                float finalDamage = weaponDef.attackDamage * multiplier;

                //weaponType 빈칸 혹시 모르니 한번 더 거르기
                if (finalDamage <= 0f)
                {
                    print($"[CombatSystem] 데미지 0 (weaponType = {weaponDef.weaponType}, bodyType = {bodyType}");
                    continue;
                }

                //공격시 전투 시작
                if (enemyCtrl != null)
                    enemyCtrl.ForceAggro();

                dmg.TakeDamage(finalDamage);
                damaged.Add(dmg);
                

                Debug.Log(
                    $"<color=red>[CombatHit]</color> enemy={col.name} body={bodyType} " +
                    $"weaponType={weaponDef.weaponType} base={weaponDef.attackDamage} x {multiplier} => {finalDamage}"
                    );
            }
        }
        _isAttacking = false;
    }

    private IEnumerator SkillRoutine(
        Vector3 playerPos,
        Vector2 dir,
        ItemDef weaponDef,
        float damageMultiplier,
        float hitDelaySec,
        float? overrideOffset,
        Vector2? overrideBoxSize)
    {
        _isAttacking = true;

        if (hitDelaySec > 0f) yield return new WaitForSeconds(hitDelaySec);

        DoHit(playerPos, dir, weaponDef, damageMultiplier, overrideOffset, overrideBoxSize, logTag: "Skill");

        _isAttacking = false;
    }

    private void DoHit(
        Vector3 playerPos,
        Vector2 dir,
        ItemDef weaponDef,
        float damageMulitplier,
        float? overrideOffset,
        Vector2? overrideBoxSize,
        string logTag)
    {
        float offset = overrideOffset ?? ((_playerData != null) ? _playerData.attackOffset : 1f);
        Vector2 boxSize = overrideBoxSize ?? ((_playerData != null) ? _playerData.attackBoxSize : Vector2.one);

        Vector3 hitCenter = playerPos + (Vector3)dir * offset;

        Collider2D[] hitEnemies = Physics2D.OverlapBoxAll(hitCenter, boxSize, 0f, _enemyLayer);

        if (hitEnemies.Length <= 0) return;

        var damaged = new HashSet<IDamageable>();

        foreach(var col in hitEnemies)
        {
            if (col == null) continue;

            var enemyCtrl = col.GetComponentInParent<EnemyController>();
            BodyType bodyType = (enemyCtrl != null) ? enemyCtrl.BodyTypeValue : BodyType.Medium;

            if (!col.TryGetComponent<IDamageable>(out var dmg))
                dmg = col.GetComponentInParent<IDamageable>();

            if (dmg == null) continue;
            if (damaged.Contains(dmg)) continue;

            float matchUp = WeaponMatchUp.GetMultiplier(weaponDef.weaponType, bodyType);
            float finalDamage = weaponDef.attackDamage * damageMulitplier * matchUp;

            if (finalDamage <= 0f) continue;

            if (enemyCtrl != null)
                enemyCtrl.ForceAggro();

            dmg.TakeDamage(finalDamage);
            damaged.Add(dmg);

            Debug.Log($"<color=red>[{logTag}Hit]</color> enemy = {col.name} body = {bodyType}" +
                $"weaponType = {weaponDef.weaponType} base = {weaponDef.attackDamage} x skill({damageMulitplier} x match({matchUp} => {finalDamage}"
                );
        }
    }

    // 에디터에서 공격 범위를 눈으로 확인하기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        if (_playerData == null) return;

        Gizmos.color = Color.red;
        // [수정] 게임 실행 중이면 실제 바라보는 방향을, 아니면 기본값(오른쪽)을 사용
        Vector2 drawDir = Vector2.down;

        if (Application.isPlaying && _playerMove != null)
        {
            drawDir = _playerMove.FacingDir;
        }

        // 계산된 방향으로 기즈모 박스 그리기
        Vector3 center = transform.position + (Vector3)drawDir * _playerData.attackOffset;
        Gizmos.DrawWireCube(center, _playerData.attackBoxSize);
    }
}