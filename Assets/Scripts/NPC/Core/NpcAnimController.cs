using UnityEngine;

/// <summary>
/// NPC 전용 애니메이션 컨트롤러 (물리 기반 최적화 버전)
/// 물리 엔진(Rigidbody2D)의 속도를 직접 참조하여 애니메이션의 끊김/깜빡임 현상을 완벽히 방지합니다.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))] // Rigidbody2D 필수 추가
public class NpcAnimController : MonoBehaviour
{
    private readonly int _hashMoveUp = Animator.StringToHash("IsMove_Up");
    private readonly int _hashMoveDown = Animator.StringToHash("IsMove_Down");
    private readonly int _hashMoveLeft = Animator.StringToHash("IsMove_Left");
    private readonly int _hashMoveRight = Animator.StringToHash("IsMove_Right");

    private Animator _animator;
    private Rigidbody2D _rb;

    [SerializeField, Tooltip("이 속도 수치 이상이어야 걷기 애니메이션이 재생됩니다.")]
    private float _movementThreshold = 0.01f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>(); // 물리 이동 감지용 Rigidbody 가져오기
    }

    private void Update()
    {
        // NPCWaypoint에서 갱신하는 물리 속도를 직접 가져옵니다. 
        // 유니티 6.3 버전이므로 velocity 대신 linearVelocity를 사용합니다.
        Vector2 currentVelocity = _rb.linearVelocity;

        // 속도가 기준치 이상인지 확인 (sqrMagnitude로 성능 최적화)
        if (currentVelocity.sqrMagnitude > _movementThreshold * _movementThreshold)
        {
            UpdateMovementAnimation(currentVelocity);
        }
        else
        {
            StopAllMovementAnimations();
        }
    }

    private void UpdateMovementAnimation(Vector2 velocity)
    {
        StopAllMovementAnimations(); // 파라미터 꼬임 방지

        // X축(좌우) 속도가 Y축(상하) 속도보다 큰지 비교
        if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
        {
            if (velocity.x > 0) _animator.SetBool(_hashMoveRight, true);
            else _animator.SetBool(_hashMoveLeft, true);
        }
        else
        {
            if (velocity.y > 0) _animator.SetBool(_hashMoveUp, true);
            else _animator.SetBool(_hashMoveDown, true);
        }
    }

    private void StopAllMovementAnimations()
    {
        _animator.SetBool(_hashMoveUp, false);
        _animator.SetBool(_hashMoveDown, false);
        _animator.SetBool(_hashMoveLeft, false);
        _animator.SetBool(_hashMoveRight, false);
    }
}