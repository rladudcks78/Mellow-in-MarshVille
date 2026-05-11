using UnityEngine;

// 공격을 받아 체력이 깎이거나 반응해야 하는 모든 객체 
public interface IDamageable
{
    // 데미지 처리 (필요시 밀쳐내기 효과를 위해 hitPoint나 knockbackDir를 추가할 수도 있음)
    void TakeDamage(float damage);
}