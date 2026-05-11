using System.Collections;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private FoodLoader foodLoader;

    private Coroutine _currentBuffCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (foodLoader == null)
            foodLoader = FindFirstObjectByType<FoodLoader>();
    }

    // [New] 이벤트 구독: 게임 시작 시 TimeManager 연결
    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay += OnNewDayPassed;
        }
    }

    // [New] 이벤트 해제: 오브젝트 파괴 시 연결 끊기 (에러 방지)
    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay -= OnNewDayPassed;
        }
    }

    // 하루가 지났을 때 호출되는 함수
    private void OnNewDayPassed(int day)
    {
        // 1. 돌아가고 있던 버프 타이머가 있다면 강제 중단
        if (_currentBuffCoroutine != null)
        {
            StopCoroutine(_currentBuffCoroutine);
            _currentBuffCoroutine = null;
        }

        // 2. 버프 수치 초기화
        RemoveBuffs();

        // 로그 확인용 (필요 없으면 삭제 가능)
        Debug.Log("[BuffManager] 잠을 자서 모든 버프가 초기화되었습니다.");
    }

    public void ConsumeFood(int itemId, int quality)
    {
        if (foodLoader == null || !foodLoader.IsLoaded) return;
        if (!foodLoader.foodDb.TryGet(itemId, out FoodDef food)) return;

        // 품질 배율 계산
        float multiplier = 1.0f;
        if (quality == 1) multiplier = 0.5f;
        else if (quality == 2) multiplier = 1.5f;

        // 즉시 회복
        if (food.healHP > 0)
        {
            float finalHeal = food.GetFinalValue(food.healHP, multiplier);
            ApplyHeal(finalHeal);
        }

        // 지속 버프
        if (food.buffDuration > 0)
        {
            if (_currentBuffCoroutine != null)
            {
                StopCoroutine(_currentBuffCoroutine);
                RemoveBuffs();
            }
            _currentBuffCoroutine = StartCoroutine(BuffRoutine(food, multiplier));
        }
    }

    private void ApplyHeal(float amount)
    {
        playerData.currentHP += amount;
        if (playerData.currentHP > playerData.FinalMaxHP)
            playerData.currentHP = playerData.FinalMaxHP;
    }

    private IEnumerator BuffRoutine(FoodDef food, float multiplier)
    {
        float addMaxHP = food.GetFinalValue(food.addMaxHP, multiplier);
        float addDmg = food.GetFinalValue(food.addAttackDamage, multiplier);
        float addAtkSpd = food.GetFinalValue(food.addAttackSpeed, multiplier);
        float addMoveSpd = food.GetFinalValue(food.addPlayerSpeed, multiplier);

        playerData.buffMaxHP += addMaxHP;
        playerData.buffAttackDamage += addDmg;
        playerData.buffAttackSpeed += addAtkSpd;
        playerData.buffMoveSpeed += addMoveSpd;

        Debug.Log($"[BuffManager] 버프 시작! (지속 {food.buffDuration}초)");

        yield return new WaitForSeconds(food.buffDuration);

        // 시간 다 됨 -> 해제
        RemoveBuffs();
        _currentBuffCoroutine = null;
        Debug.Log("[BuffManager] 버프 종료 (시간 만료).");
    }

    private void RemoveBuffs()
    {
        // PlayerData에 있는 스탯 초기화 함수 호출
        playerData.ResetBuffs();

        if (playerData.currentHP > playerData.FinalMaxHP)
            playerData.currentHP = playerData.FinalMaxHP;
    }
}