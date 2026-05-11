using UnityEngine;
using System.Collections.Generic;

public class GameStartManager : MonoBehaviour
{
    [System.Serializable]
    public struct StarterItem
    {
        public int slotIndex;
        public int itemID;
        public int amount;
    }

    [Header("References")]
    [Tooltip("플레이어의 인벤토리 시스템")]
    [SerializeField] private InventorySystem _inventorySystem;
    [SerializeField] private PlayerData _playerData;

    [Header("Start Location Settings")]
    [Tooltip("게임 시작 시 적용할 초기 장소의 LocationAnchor (예: 주인공 집 안)")]
    [SerializeField] private LocationAnchor _startAnchor;

    [Tooltip("시작 시 플레이어 오브젝트를 _startAnchor 위치로 자동 이동시킬지 여부")]
    [SerializeField] private bool _teleportPlayerToAnchorOnStart = true;
    [SerializeField] private Transform _playerTransform;

    [Header("Starter Data")]
    [SerializeField] private List<StarterItem> _starterItems = new List<StarterItem>();

    [Header("Debug")]
    [SerializeField] private bool _alwaysRunInEditor = false; // 에디터 테스트용 강제 실행 플래그

    private bool _isContinueGame = false;

    private void Awake()
    {
        if (SaveManagerV1.Instance != null)
        {
            _isContinueGame = SaveManagerV1.Instance.PendingLoad;
        }
    }

    private void Start()
    {
        // 이어하기 상태인지 체크
        if (_isContinueGame)
        {
#if UNITY_EDITOR
            if (!_alwaysRunInEditor)
#endif
            {
                Debug.Log("[GameStartManager] 이어하기(Load) 상태이므로 새 게임 초기 세팅(아이템 지급 등)을 건너뜁니다.");
                return;
            }
        }

        // ==========================================
        // 여기서부터는 '새 게임(PendingLoad == false)'일 때만 실행됩니다.
        // ==========================================

        Debug.Log("[GameStartManager] 새 게임을 감지했습니다. 초기 세팅을 시작합니다.");

        if (_playerData != null)
        {
            _playerData.InitializeData(); 
            Debug.Log("[GameStartManager] PlayerData 초기화 완료 (기본 레시피 포함).");
        }

        if (_inventorySystem == null)
            _inventorySystem = FindFirstObjectByType<InventorySystem>();

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        

        // 2. 초기 환경(실내/실외) 세팅 실행
        InitializeEnvironment();

        // 3. 초기 아이템 지급
        if (_inventorySystem != null)
        {
            GiveItems();
        }
    }

    private void InitializeEnvironment()
    {
        if (_startAnchor == null)
        {
            Debug.LogWarning("[GameStartManager] 시작 위치(LocationAnchor)가 인스펙터에 연결되지 않았습니다!");
            return;
        }

        //if (_teleportPlayerToAnchorOnStart && _playerTransform != null)
        //{
        //    Vector3 startPos = _startAnchor.transform.position;
        //    startPos.z = _playerTransform.position.z;
        //    _playerTransform.position = startPos;
        //}

        if (CameraManager.Instance != null)
            CameraManager.Instance.UpdateCameraState(_startAnchor);

        if (GlobalLightController.Instance != null)
            GlobalLightController.Instance.SetIndoorMode(_startAnchor.isIndoor);

        if (WeatherManager.Instance != null)
            WeatherManager.Instance.SetIndoorMode(_startAnchor.isIndoor);

        Debug.Log($"[GameStartManager] 초기 환경 세팅 완료: 현재 위치 = {_startAnchor.locationID}, 실내 여부 = {_startAnchor.isIndoor}");
    }

    private void GiveItems()
    {
        foreach (var item in _starterItems)
        {
            _inventorySystem.Inventory.Set(item.slotIndex, new ItemStack(item.itemID, item.amount));
        }

        Debug.Log("[GameStartManager] 초기 기본 아이템 지급이 완료되었습니다.");
    }
}