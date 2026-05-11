using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC들을 한 곳에서 통합 관리하는 매니저 클래스입니다.
/// - Waypoint 이동용 NPC와 혼잣말(대사)용 NPC를 모두 관리합니다.
/// - 카메라 범위와 플레이어와의 거리 정보를 이용해 NPC들을 상태별로 분류합니다.
/// - 각 NPC가 개별 Update를 갖지 않고, 매니저에서 프레임당 일부만 검사해 최적화합니다.
/// - 2D 직교(Orthographic) 카메라를 기준으로 화면 Bounds를 계산하는 설계입니다.
/// </summary>
public class NPCManager : MonoBehaviour
{
    /// <summary>
    /// 전역에서 접근하기 위한 간단한 싱글톤 인스턴스입니다.
    /// 씬에 NPCManager가 1개만 존재한다는 전제를 가지고 있습니다.
    /// </summary>
    public static NPCManager Instance;

    private bool isCameraBoundsDirty;
    private int frameCounter = 0;

    [Header("참조")]
    [SerializeField] private Camera mainCamera;  // NPC 가시성 계산에 사용할 메인 카메라 참조
    [SerializeField] private Transform player;   // 플레이어 위치를 참조하기 위한 Transform

    [Header("카메라 설정")]
    [SerializeField] private float cameraCheckInterval = 1f; // 카메라 범위를 몇 초 간격으로 재계산할지
    [SerializeField] private float cameraMargin = 5f;        // 화면 가장자리 바깥쪽으로 여유를 둘 거리

    [Header("플레이어 거리 기준")]
    [SerializeField] private float playerNearDistance = 10f;      // 이 거리 이내의 Waypoint NPC는 '정밀 업데이트' 대상
    [SerializeField] private float playerMonologueDistance = 15f; // 이 거리 이내의 NPC만 혼잣말(대사) 가능

    [Header("성능 최적화")]
    [SerializeField] private int waypointCheckPerFrame = 3;   // 한 프레임에 검사할 Waypoint NPC 수
    [SerializeField] private int monologueCheckPerFrame = 2;  // 한 프레임에 검사할 Monologue NPC 수

    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = false; // 콘솔에 디버그 로그 출력 여부
    [SerializeField] private bool showGizmos = false;    // 씬 뷰에 기즈모(범위/상태) 표시 여부

    // === NPC 등록 리스트 ===
    // 씬에 존재하는 모든 Waypoint/Monologue NPC를 추적하기 위한 기본 리스트입니다.
    private List<NPCWaypoint> allWaypointNpcs = new List<NPCWaypoint>();
    private List<NPCMonologue> allMonologueNpcs = new List<NPCMonologue>();

    // === 카테고리별 분류 (양쪽 시스템 공유) ===
    // Waypoint NPC를 플레이어 거리와 카메라 가시성에 따라 분류한 집합들입니다.
    private HashSet<NPCWaypoint> nearWaypointNpcs = new HashSet<NPCWaypoint>();      // 플레이어 근처 (정밀 업데이트)
    private HashSet<NPCWaypoint> visibleWaypointNpcs = new HashSet<NPCWaypoint>();   // 카메라에 보이는 일반 업데이트 대상
    private HashSet<NPCWaypoint> farWaypointNpcs = new HashSet<NPCWaypoint>();       // 멀리 있는, 단순화된 업데이트 대상

    // Monologue(혼잣말) NPC 중, 카메라에 보이면서 말할 수 있는 거리 안에 있는 NPC들
    private HashSet<NPCMonologue> visibleMonologueNpcs = new HashSet<NPCMonologue>();

    // === 캐시 (공유) ===
    // 카메라의 현재 화면 Bounds와, 매 프레임 플레이어 위치 캐시입니다.
    private Bounds cameraBounds;
    private Vector2 cachedPlayerPosition;
    private float cameraCheckTimer = 0f; // 카메라 범위를 일정 주기로 갱신하기 위한 타이머

    // === 분산 처리용 인덱스 ===
    // 프레임당 일부 NPC만 검사하기 위해 사용하는 인덱스입니다.
    private int waypointCheckIndex = 0;
    private int monologueCheckIndex = 0;

    // === 프레임 분산 체크용 임시 리스트 (GC 할당 줄이기 위해 재사용) ===
    private List<NPCWaypoint> tempWaypointList = new List<NPCWaypoint>();
    private List<NPCMonologue> tempMonologueList = new List<NPCMonologue>();

