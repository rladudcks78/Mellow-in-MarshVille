using UnityEngine;

/// <summary>
/// 물리 이동 로직입니다.
/// </summary>
public class MoveController
{
    private readonly Rigidbody2D _rb;

    public MoveController(Rigidbody2D rb)
    {
        _rb = rb;
    }

    /// <summary>
    /// 이동 처리 및 대각선 속도 보정 
    /// </summary>
    public void Move(Vector2 direction, float speed)
    {
        // 입력 값이 1을 초과하면(대각선 등) 정규화하여 일정한 속도 유지.
        Vector2 moveDir = direction.sqrMagnitude > 1f ? direction.normalized : direction;
        _rb.linearVelocity = moveDir * speed;
    }

    public void Stop()
    {
        _rb.linearVelocity = Vector2.zero;
    }
}