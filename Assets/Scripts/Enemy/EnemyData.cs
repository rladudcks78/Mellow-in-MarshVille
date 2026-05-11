using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    // =================================================================
    // 1. 기본 정보 (Identity)
    // =================================================================
    [Header("1. Identity Info")]
    public int enemyID;             // 몬스터 ID
    public string enemyName;        // 이름
    [TextArea]
    public string description;      // 도감 설명

    // =================================================================
    // 2. 스폰 설정 (Spawn Settings)
    // =================================================================
    [Header("2. Spawn Settings")]
    [Tooltip("죽은 뒤 다시 나타나기까지 걸리는 시간(초).\n0 = 다음 날 아침 리스폰\n-1 = 리스폰 안 함")]
    public float respawnTime = 5.0f; // 기본값 5초 (테스트 용이)

    // =================================================================
    // 3. 방어 설정 (Defensive)
    // =================================================================
    [Header("3. Defensive Stats")]
    public int maxHP;               // 최대 체력
    public BodyType bodyType;       // 무기 상성 (Soft, Hard)

    // =================================================================
    // 4. 공격 및 전투 설정 (Offensive)
    // =================================================================
    [Header("4. Offensive Stats")]
    public float attackDamage;        // 공격력
    public float attackRate;        // 공격 쿨타임
    public float attackRange;       // 공격 사거리

    [Header("Attack Hitbox Settings")]
    [Tooltip("공격 판정 박스의 중심점이 몬스터로부터 얼마나 떨어져 있는지")]
    public float attackOffset;      // 예: 0.5 (내 몸 앞 0.5미터)
    [Tooltip("공격 판정 박스의 크기 (가로, 세로)")]
    public Vector2 attackBoxSize;   // 예: (1, 1)
    [Tooltip("피격 시 플레이어를 밀쳐내는 힘 (또는 몬스터가 밀려나는 저항력으로 사용 가능)")]
    public float knockbackForce = 1f; // 넉백 파워 (기본값 5)

    // =================================================================
    // 5. AI 및 이동 (Movement)
    // =================================================================
    [Header("5. Movement & AI")]
    public float patrolSpeed;       // 배회 속도
    public float chaseSpeed;        // 추격 속도
    public float wanderRadius;      // 배회 반경
    public float detectionRange;    // 감지 범위
    public float giveUpRange;       // 포기 범위

    // =================================================================
    // 6. 드랍 아이템 (Loot Table)
    // =================================================================
    [Header("6. Loot Table")]
    [Tooltip("무조건 드랍 (퀘스트/재료)")]
    public List<GuaranteedLoot> guaranteedDrops;

    [Tooltip("확률 드랍 (레어 아이템)")]
    public List<ChanceLoot> chanceDrops;

    // =================================================================
    // 7. 리소스 (Visuals & Audio)
    // =================================================================
    [Header("7. Visuals & Audio")]
    public GameObject prefab;       // 몬스터 프리팹
    public AudioClip hitSound;      // 피격 사운드
    public AudioClip dieSound;      // 사망 사운드
}

// --- 보조 데이터 타입 ---

public enum BodyType
{
    Soft,
    Hard,
    Medium
}

/// <summary>
/// 100% 확률로 반드시 드랍해야 하는 아이템 정보입니다.
/// </summary>
[System.Serializable]
public struct GuaranteedLoot
{
    public int itemID;
    public int minAmount;
    public int maxAmount;
}

/// <summary>
/// 특정 확률에 따라 드랍 여부가 결정되는 아이템 정보입니다.
/// </summary>
[System.Serializable]
public struct ChanceLoot
{
    public int itemID;
    [Range(0f, 100f)]
    public float dropChance;
    public int minAmount;
    public int maxAmount;
}