    private void Awake()
    {
        // 간단한 싱글톤 구현: 이미 인스턴스가 있으면 자신을 파괴하고,
        // 없으면 자신을 전역 인스턴스로 등록합니다.
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // 인스펙터에서 카메라를 지정하지 않았다면 Camera.main을 자동 할당합니다.
        if (mainCamera == null)
            mainCamera = Camera.main;

        // 인스펙터에서 플레이어를 지정하지 않았다면 태그로 찾아서 할당합니다.
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    private void Start()
    {
        // 시작 시점에 한 번 카메라 범위를 계산해 둡니다.
        UpdateCameraBounds();
    }

    private void Update()
    {
        if (player != null)
            cachedPlayerPosition = new Vector2(player.position.x, player.position.y);

        // 카메라 Bounds 1초마다 갱신 (매 프레임 아님)
        cameraCheckTimer += Time.deltaTime;
        if (cameraCheckTimer >= cameraCheckInterval)
        {
            cameraCheckTimer = 0f;
            isCameraBoundsDirty = true;
        }

        if (isCameraBoundsDirty)
            UpdateCameraBounds();

        CategorizeAllNpcs();

        frameCounter++;
        if (frameCounter % 2 == 0)  // 짝수 프레임만 Waypoint 체크
            CheckWaypointNpcsThisFrame();
        else
            CheckMonologueNpcsThisFrame();
    }


    // ========================================
    // 등록/해제
    // ========================================

    /// <summary>
    /// Waypoint 이동을 사용하는 NPC를 매니저에 등록합니다.
    /// NPCWaypoint가 활성화될 때 호출되는 것을 전제로 합니다.
    /// </summary>
    public void RegisterWaypointNpc(NPCWaypoint npc)
    {
        if (!allWaypointNpcs.Contains(npc))
        {
            allWaypointNpcs.Add(npc);

            if (showDebugInfo)
                Debug.Log($"[NPCManager] Waypoint NPC 등록: {npc.name} (총 {allWaypointNpcs.Count}개)");
        }
    }

    /// <summary>
    /// Waypoint NPC를 매니저에서 제거합니다.
    /// (리스트와 모든 카테고리 집합에서 제거합니다.)
    /// 주로 OnDisable 또는 OnDestroy에서 호출하는 것을 권장합니다.
    /// </summary>
    public void UnregisterWaypointNpc(NPCWaypoint npc)
    {
        allWaypointNpcs.Remove(npc);
        nearWaypointNpcs.Remove(npc);
        visibleWaypointNpcs.Remove(npc);
        farWaypointNpcs.Remove(npc);
    }

    /// <summary>
    /// 혼잣말(대사) 기능을 가진 NPC를 매니저에 등록합니다.
    /// </summary>
    public void RegisterMonologueNpc(NPCMonologue npc)
    {
        if (!allMonologueNpcs.Contains(npc))
        {
            allMonologueNpcs.Add(npc);

            if (showDebugInfo)
                Debug.Log($"[NPCManager] Monologue NPC 등록: {npc.name} (총 {allMonologueNpcs.Count}개)");
        }
    }

    /// <summary>
    /// Monologue NPC를 매니저에서 제거합니다.
    /// </summary>
    public void UnregisterMonologueNpc(NPCMonologue npc)
    {
        allMonologueNpcs.Remove(npc);
        visibleMonologueNpcs.Remove(npc);
    }

    // ========================================
    // 카메라/거리 계산 (통합)
    // ========================================

    /// <summary>
    /// 현재 카메라 위치, 직교 사이즈, 화면 비율을 이용해
    /// NPC 가시성 판정에 사용할 Bounds(직사각형 영역)를 계산합니다.
    /// </summary>
    private void UpdateCameraBounds()
    {
        if (mainCamera == null) return;

        float orthographicSize = mainCamera.orthographicSize * 2f;
        float height = orthographicSize + cameraMargin * 2f;
        float width = height * mainCamera.aspect;

        Vector3 center = mainCamera.transform.position;
        Vector3 size = new Vector3(width, height, 1000f);

        cameraBounds = new Bounds(center, size);
        isCameraBoundsDirty = false;  // 추가
    }


    /// <summary>
    /// 통합 분류: 등록된 모든 NPC를 한 번씩 돌면서
    /// - Waypoint NPC: 플레이어 거리 + 카메라 가시성 기준 분류
    /// - Monologue NPC: 카메라 안 + 거리 조건을 만족하는지 여부만 판단
    /// 
    /// 동시에 null이 된 NPC는 리스트에서 제거해 이후 루프를 줄입니다.
    /// </summary>
    private void CategorizeAllNpcs()
    {
        // 이전 프레임의 분류 결과 초기화
        nearWaypointNpcs.Clear();
        visibleWaypointNpcs.Clear();
        farWaypointNpcs.Clear();
        visibleMonologueNpcs.Clear();

        // null 체크 후 제거용 임시 리스트
        List<NPCWaypoint> removeWaypoint = null;
        List<NPCMonologue> removeMonologue = null;

        // === Waypoint NPC 분류 ===
        for (int i = 0; i < allWaypointNpcs.Count; i++)
        {
            var npc = allWaypointNpcs[i];
            if (npc == null || !npc.enabled)
            {
                removeWaypoint ??= new List<NPCWaypoint>();
                removeWaypoint.Add(npc);
                continue;
            }

            Vector2 npcPos = npc.transform.position;
            bool inCamera = cameraBounds.Contains(npcPos);

            float distSqToPlayer = (npcPos - cachedPlayerPosition).sqrMagnitude;
            float nearDistSq = playerNearDistance * playerNearDistance;

            if (distSqToPlayer <= nearDistSq)
            {
                nearWaypointNpcs.Add(npc);
                npc.SetUpdateMode(NPCWaypoint.UpdateMode.Precise);
            }
            else if (inCamera)
            {
                visibleWaypointNpcs.Add(npc);
                npc.SetUpdateMode(NPCWaypoint.UpdateMode.Normal);
            }
            else
            {
                farWaypointNpcs.Add(npc);
                npc.SetUpdateMode(NPCWaypoint.UpdateMode.Simplified);
            }
        }

        // null이 된 Waypoint NPC 제거
        if (removeWaypoint != null)
        {
            foreach (var npc in removeWaypoint)
            {
                allWaypointNpcs.Remove(npc);
                nearWaypointNpcs.Remove(npc);
                visibleWaypointNpcs.Remove(npc);
                farWaypointNpcs.Remove(npc);
            }
        }

        // === Monologue NPC 분류 ===
        // 조건: 카메라 안에 있고, 플레이어와의 거리가 특정 값 이내일 때만 대사(혼잣말)를 활성화
        for (int i = 0; i < allMonologueNpcs.Count; i++)
        {
            var npc = allMonologueNpcs[i];

            if (npc == null || !npc.enabled)
            {
                removeMonologue ??= new List<NPCMonologue>();
                removeMonologue.Add(npc);
                continue;
            }

            Vector2 npcPos = npc.transform.position;
            bool inCamera = cameraBounds.Contains(npcPos);
            float distToPlayer = Vector2.Distance(npcPos, cachedPlayerPosition);

            // 카메라 안이고 혼잣말 가능 거리 안이면 활성
            if (inCamera && distToPlayer <= playerMonologueDistance)
            {
                visibleMonologueNpcs.Add(npc);
                npc.SetVisible(true);
            }
            else
            {
                npc.SetVisible(false);
            }
        }

        // null이 된 Monologue NPC 제거
        if (removeMonologue != null)
        {
            foreach (var npc in removeMonologue)
            {
                allMonologueNpcs.Remove(npc);
                visibleMonologueNpcs.Remove(npc);
            }
        }

        // 디버그 로그: 현재 분류 결과를 콘솔에 출력
        if (showDebugInfo)
        {
            Debug.Log(
                $"[NPCManager] Waypoint - 근처:{nearWaypointNpcs.Count} " +
                $"카메라:{visibleWaypointNpcs.Count} 먼곳:{farWaypointNpcs.Count} / " +
                $"Monologue - 활성:{visibleMonologueNpcs.Count}"
            );
        }
    }

    // ========================================
    // 프레임 분산 체크
    // ========================================

    /// <summary>
    /// 이번 프레임에서 검사할 Waypoint NPC들을 일부만 선택해 OnManagerCheck를 호출합니다.
    /// - HashSet을 List로 변환해 순서를 고정한 뒤,
    /// - 내부 인덱스를 회전시키면서 일정 개수만 검사합니다.
    /// - 임시 리스트는 필드로 재사용해 GC 할당을 줄였습니다.
    /// </summary>
    private void CheckWaypointNpcsThisFrame()
    {
        // 기존에 사용하던 new List 대신, 재사용용 필드 리스트를 사용합니다.
        tempWaypointList.Clear();
        tempWaypointList.AddRange(visibleWaypointNpcs);

        if (tempWaypointList.Count == 0) return;

        int checkedCount = 0;

        // 이번 프레임에 waypointCheckPerFrame 개수만큼 NPC를 검사합니다.
        while (checkedCount < waypointCheckPerFrame && tempWaypointList.Count > 0)
        {
            // 인덱스가 리스트 끝을 넘어가면 0으로 되돌려서 순환 구조를 만듭니다.
            if (waypointCheckIndex >= tempWaypointList.Count)
                waypointCheckIndex = 0;

            if (waypointCheckIndex < tempWaypointList.Count)
            {
                var npc = tempWaypointList[waypointCheckIndex];
                if (npc != null && npc.enabled)
                {
                    // 개별 NPC에게 "매니저에서 체크할 차례"라고 알려주는 콜백
                    npc.OnManagerCheck();
                }
            }

            waypointCheckIndex++;
            checkedCount++;
        }
    }

    /// <summary>
    /// 이번 프레임에서 검사할 Monologue NPC 일부만 선택해 OnManagerCheck를 호출합니다.
    /// Waypoint와 마찬가지로 프레임당 처리량을 제한하는 역할입니다.
    /// </summary>
    private void CheckMonologueNpcsThisFrame()
    {
        tempMonologueList.Clear();
        tempMonologueList.AddRange(visibleMonologueNpcs);

        if (tempMonologueList.Count == 0) return;

        int checkedCount = 0;

        while (checkedCount < monologueCheckPerFrame && tempMonologueList.Count > 0)
        {
            if (monologueCheckIndex >= tempMonologueList.Count)
                monologueCheckIndex = 0;

            if (monologueCheckIndex < tempMonologueList.Count)
            {
                var npc = tempMonologueList[monologueCheckIndex];
                if (npc != null && npc.enabled)
                {
                    npc.OnManagerCheck();
                }
            }

            monologueCheckIndex++;
            checkedCount++;
        }
    }

    // ========================================
    // 외부 접근용 (다른 스크립트에서 필요할 수 있는 정보들)
    // ========================================

    /// <summary>플레이어의 마지막 캐싱된 2D 위치를 반환합니다.</summary>
    public Vector2 GetPlayerPosition() => cachedPlayerPosition;

    /// <summary>현재 카메라 기준 NPC 가시성 판단에 사용하는 Bounds를 반환합니다.</summary>
    public Bounds GetCameraBounds() => cameraBounds;

    /// <summary>특정 Waypoint NPC가 플레이어 근처(정밀 업데이트 대상)인지 여부.</summary>
    public bool IsWaypointNpcNearPlayer(NPCWaypoint npc) => nearWaypointNpcs.Contains(npc);

    /// <summary>특정 Waypoint NPC가 카메라 안에서 보이는지 여부.</summary>
    public bool IsWaypointNpcVisible(NPCWaypoint npc) => visibleWaypointNpcs.Contains(npc);

    /// <summary>특정 Waypoint NPC가 먼 거리(단순화 업데이트 대상)인지 여부.</summary>
    public bool IsWaypointNpcFar(NPCWaypoint npc) => farWaypointNpcs.Contains(npc);

    /// <summary>특정 Monologue NPC가 카메라 안 + 거리 조건을 만족해 대사 활성 상태인지 여부.</summary>
    public bool IsMonologueNpcVisible(NPCMonologue npc) => visibleMonologueNpcs.Contains(npc);

    // ========================================
    // 디버그 시각화 (씬 뷰에서만 보이는 기즈모)
    // ========================================

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // cameraBounds가 아직 유효하지 않은 경우(0에 가까운 크기)는 기즈모를 그리지 않습니다.
        if (cameraBounds.size.x <= 0.01f || cameraBounds.size.y <= 0.01f) return;

        // 카메라 범위 박스 그리기
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);

        // 플레이어 주변 거리 기준 원(근처 / 혼잣말 가능 거리) 그리기
        if (player != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.position, playerNearDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, playerMonologueDistance);
        }

        // Waypoint NPC 상태별 표시
        foreach (var npc in nearWaypointNpcs)
        {
            if (npc != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(npc.transform.position, Vector3.one * 0.5f);
            }
        }

        foreach (var npc in visibleWaypointNpcs)
        {
            if (npc != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(npc.transform.position, Vector3.one * 0.4f);
            }
        }

        foreach (var npc in farWaypointNpcs)
        {
            if (npc != null)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireCube(npc.transform.position, Vector3.one * 0.3f);
            }
        }

        // Monologue NPC(대사 활성) 표시
        foreach (var npc in visibleMonologueNpcs)
        {
            if (npc != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(npc.transform.position, 0.3f);
            }
        }
    }
}
