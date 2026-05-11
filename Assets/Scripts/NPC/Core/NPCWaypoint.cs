using UnityEngine;

/// <summary>
/// NPC Waypoint 이동 (최적화 버전)
/// - Manager에 등록되어 업데이트 모드 관리
/// - 카메라 밖/플레이어 멀리 있으면 간소화된 이동
/// - Rigidbody2D와 Transform 이동을 모드에 따라 구분해 물리/성능 균형
/// </summary>
public class NPCWaypoint : MonoBehaviour
{
    /// <summary>
    /// NPC 업데이트 모드 (Manager가 거리/가시성에 따라 설정)
    /// Precise: 플레이어 근처 → 매 프레임 물리 기반 정밀 이동 + 충돌 체크
    /// Normal: 카메라 안 → Manager 주기적 체크 + 기본 이동
    /// Simplified: 카메라 밖 → Transform 직접 이동 + 낮은 업데이트 빈도
    /// </summary>
    public enum UpdateMode
    {
        Precise,     // 정밀 (플레이어 근처)
        Normal,      // 보통 (카메라 안)
        Simplified   // 간소화 (카메라 밖)
    }

    [Header("경로 설정")]
    [SerializeField] private Transform[] waypoints;  // 순회할 경로 포인트들 (인스펙터에서 배열로 설정)
    [SerializeField] private bool loopPath = true;   // 경로 끝에 도달하면 처음으로 순환할지 여부

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 1.5f;      // Rigidbody2D 이동 속도 (Precise/Normal 모드)
    [SerializeField] private float arriveDistance = 0.1f; // waypoint 도착으로 간주할 거리

    [Header("대기 설정")]
    [SerializeField] private float waitTimeMin = 0.5f;   // waypoint 도착 후 최소 대기 시간
    [SerializeField] private float waitTimeMax = 3f;     // waypoint 도착 후 최대 대기 시간

    [Header("중간 멈춤 설정")]
    [SerializeField] private bool enableRandomPause = true;      // 이동 중 랜덤 멈춤 활성화
    [SerializeField] private float pauseCheckInterval = 2f;      // 멈춤 체크 간격
    [SerializeField][Range(0f, 1f)] private float pauseChance = 0.3f;  // 멈춤 발생 확률
    [SerializeField] private float pauseTimeMin = 0.5f;   // 랜덤 멈춤 최소 시간
    [SerializeField] private float pauseTimeMax = 2f;     // 랜덤 멈춤 최대 시간

    [Header("플레이어 감지")]
    [SerializeField] private float detectionRadius = 1f;  // 플레이어와 충돌 감지 반경
    [SerializeField] private LayerMask playerLayer;       // 플레이어 판정 LayerMask

    [Header("최적화 설정")]
    [SerializeField] private float simplifiedUpdateInterval = 0.5f;  // Simplified 모드 업데이트 간격

    // === 핵심 상태 ===
    private int currentWaypointIndex = 0;  // 현재 향하는 waypoint 인덱스
    private bool isWaiting = false;        // waypoint 도착 후 대기 중인지 여부
    private float waitTimer = 0f;          // 대기 타이머
    private bool isBlocked = false;        // 플레이어에 의해 이동 차단된 상태
    private float pauseCheckTimer = 0f;    // 랜덤 멈춤 체크 타이머

    // === 컴포넌트/캐시 ===
    private Rigidbody2D rb;                // 물리 이동용 (Precise/Normal 모드)
    private Vector2 initialPosition;       // NPC가 벗어나지 않도록 초기 위치 저장

    // === 최적화 관련 ===
    private UpdateMode currentMode = UpdateMode.Normal;  // Manager가 설정하는 현재 업데이트 모드
    private float simplifiedTimer = 0f;    // Simplified 모드용 타이머

    private void Start()
    {
        // Rigidbody2D 설정 (회전 고정)
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError($"{name}: Rigidbody2D가 없습니다!");
            enabled = false;
            return;
        }
        rb.freezeRotation = true;

        // 초기 위치 저장
        initialPosition = transform.position;

