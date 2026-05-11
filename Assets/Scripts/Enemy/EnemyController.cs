using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyData(SO)를 기반으로 몬스터의 AI(FSM)와 전투를 처리하는 컨트롤러입니다.
/// </summary>
public class EnemyController : MonoBehaviour, IDamageable
{
    // -----------------------------------------------------------
    // 1. 상태 및 타겟 레이어 정의
    // -----------------------------------------------------------
    public enum EnemyState
    {
        Idle,       // 배회
        Chase,      // 추적
        Attack,     // 공격 (선딜레이 및 판정)
        Hit,        // 피격 (넉백 및 경직)
        Return,     // 복귀
        Dead        // 사망
    }

    [Header("Settings")]
    [SerializeField] private EnemyData _data;         // 데이터 연동
    [SerializeField] private LayerMask _targetLayer;  // 공격 대상 레이어 (주로 Player)

    [Header("Debug Info (Read Only)")]
    [SerializeField] private EnemyState _currentState;
    [SerializeField] private float _currentHP;
    [SerializeField] private bool _hasAggro;            //선공 트리거

    [SerializeField] private float _arriveDistance = 0.15f; //스폰지점 도착 판정 거리

    // 내부 컴포넌트
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Transform _target;      // 플레이어
    private Vector3 _spawnPosition; // 초기 스폰 위치


    // 타이머 및 상태 제어
    private float _stateTimer;      // Idle, Hit 상태 타이머
    private float _lastAttackTime;  // 공격 쿨타임 체크
    private bool _isInvincible;     // 무적 상태 (연타 방지)

    private Vector2 _wanderDirection; // 현재 배회 방향

    public BodyType BodyTypeValue => _data != null ? _data.bodyType : BodyType.Medium;

