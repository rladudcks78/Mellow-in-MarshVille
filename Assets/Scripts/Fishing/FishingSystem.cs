using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FishingSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private FishingMiniGameUI minigameUI;

    [Header("Rod Check")]
    [SerializeField] private string fishingRodToolType = "fishingRod";

    [Header("Bite?")]
    [SerializeField] private GameObject biteMarkRoot;           //느낌표 오브젝트
    [SerializeField] private float preBiteDelayMin = 5f;        //유예 최소 시간
    [SerializeField] private float preBiteDelayMax = 10f;       //유예 최대 시간
    [SerializeField] private float biteWindowSec = 1.2f;        //느낌표 뜬 뒤 입력 제한 시간

    [SerializeField] private Vector3 biteMarkOffset = new Vector3(0f, 1f, 0f);   //느낌표 위치 (플레이어 머리 위)
    

    [Header("Minigame Tuning")]
    [SerializeField] private float gaugeGainPerPress = 6f;
    [SerializeField] private float gaugeDecayPerSec = 8f;

    [Header("Rod Rarity Bonus")]
    [SerializeField] private int tierForBonus10 = 2;
    [SerializeField] private int tierForBonus25 = 3;
    [SerializeField, Range(0f, 1f)] private float bonus10 = 0.1f;
    [SerializeField, Range(0f, 1f)] private float bonus25 = 0.25f;

    private FishArea currentArea;
    private int currentBiomeMask;
    private int currentRodTier;

    public bool IsBusy { get; private set; }

    private Coroutine fishingRoutine;

    private void Awake()
    {
        if (inventorySystem == null) inventorySystem = FindFirstObjectByType<InventorySystem>();
        if (playerMove == null) playerMove = FindFirstObjectByType<PlayerMove>();
        if (minigameUI == null) minigameUI = FindFirstObjectByType<FishingMiniGameUI>();

        SetBiteMark(false);
    }

    private void OnDisable()
    {
        CancelFishing();
    }

    /// <summary>
    /// WaterSystem에서 물 타일 확인 완료 후 호출
    /// </summary>
    public void StartFishingMiniGame(FishArea area, int biomeMask)
    {
        if (IsBusy) return;

        //Fish DB 체크
        if(FishLoader.Instance == null || !FishLoader.Instance.IsLoaded)
        {
            Debug.LogWarning("[FishingSystem] FishLoader 준비 전");
            return;
        }

        //인벤/DB체크
        if(inventorySystem == null || !inventorySystem.IsReady)
        {
            Debug.LogWarning("[FishingSystem] InventorySystem 준비 전");
            return;
        }

        //미니게임 UI 체크
        if(minigameUI == null)
        {
            Debug.LogWarning("[FishingSystem] FishingMiniGame UI 없음");
            return;
        }

        //낚싯대 정보 들고오기
        TryGetActiveItem(out var def);

        //환경값 저장
        currentArea = area;
        currentBiomeMask = biomeMask;
        currentRodTier = def.tier;

        //플레이어 입력 잠금
        IsBusy = true;
        if (playerMove != null) playerMove.enabled = false;

        //느낌표 플레이어 위치에 배치
        PlaceBiteMark();

        //SFX재생
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Fish_Cast);

        //낚시 코루틴 시작
        if (fishingRoutine != null) StopCoroutine(fishingRoutine);
        fishingRoutine = StartCoroutine(FishingSequence());
    }

    private IEnumerator FishingSequence()
    {
        SetBiteMark(false);

        //유예기간 (랜덤 대기)
        float delay = Random.Range(preBiteDelayMin, preBiteDelayMax);
        float t = 0f;
        while (t < delay)
        {
            if (!IsBusy) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        SetBiteMark(true);

        //SFX재생
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Fish_Bite);

        float window = 0f;
        while(window < biteWindowSec)
        {
            if (!IsBusy) yield break;

            bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (pressed)
            {
                //성공 : 느낌표 끄고 미니게임
                SetBiteMark(false);
                fishingRoutine = null;

                //미니게임 시작
                minigameUI.Open(gaugeGainPerPress, gaugeDecayPerSec, OnMiniGameFinished);
                yield break;
            }

            window += Time.unscaledDeltaTime;
            yield return null;
        }

        //실패 : 시간 내 입력 없음
        SetBiteMark(false);
        fishingRoutine = null;

        Debug.Log("[FishingSystem] 낚시 실패! (빨리 안낚아채서 물고기가 도망감)");
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Fish_Fail);
        EndFishingCleanUp();
    }

    private int PickFishId_DB(FishArea area, int biomeMask, int rodTier)
    {
        var db = FishLoader.Instance.fishingDb;
        var fishDb = db.fishDb;
        var rateTable = db.rateTable;

        //현재 weather/timeWindow 결정
        var weather = GetCurrentWeather();
        var timeWindow = GetCurrentTimeWindow();

        //희귀도 먼저 뽑기
        FishRarity rarity = PickRarity(rateTable, area, weather, timeWindow, rodTier);

        //FishTable 후보 필터링
        var candidates = new List<FishDef>(32);
        var weights = new List<int>(32);

        foreach (var fish in fishDb.All)
        {
            if (fish == null) continue;
            if (fish.area != area) continue;
            if (fish.rarity != rarity) continue;

            //biomeMask
            if ((fish.biomeMask & biomeMask) == 0) continue;

            //시간 필터
            if(timeWindow != FishTimeWindow.Any)
            {
                if(!(fish.timeWindow == FishTimeWindow.Always ||
                    fish.timeWindow == FishTimeWindow.Any ||
                    fish.timeWindow == timeWindow))
                    continue;
            }

            int w = Mathf.Max(0, fish.baseWeight);
            if (w <= 0) continue;

            candidates.Add(fish);
            weights.Add(w);
        }

        if (candidates.Count == 0)
            return 0;

        int idx = PickIndexByWeights(weights);
        return candidates[idx].fishId;
    }

    private FishRarity PickRarity(FishingRateTable rateTable, FishArea area, WeatherManager.WeatherType weather, FishTimeWindow timeWindow, int rodTier)
    {
        int wCommon = Mathf.Max(0, rateTable.GetWeight(area, weather, timeWindow, FishRarity.Common));
        int wRare = Mathf.Max(0, rateTable.GetWeight(area, weather, timeWindow, FishRarity.Rare));
        int wEpic = Mathf.Max(0, rateTable.GetWeight(area, weather, timeWindow, FishRarity.Epic));
        int wLegend = Mathf.Max(0, rateTable.GetWeight(area, weather, timeWindow, FishRarity.Legend));

        float bonus = GetRodRarityBonus(rodTier);
        if(bonus > 0f)
        {
            wRare = Mathf.RoundToInt(wRare * (1f + bonus));
            wEpic = Mathf.RoundToInt(wEpic * (1f + bonus));
            wLegend = Mathf.RoundToInt(wLegend * (1f + bonus));
        }

        int total = wCommon + wEpic + wLegend;
        if (total <= 0) return FishRarity.Common;

        int roll = Random.Range(0, total);
        if (roll < wCommon) return FishRarity.Common;
        roll -= wCommon;
        if (roll < wRare) return FishRarity.Rare;
        roll -= wRare;
        if (roll < wEpic) return FishRarity.Epic;
        roll -= wEpic;
        return FishRarity.Legend;
    }

    private float GetRodRarityBonus(int rodTier)
    {
        if (rodTier >= tierForBonus25) return bonus25;
        if (rodTier >= tierForBonus10) return bonus10;
        return 0f;
    }

    private WeatherManager.WeatherType GetCurrentWeather()
    {
        if (WeatherManager.Instance == null) return WeatherManager.WeatherType.Sunny;
        return WeatherManager.Instance.CurrentWeather;
    }

    private FishTimeWindow GetCurrentTimeWindow()
    {
        if (TimeManager.Instance == null) return FishTimeWindow.Morning;

        switch (TimeManager.Instance.CurrentPhase)
        {
            case TimeManager.DayPhase.Morning: return FishTimeWindow.Morning;
            case TimeManager.DayPhase.Afternoon: return FishTimeWindow.Any;
            case TimeManager.DayPhase.Night: return FishTimeWindow.Night;
            default: return FishTimeWindow.Morning;
        }
    }

    private int PickIndexByWeights(List<int> weights)
    {
        int total = 0;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0, weights[i]);
        if (total <= 0) return 0;

        int roll = Random.Range(0, total);
        int acc = 0;

        for(int i = 0; i < weights.Count; i++)
        {
            acc += Mathf.Max(0, weights[i]);
            if (roll < acc) return i;
        }
        return weights.Count - 1;
    }

    private void GiveFishToInventory(int itemId)
    {
        bool ok = inventorySystem.TryPickup(itemId, 1);
        if (!ok)
        {
            //인벤 꽉찼을 때 처리
            print("[Fishing] 인벤토리 가득 참");
        }
    }

    public void CancelFishing()
    {
        if (fishingRoutine != null)
        {
            StopCoroutine(fishingRoutine);
            fishingRoutine = null;
        }

        SetBiteMark(false);

        //미니게임이 열려 있었으면 닫아주기
        if (minigameUI != null)
            minigameUI.Close();

        if (IsBusy)
            EndFishingCleanUp();
    }

    private void EndFishingCleanUp()
    {
        if (playerMove != null) playerMove.enabled = true;
        IsBusy = false;
    }

    private void SetBiteMark(bool on)
    {
        if (biteMarkRoot != null) biteMarkRoot.SetActive(on);
    }

    private void PlaceBiteMark()
    {
        if (biteMarkRoot == null) return;

        Vector3 worldPos = transform.position + biteMarkOffset;

        if (biteMarkRoot.transform.IsChildOf(transform))
        {
            biteMarkRoot.transform.localPosition = biteMarkOffset;
            return;
        }
    }

    private bool TryGetActiveItem(out ItemDef def)
    {
        def = null;
        if(inventorySystem != null && inventorySystem.TryGetActive(out _, out _, out var activeDef))
        {
            def = activeDef;
            return def != null;
        }
        return false;
    }

    private void OnMiniGameFinished(bool success)
    {
        if (success)
        {
            int fishId = PickFishId_DB(currentArea, currentBiomeMask, currentRodTier);
            if(fishId > 0)
            {
                GiveFishToInventory(fishId);
                print($"<color=cyan>[Fishing]</color> 성공! 물고기 획득: {fishId}");
            }
        }
        else
        {
            print("[Fishing] 실패! (이번 단계는 패널티 없음)");
        }

        EndFishingCleanUp();
    }

}
