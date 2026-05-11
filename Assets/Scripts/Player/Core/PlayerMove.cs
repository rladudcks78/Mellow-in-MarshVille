using UnityEngine;
using System.Collections;

public class PlayerMove : MonoBehaviour
{
    [Header("Input & Data")]
    [SerializeField] private InputReader _inputReader;
    [SerializeField] private PlayerData _playerData;

    private MoveController _moveController;
    private AnimController _animController;

    private Vector2 _currentInput;
    private bool _isKickBoard = false;

    // 현재 기절 연출 중인지 확인하는 플래그
    private bool _isFainting = false;

    public Vector2 FacingDir { get; private set; } = Vector2.down;

    private void Awake()
    {
        _moveController = new MoveController(GetComponent<Rigidbody2D>());
        _animController = new AnimController(GetComponent<Animator>());
    }

    #region 이벤트 구독 및 해제
    private void OnEnable()
    {
        _inputReader.MoveEvent += OnMoveInput;
        _inputReader.KickboardEvent += HandleKickBoard;
    }

    private void OnDisable()
    {
        _inputReader.MoveEvent -= OnMoveInput;
        _inputReader.KickboardEvent -= HandleKickBoard;
    }

    // TimeManager 이벤트는 Start/OnDestroy에서 관리 권장 (싱글톤 타이밍 이슈 방지)
    private void Start()
    {
        // TimeManager가 존재한다면 기절 애니메이션 이벤트 구독
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnPassOutAnimTrigger += HandlePassOutTrigger;
            // 아침이 되어 다시 움직일 수 있을 때 플래그를 풀어주기 위해 구독
            TimeManager.Instance.OnNewDay += HandleNewDay;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnPassOutAnimTrigger -= HandlePassOutTrigger;
            TimeManager.Instance.OnNewDay -= HandleNewDay;
        }
    }
    #endregion

    private void Update()
    {
        // 기절 중이면 애니메이션 업데이트도 중단 
        if (_isFainting) return;

        if (_currentInput.sqrMagnitude > 0.1f)
        {
            UpdateFacingDir(_currentInput);
        }

        _animController.UpdateAnimation(_currentInput);
    }

    private void FixedUpdate()
    {
        // 기절 중이면 물리 이동 연산 중단
        if (_isFainting)
        {
            _moveController.Stop(); // 미끄러짐 방지
            return;
        }

        float finalSpeed = _playerData.baseMoveSpeed;
        if (_playerData.isOnKickboard) finalSpeed += _playerData.kickboardBonusSpeed;

        _moveController.Move(_currentInput, finalSpeed);
    }

    // TimeManager가 02:00에 호출할 함수
    private void HandlePassOutTrigger()
    {
        // 1. 이동 조작 잠금
        _isFainting = true;
        _currentInput = Vector2.zero; // 입력 값 초기화

        // 2. 물리 이동 즉시 정지
        _moveController.Stop();

        // 3. 기절 애니메이션 재생
        _animController.TriggerPassOut();
    }

    // 다음 날 아침이 밝았을 때 호출 (TimeManager.OnNewDay)
    private void HandleNewDay(int day)
    {
        // 다시 움직일 수 있게 상태 해제
        _isFainting = false;
    }

    public void LookAt(Vector3 targetWorldPos)
    {
        // 기절 중에는 방향 전환 불가
        if (_isFainting) return;

        if (_currentInput.sqrMagnitude > 0.1f) return;

        Vector2 dir = (targetWorldPos - transform.position).normalized;
        UpdateFacingDir(dir);
    }

    private void UpdateFacingDir(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (angle > -45 && angle <= 45) FacingDir = Vector2.right;
        else if (angle > 45 && angle <= 135) FacingDir = Vector2.up;
        else if (angle > 135 || angle <= -135) FacingDir = Vector2.left;
        else FacingDir = Vector2.down;
    }

    private void HandleKickBoard()
    {
        // 기절 중에는 킥보드 조작 불가
        if (_isFainting) return;

        if (_isKickBoard) return;

        if (_playerData.isOnKickboard) StartCoroutine(UnmountKickboardRoutine());
        else MountKickboard();
    }

    private void MountKickboard() => _playerData.isOnKickboard = true;

    private IEnumerator UnmountKickboardRoutine()
    {
        _isKickBoard = true;
        yield return new WaitForSeconds(0.5f);
        _playerData.ResetToHandState();
        _isKickBoard = false;
    }

    private void OnMoveInput(Vector2 input)
    {
        // 기절 중이면 입력 무시
        if (_isFainting)
        {
            _currentInput = Vector2.zero;
            return;
        }
        _currentInput = input;
    }
}