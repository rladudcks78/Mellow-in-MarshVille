using System.Collections.Generic;
using UnityEngine;

public class HarvestRespawnManager : MonoBehaviour
{ 
    [Header("자동 수집 옵션")]
    [SerializeField] private bool includeInactive = true;

    // 자동 수집된 모든 채집물
    private readonly List<Harvestable> all = new();

    // key: 채집물, value: 리스폰 예정 absolute minute
    private readonly Dictionary<Harvestable, long> respawnAt = new();

    // TimeManager 데이터를 이용해 현재 총 플레이 시간(분)을 계산하는 프로퍼티
    private long CurrentTotalMinutes
    {
        get
        {
            if (TimeManager.Instance == null) return 0;
            // (일차 - 1) * 24시간 * 60분 + 현재 시 * 60분 + 현재 분
            return (TimeManager.Instance.CurrentDay - 1) * 1440 +
                   TimeManager.Instance.CurrentHour * 60 +
                   TimeManager.Instance.CurrentMinute;
        }
    }

    void Awake()
    {
        CollectAllHarvestables();

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += HandleNewDay;
    }

    void OnDestroy()
    {
        foreach (var h in all)
        {
            if (h == null) continue;
            h.OnHarvested -= HandleHarvested;
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= HandleNewDay;
    }

    void Update()
    {
        if (TimeManager.Instance == null || respawnAt.Count == 0) return;

        long now = CurrentTotalMinutes;

        List<Harvestable> ready = null;
        foreach (var kv in respawnAt)
        {
            if (now >= kv.Value)
            {
                ready ??= new List<Harvestable>();
                ready.Add(kv.Key);
            }
        }

        if (ready == null) return;

        foreach (var h in ready)
        {
            if (h != null)
                h.gameObject.SetActive(true); // 리스폰 [web:120]
            respawnAt.Remove(h);
        }
    }

    void CollectAllHarvestables()
    {
        all.Clear();

        var found = FindObjectsByType<Harvestable>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (var h in found)
        {
            if (h == null) continue;
            all.Add(h);
            h.OnHarvested -= HandleHarvested; // 중복 구독 방지
            h.OnHarvested += HandleHarvested;
        }

        Debug.Log($"[채집] 채집 가능 물체 자동 수집 완료: {all.Count}개");
    }

    void HandleHarvested(Harvestable h)
    {
        if (TimeManager.Instance == null) return;

        long now = CurrentTotalMinutes;
        respawnAt[h] = now + h.RespawnAfterMinutes;
    }

    public void RespawnAllNow()
    {
        for (int i = 0; i < all.Count; i++)
        {
            var h = all[i];
            if (h == null) continue;
            h.gameObject.SetActive(true); // [web:120]
        }

        respawnAt.Clear();
        Debug.Log("[채집] 새 날 시작: 전체 강제 리스폰");
    }

    // TimeManager의 OnNewDay 이벤트(Action<int>)와 연결하기 위한 어댑터 함수
    private void HandleNewDay(int day)
    {
        RespawnAllNow();
    }

    // (선택) 씬에 채집물이 런타임에 추가될 수도 있으면, 필요할 때 호출
    public void RebuildRegistry()
    {
        // 기존 구독 해제 후 재수집
        foreach (var h in all)
        {
            if (h == null) continue;
            h.OnHarvested -= HandleHarvested;
        }

        CollectAllHarvestables();
    }
}
