using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 영구적인 데이터(저장 데이터)를 관리하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "SO/PlayerData")]
public class PlayerData : ScriptableObject
{
    [Header("Basic Stats")]
    public string playerName;
    public float currentHP;
    public float maxHP = 100;
    public long gold = 1000;   // 골드는 인플레이션 및 오버플로우 방지를 위해 long 타입을 사용합니다.

    [Header("Runtime Buffs (Do Not Edit)")]
    public float buffMaxHP;        // 버프로 늘어난 최대 체력
    public float buffAttackDamage; // 버프로 늘어난 공격력
    public float buffAttackSpeed;  // 버프로 늘어난 공격 속도
    public float buffMoveSpeed;    // 버프로 늘어난 이동 속도

    public float FinalMaxHP => maxHP + buffMaxHP;
    public float FinalAttackDamage => baseAttackDamage + buffAttackDamage;
    public float FinalMoveSpeed => isOnKickboard ? (baseMoveSpeed + kickboardBonusSpeed + buffMoveSpeed) : (baseMoveSpeed + buffMoveSpeed);

    [Header("Equipment & Action")]
    // 장비(칼, 국자, 도구 등)를 사용하는 속도입니다.
    public float actionSpeed = 1.0f;
    // 상호 작용 가능 거리
    public float interactRange = 2.0f;

    [Header("Movement")]
    // 킥보드 등 장비 효과를 제외한 플레이어의 순수 기본 속도입니다.
    public float baseMoveSpeed = 5f;
    public float kickboardBonusSpeed = 3f; // 킥보드 추가 속도

    [Header("Equipment State")]
    public bool isOnKickboard; // 킥보드 탑승 상태

    [Header("Combat Stats")]

    [Tooltip("기본 공격력 (무기 공격력과 합산될 수 있음)")]
    public int baseAttackDamage = 10;

    [Tooltip("공격 속도 (초 단위, 낮을수록 빠름 / 애니메이션 딜레이와 연동)")]
    public float attackDelay = 0.5f;

    [Tooltip("공격 판정의 중심점 거리 (플레이어 앞쪽으로 얼마나 나가서 때릴지)")]
    public float attackOffset = 1.0f;

    [Tooltip("공격 범위 크기 (가로, 세로)")]
    public Vector2 attackBoxSize = new Vector2(1.0f, 1.0f);

    [Header("Recipe Book")]
    // 저장 및 인스펙터 확인용 리스트
    [SerializeField]
    private List<int> unlockedRecipeIds = new List<int>();

    // 게임 중 빠른 검색(O(1))을 위한 해시셋 (런타임 전용)
    private HashSet<int> _runtimeRecipeSet = new HashSet<int>();

    //[Tooltip("넉백 파워 (적을 밀어내는 힘)")]
    //public float knockbackForce = 5.0f;

    //[Tooltip("치명타 확률 (0.0 ~ 1.0)")]
    //[Range(0f, 1f)]
    //public float criticalChance = 0.1f; // 10%

    //[Tooltip("치명타 배율 (기본 1.5배)")]
    //public float criticalMultiplier = 1.5f;


    /// <summary>
    /// 새 게임 시작 시 데이터를 기본값으로 초기화합니다.
    /// </summary>
    public void InitializeData()
    {
        currentHP = maxHP;
        gold = 1000;
        isOnKickboard = false;

        ResetBuffs();

        _runtimeRecipeSet.Clear();
        foreach (var id in unlockedRecipeIds)
        {
            _runtimeRecipeSet.Add(id);
        }
        UnlockRecipe(4001);
    }

    public void ResetBuffs()
    {
        buffMaxHP = 0;
        buffAttackDamage = 0;
        buffAttackSpeed = 0;
        buffMoveSpeed = 0;
    }

    /// <summary>
    /// 레시피 해금 (퀘스트 보상 등)
    /// </summary>
    public void UnlockRecipe(int recipeId)
    {
        // 이미 배우지 않았다면 추가
        if (!_runtimeRecipeSet.Contains(recipeId))
        {
            _runtimeRecipeSet.Add(recipeId);      // 검색용 추가
            unlockedRecipeIds.Add(recipeId);      // 저장용 추가
            Debug.Log($"[PlayerData] 레시피 습득! ID: {recipeId}");
        }
    }

    /// <summary>
    /// 이미 배운 레시피인지 확인 (도마, UI 표시용)
    /// </summary>
    public bool HasRecipe(int recipeId)
    {
        // 런타임 셋이 비어있으면(에디터에서 바로 실행 시 등) 동기화 시도
        if (_runtimeRecipeSet.Count != unlockedRecipeIds.Count)
        {
            SyncRecipeSet();
        }
        return _runtimeRecipeSet.Contains(recipeId);
    }

    // 안전장치: 리스트와 셋 동기화
    private void SyncRecipeSet()
    {
        _runtimeRecipeSet.Clear();
        foreach (var id in unlockedRecipeIds) _runtimeRecipeSet.Add(id);
    }

    // 킥보드 해제 후 상태는 도구 미장착(손 상태)
    public void ResetToHandState()
    {
        isOnKickboard = false;
    }
}