        // waypoint 검증
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError($"{name}: Waypoint가 설정되지 않았습니다!");
            enabled = false;
            return;
        }

        // 타이머 초기화 (랜덤 값으로 시작 지연 분산)
        pauseCheckTimer = Random.Range(0f, pauseCheckInterval);
        simplifiedTimer = Random.Range(0f, simplifiedUpdateInterval);

        // NPCManager에 등록 (자동으로 LOD 모드 관리 시작)
        if (NPCManager.Instance != null)
        {
            NPCManager.Instance.RegisterWaypointNpc(this);
        }
        else
        {
            Debug.LogWarning($"{name}: NPCManager를 찾을 수 없습니다!");
        }
    }

    private void OnDestroy()
    {
        // Manager에서 등록 해제 (메모리 누수 방지)
        if (NPCManager.Instance != null)
        {
            NPCManager.Instance.UnregisterWaypointNpc(this);
        }
    }

    /// <summary>
    /// 모드에 따라 다른 업데이트 로직을 실행합니다.
    /// Manager와 연동되어 currentMode가 변경됩니다.
    /// </summary>
    void Update()
    {
        // 공통 체크: 맵 경계 벗어나면 초기 위치로 리셋
        CheckOutOfBounds();

        // 모드별 분기 (switch 대신 플래그로 간단히 처리)
        switch (currentMode)
        {
            case UpdateMode.Precise:
                UpdatePrecise();  // 매 프레임 정밀 처리
                break;
            case UpdateMode.Normal:
                UpdateNormal();   // 기본 이동 + Manager 보조 체크
                break;
            case UpdateMode.Simplified:
                UpdateSimplified(); // 낮은 빈도 업데이트
                break;
        }
    }

    /// <summary>
    /// Precise 모드: 플레이어 근처에서만 동작
    /// - 매 프레임 Physics2D 충돌 체크 + Rigidbody2D 물리 이동
    /// </summary>
    private void UpdatePrecise()
    {
        // 플레이어 차단 체크 (매 프레임)
        CheckPlayerBlocking();

        if (isBlocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleWaiting();  // 대기 처리

        if (!isWaiting && enableRandomPause)
        {
            CheckRandomPause();  // 정밀 모드: 빠른 체크 간격
        }

        if (!isWaiting)
        {
            MoveToCurrentWaypoint();  // 물리 기반 부드러운 이동
        }
    }

    /// <summary>
    /// Normal 모드: 카메라 안에 있을 때
    /// - Manager가 주기적으로 OnManagerCheck 호출 → 플레이어 체크 위임
    /// - 랜덤 pause 확률/간격을 약간 느슨하게 조정
    /// </summary>
    private void UpdateNormal()
    {
        if (isBlocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleWaiting();  // 대기 처리

        if (!isWaiting && enableRandomPause)
        {
            // 체크 간격을 1.5배 늘리고 확률 0.7배로 조정 (성능 절약)
            pauseCheckTimer -= Time.deltaTime;
            if (pauseCheckTimer <= 0)
            {
                pauseCheckTimer = pauseCheckInterval * 1.5f;
                if (Random.value < pauseChance * 0.7f)
                {
                    StartPause();
                }
            }
        }

        if (!isWaiting)
        {
            MoveToCurrentWaypoint();
        }
    }

    /// <summary>
    /// Simplified 모드: 카메라 밖에서 동작 (성능 최우선)
    /// - Rigidbody2D 안 쓰고 직접 Transform 이동 (텔레포트)
    /// - 0.5초 간격으로만 업데이트
    /// </summary>
    private void UpdateSimplified()
    {
        simplifiedTimer += Time.deltaTime;
        if (simplifiedTimer < simplifiedUpdateInterval) return;  // 간격 체크
        simplifiedTimer = 0f;

        if (isWaiting)
        {
            waitTimer -= simplifiedUpdateInterval;
            if (waitTimer <= 0)
            {
                isWaiting = false;
            }
            return;
        }

        if (currentWaypointIndex >= waypoints.Length) return;

        Vector2 targetPos = waypoints[currentWaypointIndex].position;
        float distance = Vector2.Distance(transform.position, targetPos);

        if (distance < arriveDistance)
        {
            // 도착 → 대기 시작
            StartWaypointWait();
        }
        else
        {
            // 텔레포트 방식으로 조금씩 이동 (Rigidbody 안 씀)
            Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
            float moveAmount = moveSpeed * simplifiedUpdateInterval;

            if (moveAmount < distance)
            {
                transform.position += (Vector3)(direction * moveAmount);
            }
            else
            {
                transform.position = targetPos;  // 도착
            }
        }
    }

    /// <summary>
    /// Manager가 프레임 분산으로 호출 (Normal 모드 전용)
    /// - 플레이어 차단 여부만 체크
    /// </summary>
    public void OnManagerCheck()
    {
        if (currentMode == UpdateMode.Normal)
        {
            CheckPlayerBlocking();
        }
    }

    /// <summary>
    /// Manager가 호출하는 모드 변경 함수
    /// 모드 변경 시 필요한 초기화 수행
    /// </summary>
    public void SetUpdateMode(UpdateMode mode)
    {
        if (currentMode != mode)
        {
            currentMode = mode;
            if (mode == UpdateMode.Simplified)
            {
                simplifiedTimer = 0f;  // 즉시 업데이트
            }
            Debug.Log($"{name} 모드 변경: {mode}");
        }
    }

    /// <summary>
    /// 현재 waypoint로 물리 기반 부드러운 이동 (Precise/Normal 공통)
    /// </summary>
    private void MoveToCurrentWaypoint()
    {
        if (currentWaypointIndex >= waypoints.Length) return;

        Vector2 targetPos = waypoints[currentWaypointIndex].position;
        float distance = Vector2.Distance(transform.position, targetPos);

        if (distance < arriveDistance)
        {
            StartWaypointWait();
        }
        else
        {
            Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;  // Rigidbody2D로 자연스러운 이동
        }
    }

    /// <summary>
    /// waypoint 도착 시 랜덤 대기 시작
    /// </summary>
    private void StartWaypointWait()
    {
        isWaiting = true;
        waitTimer = Random.Range(waitTimeMin, waitTimeMax);
        rb.linearVelocity = Vector2.zero;
        MoveToNextWaypoint();  // 다음 waypoint 준비
    }

    /// <summary>
    /// 대기 타이머 처리 (모든 모드 공통)
    /// </summary>
    private void HandleWaiting()
    {
        if (isWaiting)
        {
            rb.linearVelocity = Vector2.zero;
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
            }
        }
    }

    /// <summary>
    /// 랜덤 중간 멈춤 체크 (Precise 모드용 빠른 체크)
    /// </summary>
    private void CheckRandomPause()
    {
        pauseCheckTimer -= Time.deltaTime;
        if (pauseCheckTimer <= 0)
        {
            pauseCheckTimer = pauseCheckInterval;
            if (Random.value < pauseChance)
            {
                StartPause();
            }
        }
    }

    /// <summary>
    /// 랜덤 멈춤 시작
    /// </summary>
    private void StartPause()
    {
        isWaiting = true;
        waitTimer = Random.Range(pauseTimeMin, pauseTimeMax);
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 플레이어와의 충돌 여부 체크 (Physics2D.OverlapCircle 사용)
    /// Manager의 cachedPlayerPos를 활용하면 더 최적화 가능
    /// </summary>
    private void CheckPlayerBlocking()
    {
        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        isBlocked = (playerCollider != null);
    }

    /// <summary>
    /// 맵 경계 벗어나면 초기 위치로 리셋 (안전장치)
    /// </summary>
    private void CheckOutOfBounds()
    {
        if (Vector2.Distance(transform.position, initialPosition) > 20f)
        {
            transform.position = initialPosition;
            currentWaypointIndex = 0;
            isWaiting = false;
            rb.linearVelocity = Vector2.zero;
            Debug.LogWarning($"{name}: 경계 벗어나 초기 위치로 리셋");
        }
    }

    /// <summary>
    /// 다음 waypoint로 인덱스 이동
    /// </summary>
    private void MoveToNextWaypoint()
    {
        currentWaypointIndex++;
        if (currentWaypointIndex >= waypoints.Length)
        {
            if (loopPath)
            {
                currentWaypointIndex = 0;  // 순환
            }
            else
            {
                currentWaypointIndex = waypoints.Length - 1;  // 마지막에 멈춤
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 에디터에서 경로와 감지 범위 시각화
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // waypoint 경로 선 그리기
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }

        // 루프 경로면 마지막→첫 번째 선 추가
        if (loopPath && waypoints.Length > 0 && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }

        // 각 waypoint 구체 표시
        Gizmos.color = Color.green;
        foreach (Transform wp in waypoints)
        {
            if (wp != null)
            {
                Gizmos.DrawWireSphere(wp.position, 0.3f);
            }
        }

        // 플레이 시 현재 모드에 따라 감지 범위 색상 변경
        if (Application.isPlaying)
        {
            switch (currentMode)
            {
                case UpdateMode.Precise: Gizmos.color = Color.green; break;    // 녹색: 정밀
                case UpdateMode.Normal: Gizmos.color = Color.yellow; break;    // 노랑: 일반
                case UpdateMode.Simplified: Gizmos.color = Color.gray; break;  // 회색: 간소화
            }
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
        else
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
