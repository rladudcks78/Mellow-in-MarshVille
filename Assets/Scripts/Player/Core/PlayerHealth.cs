using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어의 체력을 관리하고 피격 처리를 담당하는 클래스입니다.
/// 몬스터와 동일하게 IDamageable을 구현하여 몬스터의 공격을 받을 수 있습니다.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private PlayerData _playerData;        // 최대 체력 정보를 가져옴
    [SerializeField] private SpriteRenderer _bodyRenderer;  // 피격 시 깜빡일 스프라이트

    [Header("State")]
    [SerializeField] private float _currentHP;
    [SerializeField] private float _maxHP;

    public float CurrentHP => _currentHP;
    public float MaxHP => _maxHP;

    private bool _isInvincible = false; // 무적 상태

    public event Action<float, float> OnHpChanged;

    public event Action OnDied;

    // UI 업데이트를 위한 이벤트 (나중에 HP바 만들 때 사용)
    // public event System.Action<int, int> OnHealthChanged; 

    private void Start()
    {
        _currentHP = (_playerData != null) ? _playerData.maxHP : 100f;
        _maxHP = (_playerData != null) ? _playerData.maxHP : 100f;

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponentInChildren<SpriteRenderer>();

        OnHpChanged?.Invoke(CurrentHP, MaxHP);

        Debug.Log($"[Player] 체력 초기화 완료: {_currentHP}");
    }

    // ---------------------------------------------------------
    // IDamageable 인터페이스 구현 (몬스터가 이 함수를 호출함)
    // ---------------------------------------------------------
    public void TakeDamage(float damage)
    {
        // 1. 무적 상태거나 이미 죽었으면 무시
        if (_isInvincible || _currentHP <= 0) return;

        // 2. 체력 감소
        _currentHP -= damage;
        Debug.Log($"<color=red>[Player] 으악! 맞았다! (남은 체력: {_currentHP})</color>");

        // (선택) UI 업데이트 알림
        OnHpChanged?.Invoke(CurrentHP, MaxHP);

        // 3. 사망 체크
        if (_currentHP <= 0)
        {
            Die();
            return;
        }

        // 4. 무적 및 깜빡임 효과 시작
        StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        _isInvincible = true;

        // 1초 동안 무적 
        float duration = 1.0f;
        float timer = 0;

        // 깜빡임 효과 
        while (timer < duration)
        {
            timer += 0.1f;
            if (_bodyRenderer != null)
            {
                _bodyRenderer.color = new Color(1, 1, 1, 0.5f); // 반투명
                yield return new WaitForSeconds(0.05f);
                _bodyRenderer.color = new Color(1, 1, 1, 1f);   // 불투명
                yield return new WaitForSeconds(0.05f);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        if(_bodyRenderer != null) _bodyRenderer.color = Color.white; // 색상 완전 복구
        _isInvincible = false;
    }

    private void Die()
    {
        Debug.Log("<color=red>[Game Over] 플레이어가 쓰러졌습니다...</color>");

        OnDied?.Invoke();

        //1)화면 까매지고, 2)체력 & 아이템 잃고(?), 3)하루 지나고, 4)스폰 포인트로 이동 시키고 5)화면 밝아지기




        // 여기에 게임 오버 로직 추가
        // 예: 입력 차단, 쓰러지는 애니메이션, 병원으로 이송되는 UI 등
        // GetComponent<PlayerInteract>().enabled = false;
        // GetComponent<PlayerMove>().enabled = false;
    }

    public void RestoreHealth()
    {
        _currentHP = _maxHP;
        _isInvincible = false;
        OnHpChanged?.Invoke(CurrentHP, MaxHP);
    }
}