    // -----------------------------------------------------------
    // 2. 초기화 (Awake / Start)
    // -----------------------------------------------------------
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_data != null)
        {
            _currentHP = _data.maxHP;
        }
        else
        {
            Debug.LogError("[EnemyController] EnemyData가 비어있습니다!");
        }

        _spawnPosition = transform.position;
        _currentState = EnemyState.Idle;
        _hasAggro = false;
    }

    private void Start()
    {
        EnsureTarget();

        //// 씬 내의 플레이어 찾기 (PlayerInteract 컴포넌트 기준)
        //var playerObj = FindFirstObjectByType<PlayerInteract>();
        //if (playerObj != null)
        //    _target = playerObj.transform;
    }

    // -----------------------------------------------------------
    // 3. 메인 루프 (FixedUpdate)
    // -----------------------------------------------------------
    private void FixedUpdate()
    {
        if (_currentState == EnemyState.Dead) return;

        switch (_currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
            case EnemyState.Hit:
                UpdateHitState();
                break;
            case EnemyState.Return:
                UpdateReturnState();
                break;
        }
    }

    //플레이어(타겟) 찾기
    private void EnsureTarget()
    {
        if (_target != null) return;

        var pm = FindFirstObjectByType<PlayerMove>();
        if(pm != null)
        {
            _target = pm.transform;
            return;
        }

        var ph = FindFirstObjectByType<PlayerHealth>();
        if(ph != null)
        {
            _target = ph.transform;
            return;
        }
    }

    public void ForceAggro()
    {
        _hasAggro = true;
        EnsureTarget();

        if (_currentState == EnemyState.Dead) return;

        //Idle/ Return 상태면 즉시 추적으로 진입
        if (_currentState == EnemyState.Idle || _currentState == EnemyState.Return)
            _currentState = EnemyState.Chase;
    }

    // -----------------------------------------------------------
    // 4. 상태별 로직 구현
    // -----------------------------------------------------------

    // [Idle] 배회 상태
    private void UpdateIdleState()
    {
        if (_hasAggro)
        {
            _currentState = EnemyState.Chase;
            return;
        }

        _stateTimer -= Time.fixedDeltaTime;

        // 일정 시간마다 이동 방향 변경
        if (_stateTimer <= 0)
        {
            _stateTimer = Random.Range(2f, 4f);
            // 50% 확률로 정지 혹은 이동
            if (Random.value < 0.5f) _wanderDirection = Vector2.zero;
            else _wanderDirection = Random.insideUnitCircle.normalized;
        }

        // 스폰 위치 벗어나면 복귀
        if (Vector3.Distance(transform.position, _spawnPosition) > _data.wanderRadius)
        {
            _wanderDirection = (_spawnPosition - transform.position).normalized;
        }

        // 실제 이동
        _rb.linearVelocity = _wanderDirection * _data.patrolSpeed;
        FlipSprite(_wanderDirection.x);

        // 선공형 몬스터라면 여기서 감지 로직 추가 가능 (detectionRange 사용)
    }

    // [Chase] 추적 상태
    private void UpdateChaseState()
    {
        EnsureTarget();
        if (_target == null)
        {
            _currentState = EnemyState.Idle;
            _hasAggro = false;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, _target.position);
        float playerToSpawn = Vector3.Distance(_target.position, _spawnPosition);

        //스폰 기준 리쉬 : 플레이어가 스폰에서 일정 거리 이상 이탈하면 끊김
        if(playerToSpawn > _data.giveUpRange)
        {
            BeginReturn();
            return;
        }

        if (_data.detectionRange > 0f && distToPlayer > _data.detectionRange)
        {
            BeginReturn();
            return;
        }

        //공격 사거리면 Attack 전환
        if(distToPlayer <= _data.attackRange)
        {
            _currentState = EnemyState.Attack;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dir = (_target.position - transform.position).normalized;
        _rb.linearVelocity = dir * _data.chaseSpeed;
        FlipSprite(dir.x);
    }

    // [Attack] 공격 대기 상태
    private void UpdateAttackState()
    {
        EnsureTarget();
        if (_target == null)
        {
            BeginReturn();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, _target.position);
        float playerToSpawn = Vector3.Distance(_target.position, _spawnPosition);
        if(playerToSpawn > _data.giveUpRange)
        {
            BeginReturn();
            return;
        }

        if(_data.detectionRange > 0f && distToPlayer > _data.detectionRange)
        {
            BeginReturn();
            return;
        }

        if(distToPlayer > _data.attackRange)
        {
            _currentState = EnemyState.Chase;
            return;
        }

        if(Time.time >= _lastAttackTime + _data.attackRate)
        {
            _lastAttackTime = Time.time;
            PerformAttack();
        }
    }

    // [Hit] 피격(넉백) 상태
    private void UpdateHitState()
    {
        _stateTimer -= Time.fixedDeltaTime;

        // 경직 시간 끝나면 추적 재개
        if (_stateTimer <= 0)
        {
            _rb.linearVelocity = Vector2.zero;
            //피격 후엔 aggro가 켜져있으니 Chase로 바꿈
            _currentState = _hasAggro ? EnemyState.Chase : EnemyState.Idle;
        }
        // * 넉백 힘(Velocity)은 HitRoutine 시작 시 한번 가해지고 물리 엔진에 의해 자연스럽게 줄어듦
    }

    //[Return] 복귀 상태
    private void UpdateReturnState()
    {
        EnsureTarget();

        //복귀중에도 플레이어가 몬스터 감지 반경으로 들어오면 다시 추적
        if(_target != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, _target.position);
            float playerToSpawn = Vector3.Distance(_target.position, _spawnPosition);

            if(_hasAggro &&
                playerToSpawn <= _data.giveUpRange &&
                (_data.detectionRange <= 0f || distToPlayer <= _data.detectionRange))
            {
                _currentState = EnemyState.Chase;
                return;
            }
        }

        //스폰으로 이동
        Vector2 dir = (_spawnPosition - transform.position).normalized;
        _rb.linearVelocity = dir * _data.patrolSpeed;
        FlipSprite(dir.x);

        //도착하면 전투 종료 + 풀피
        if (Vector3.Distance(transform.position, _spawnPosition) <= _arriveDistance)
            ResetCombatToSpawn();
    }

    private void BeginReturn()
    {
        _currentState = EnemyState.Return;
        _rb.linearVelocity = Vector2.zero;
    }

    private void ResetCombatToSpawn()
    {
        _rb.linearVelocity = Vector2.zero;

        //스폰 도착 즉시 풀피
        _currentHP = _data.maxHP;

        //전투 종료
        _hasAggro = false;
        _isInvincible = false;
        if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

        //Idle로 복귀
        _wanderDirection = Vector2.zero;
        _stateTimer = 0f;
        _currentState = EnemyState.Idle;
    }

    // -----------------------------------------------------------
    // 5. 공격 실행 (Box Hitbox Logic)
    // -----------------------------------------------------------
    private void PerformAttack()
    {
        // 쿨타임 갱신
        // (참고: 애니메이션이 있다면 애니메이션 길이를 고려해 코루틴으로 처리하는 것이 좋습니다)
        // 여기서는 즉발 공격으로 구현합니다.

        // 1. 공격 방향 계산 (타겟 쪽)
        Vector2 attackDir = Vector2.right;
        if (_target != null)
            attackDir = (_target.position - transform.position).normalized;

        // 2. 히트 박스 중심점 계산 (Data의 Offset 활용)
        Vector2 hitCenter = (Vector2)transform.position + (attackDir * _data.attackOffset);

        // 3. 물리 연산으로 충돌체 감지 (OverlapBox)
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitCenter, _data.attackBoxSize, 0, _targetLayer);

        if (hits.Length > 0)
        {
            Debug.Log($"<color=red>[Enemy Attack] {_data.enemyName} 휘두르기!</color>");
            foreach (var hit in hits)
            {
                // 플레이어(IDamageable)에게 데미지 전달
                if (hit.TryGetComponent<IDamageable>(out IDamageable targetHealth))
                {
                    targetHealth.TakeDamage(_data.attackDamage);
                    Debug.Log($"적중 대상: {hit.name} / 데미지: {_data.attackDamage}");
                }
            }
        }
        else
        {
            // 허공에 휘두름
        }
    }

    // -----------------------------------------------------------
    // 6. 피격 처리 (IDamageable 구현)
    // -----------------------------------------------------------
    public void TakeDamage(float damage)
    {
        if (_currentState == EnemyState.Dead || _isInvincible) return;

        ForceAggro();

        _currentHP -= damage;

        // 피격 사운드
        // if (_data.hitSound) AudioSource.PlayClipAtPoint(_data.hitSound, transform.position);

        if (_currentHP <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(HitRoutine());
    }

    private IEnumerator HitRoutine()
    {
        _isInvincible = true;
        _currentState = EnemyState.Hit;
        _stateTimer = 0.3f; // 0.3초간 경직
        if(_spriteRenderer != null) _spriteRenderer.color = Color.red;

        // 넉백 방향 계산 (플레이어 반대쪽)
        Vector2 knockbackDir = Vector2.zero;
        if(_target != null)
            knockbackDir = (transform.position - _target.position).normalized;

        // 기존 이동 관성 초기화 후 넉백 힘 적용
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(knockbackDir * _data.knockbackForce, ForceMode2D.Impulse);

        // 무적 시간 (연타 방지)
        yield return new WaitForSeconds(0.2f);

        if(_spriteRenderer != null) _spriteRenderer.color = Color.white;
        _isInvincible = false;
    }

    private void Die()
    {
        _currentState = EnemyState.Dead;
        _rb.linearVelocity = Vector2.zero;
        Debug.Log($"<color=gray>{_data.enemyName} 사망.</color>");

        // 사운드
        // if (_data.dieSound) AudioSource.PlayClipAtPoint(_data.dieSound, transform.position);

        CalculateLoot();
        Destroy(gameObject);
    }

    private void CalculateLoot()
    {
        // 확정 드랍
        if (_data.guaranteedDrops != null)
        {
            foreach (var loot in _data.guaranteedDrops)
            {
                int count = Random.Range(loot.minAmount, loot.maxAmount + 1);
                Debug.Log($"[Drop-확정] ItemID: {loot.itemID}, 개수: {count}");
            }
        }

        // 확률 드랍
        if (_data.chanceDrops != null)
        {
            foreach (var loot in _data.chanceDrops)
            {
                float roll = Random.Range(0f, 100f);
                if (roll <= loot.dropChance)
                {
                    int count = Random.Range(loot.minAmount, loot.maxAmount + 1);
                    Debug.Log($"<color=cyan>[Drop-당첨] ItemID: {loot.itemID}, 개수: {count}</color>");
                }
            }
        }
    }

    // -----------------------------------------------------------
    // 7. 유틸리티 및 기즈모
    // -----------------------------------------------------------
    private void FlipSprite(float xDir)
    {
        if (Mathf.Abs(xDir) > 0.1f)
        {
            Vector3 scale = transform.localScale;
            scale.x = (xDir < 0) ? 1 : -1; // 원본 이미지 방향에 따라 조정
            transform.localScale = scale;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        // 1. 배회 및 감지 범위
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPosition : transform.position, _data.wanderRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _data.giveUpRange);

        // 2. 공격 시작 거리 (AI Trigger)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _data.attackRange);

        // 3. 실제 공격 타격 박스 (Hitbox)
        Gizmos.color = new Color(1, 0, 0, 0.5f);

        Vector3 aimDir = Vector3.right;
        // 게임 중이면 타겟 방향, 에디터면 오른쪽 기준
        if (Application.isPlaying && _target != null)
            aimDir = (_target.position - transform.position).normalized;

        Vector3 boxCenter = transform.position + (aimDir * _data.attackOffset);
        Gizmos.DrawWireCube(boxCenter, _data.attackBoxSize);
    }
}