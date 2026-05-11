using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 입력을 감지하여 InteractionController에게 명령을 내리는 관리 클래스입니다.
/// </summary>
public class PlayerInteract : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader _inputReader;
    [SerializeField] private PlayerData _playerData;
    [SerializeField] private TileDistanceChecker _distanceChecker;

    [Header("Systems")]
    [SerializeField] private InventorySystem _inventorySystem;
    [SerializeField] private FarmSystem _farmSystem;
    [SerializeField] private WaterSystem _waterSystem;
    [SerializeField] private CombatSystem _combatSystem;
    [SerializeField] private SkillSystem _skillSystem;

    //[Header("Tool Settings (CSV ID)")]
    //[SerializeField] private int _hoeID = 7001;
    //[SerializeField] private int _wateringCanID = 7002;
    //[SerializeField] private int _sickleID = 7003;
    //[SerializeField] private int _fishingRodID = 7004;
    //[SerializeField] private int _scoopID = 6002;
    //[SerializeField] private int _wateringCan2ID = 7005;
    //[SerializeField] private int _sickle2ID = 7006;

    //[Header("Test Settings")]
    //[SerializeField] private bool _isTestMode = true;
    //[SerializeField] private int _testSeedID = 8001;
    //[SerializeField] private int _testSeed2ID = 8002;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactLayer; // 조리대가 있는 레이어

    [Header("Interaction Settings")]
    [SerializeField] private float _skillCooldown = 0.3f;   // 스킬/섭취 쿨타임 (초)
    private float _lastSkillTime;                           // 마지막 사용 시간 기록용

    [Header("UI Clickthrough Guard")]
    [Tooltip("UI 위에서 클릭했을 때 월드 상호작용을 막습니다(Update에서 검사).")]
    [SerializeField] private bool blockWorldInteractionWhenPointerOverUI = true;

    private PlayerMove _playerMove;
    private InteractionController _interactionController;
    private int _currentHotbarIndex = 0;

    // 입력 콜백에서 즉시 실행하지 않고, Update에서 처리하기 위한 요청 플래그
    private bool _interactionRequested;

    private void Awake()
    {
        if (_inventorySystem == null)
            _inventorySystem = FindFirstObjectByType<InventorySystem>();

        // 컴포넌트 자동 할당
        if (_distanceChecker == null)
            _distanceChecker = GetComponent<TileDistanceChecker>();

        if (_combatSystem == null) 
            _combatSystem = GetComponent<CombatSystem>();

        _playerMove = GetComponent<PlayerMove>();

        if (_skillSystem == null)
            _skillSystem = GetComponent<SkillSystem>();

        // 주의: _farmSystem/_waterSystem은 인스펙터 할당을 기대(필요하면 여기서 Find로 보완 가능)
        _interactionController = new InteractionController(_farmSystem, _waterSystem, _combatSystem, _inventorySystem,_skillSystem);
    }

    //private void Start()
    //{
    //    if (_isTestMode && _inventorySystem != null)
    //    {
    //        // 이 Set은 인벤 이벤트(OnItemAdded)를 발생시키지 않을 수 있어요(직접 슬롯 세팅이므로).
    //        // 테스트용으로는 OK, 실제 게임에선 TryPickup/TryAddFromExternal 경로를 권장.
    //        _inventorySystem.Inventory.Set(0, new ItemStack(_hoeID, 1));
    //        _inventorySystem.Inventory.Set(1, new ItemStack(_wateringCanID, 1));
    //        _inventorySystem.Inventory.Set(2, new ItemStack(_testSeedID, 10));
    //        _inventorySystem.Inventory.Set(3, new ItemStack(_sickleID, 1));
    //        _inventorySystem.Inventory.Set(4, new ItemStack(_fishingRodID, 1));
    //        _inventorySystem.Inventory.Set(5, new ItemStack(_scoopID, 1));
    //        _inventorySystem.Inventory.Set(6, new ItemStack(_wateringCan2ID, 1));
    //        _inventorySystem.Inventory.Set(7, new ItemStack(_sickle2ID, 1));
    //        _inventorySystem.Inventory.Set(8, new ItemStack(_testSeed2ID, 10));
    //    }
    //}

    #region 이벤트 구독
    private void OnEnable()
    {
        if (_inputReader == null)
        {
            Debug.LogError("[PlayerInteract] InputReader가 연결되지 않았습니다!");
            return;
        }

        // AttackEvent(입력 콜백) -> OnInteractionInput 호출
        _inputReader.AttackEvent += OnInteractionInput;
        _inputReader.skillEvent += OnSkillInput;
    }

    private void OnDisable()
    {
        _inputReader.AttackEvent -= OnInteractionInput;
        _inputReader.skillEvent -= OnSkillInput;
    }
    #endregion

    // 거리 계산 로직 리팩토링
    private void OnInteractionInput()
    {
        _interactionRequested = true;
    }

    private void Update()
    {
        if (!_interactionRequested) return;
        _interactionRequested = false;

        // UI 위 클릭이면 월드 상호작용 차단 (Update에서 호출해야 경고/프레임 불일치 문제를 피함)
        if (blockWorldInteractionWhenPointerOverUI)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            ExecuteWorldInteraction();
        }
    }

    /// <summary>
    /// 기존 OnInteractionInput의 본체(월드 상호작용 실행).
    /// </summary>
    private void ExecuteWorldInteraction()
    {

    if (_inventorySystem == null || !_inventorySystem.IsReady) return;


        // 1. 오브젝트 상호작용 (조리대 등)
        if (TryInteractWithObject()) return;

        // 2. 거리 체크 (무기 제외)
        if (!IsActiveWeapon())
        {
            if (!_distanceChecker.IsInInteractionRange()) return;
        }

        if (_distanceChecker == null)
        {
            Debug.LogError("TileDistanceChecker가 없음!");
            return;
        }

        // 마우스 월드 좌표 가져오기
        Vector3 mouseWorldPos = _distanceChecker.GetMouseWorldPosition();


        // --- 도구 사용 직전에 마우스 방향을 보게 함 ---
        if (_playerMove != null)
        {
            _playerMove.LookAt(mouseWorldPos); // FacingDir가 마우스 쪽으로 업데이트됨
        }

        _interactionController.ExecuteInteraction(
            mouseWorldPos,
            transform.position,
            _playerMove != null ? _playerMove.FacingDir : Vector2.zero
        );
        Debug.Log("[PlayerInteract] WorldInteraction 진입");
    }

    private bool IsActiveWeapon()
    {
        if (_inventorySystem == null) return false;

        if (_inventorySystem.TryGetActive(out _, out _, out ItemDef def) && def != null)
            return def.IsWeapon;

        return false;
    }

    private void OnSkillInput()
    {
        if (_inventorySystem == null) return;
        if (!_inventorySystem.IsReady) return;

        if (Time.time < _lastSkillTime + _skillCooldown) return;

        // 실행
        _interactionController.ExecuteSkill(transform.position, _playerMove.FacingDir);

        // 시간 갱신
        _lastSkillTime = Time.time;
    }

    // [추가] 마우스 클릭 지점에 있는 오브젝트와 상호작용 시도
    private bool TryInteractWithObject()
    {
        if (_distanceChecker == null)
        {
            Debug.LogError("[PlayerInteract] TileDistanceChecker가 없습니다.");
            return false;
        }

        Vector3 mousePos = _distanceChecker.GetMouseWorldPosition();
        Vector2 checkPoint = new Vector2(mousePos.x, mousePos.y);

        // 디버그: 클릭 좌표 확인 (필요할 때만 켜도 됨)
        // Debug.Log($"[PlayerInteract] checkPoint = {checkPoint}");

        Collider2D hit = Physics2D.OverlapPoint(checkPoint, interactLayer);

        if (hit == null)
        {
            // 여기 로그를 잠깐 켜면 레이어/좌표 문제 확인에 좋음
            // Debug.Log("[PlayerInteract] 상호작용 대상 없음 (hit == null)");
            return false;
        }

        Debug.Log($"[Debug] 클릭한 물체: {hit.name}, 레이어: {LayerMask.LayerToName(hit.gameObject.layer)}");

        float dist = Vector3.Distance(transform.position, hit.transform.position);
        if (_playerData != null && dist > _playerData.interactRange)
        {
            Debug.Log($"[Debug] 너무 멂! 거리: {dist:F2} / 허용: {_playerData.interactRange:F2}");
            return false;
        }

        // 1) 조리대
        if (hit.TryGetComponent<CookingStation>(out var cookingStation))
        {
            cookingStation.Interact();
            return true;
        }

        // 2) 창고 (같은 오브젝트에 스크립트가 있는 경우)
        if (hit.TryGetComponent<StorageStation>(out var storageStation))
        {
            storageStation.Interact();
            return true;
        }

        // 3) 콜라이더가 자식이고, 스크립트가 부모에 붙은 경우까지 대응 (안전장치)
        var cookingParent = hit.GetComponentInParent<CookingStation>();
        if (cookingParent != null)
        {
            cookingParent.Interact();
            return true;
        }

        var storageParent = hit.GetComponentInParent<StorageStation>();
        if (storageParent != null)
        {
            storageParent.Interact();
            return true;
        }

        Debug.Log($"[PlayerInteract] 상호작용 가능한 스크립트 없음: {hit.name}");
        return false;
    }

    // 외부에 현재 핫바 인덱스를 제공하는 프로퍼티
    public int CurrentHotbarIndex => _currentHotbarIndex;
}