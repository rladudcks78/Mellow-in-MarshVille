using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// NPC 호감도(관계) 관리.
/// </summary>
public class RelationshipManager : MonoBehaviour
{
    public static RelationshipManager Instance;

    [Header("기본 설정")]
    [SerializeField] private int maxAffection = 100;

    [Header("UI 연결")]
    [SerializeField] private AffectionUI affectionUI;

    [Header("NPC DB(선택)")]
    [Tooltip("NpcLoader가 씬에 있으면 자동으로 Instance를 쓰지만, 명시 연결도 가능")]
    [SerializeField] private NpcLoader npcLoader;

    [Header("--- Time System Link ---")]
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private bool autoLinkTimeManager = true;

    private bool _daySyncInitialized = false;

    // --- npcId 기반 ---
    private readonly Dictionary<int, int> affectionByNpcId = new Dictionary<int, int>();

    // --- 우정 퀘스트 완료 상태 ---
    // key: npcId, value: 완료된 게이트(20/40/60/80/100) 집합
    private readonly Dictionary<int, HashSet<int>> clearedFriendshipGates = new Dictionary<int, HashSet<int>>();
    private static readonly int[] FriendshipGateThresholds = { 20, 40, 60, 80, 100 };

    // - 추후 GameTime/Calendar 시스템이 생기면 이 값은 거기서 공급받게 교체합니다.
    [Header("게임 내 하루(임시)")]
    [Tooltip("게임 내 '오늘'을 나타내는 키. 예: 0,1,2... (세이브에 포함 권장)")]
    [SerializeField] private int currentDayKey = 0;

    public int CurrentDayKey => currentDayKey;     // 다른 시스템이 '오늘' 판정에 사용할 수 있는 키.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // TimeManager 자동 연결
        if (autoLinkTimeManager)
        {
            LinkToTimeManager();
        }
    }

    private void OnDestroy()
    {
        if (timeManager != null)
        {
            timeManager.OnNewDay -= OnTimeManagerNewDay;
        }
    }


    private void LinkToTimeManager()
    {
        if (timeManager == null)
        {
            timeManager = TimeManager.Instance ?? FindAnyObjectByType<TimeManager>();
        }

        if (timeManager != null && !_daySyncInitialized)
        {
            // 초기 동기화: TimeManager의 CurrentDay를 기준으로 currentDayKey 설정
            currentDayKey = timeManager.CurrentDay - 1;  // TimeManager는 1부터 시작하므로 -1

            // 새 날짜 이벤트 구독
            timeManager.OnNewDay += OnTimeManagerNewDay;
            _daySyncInitialized = true;

            Debug.Log($"[RelationshipManager] TimeManager와 동기화 완료. currentDayKey={currentDayKey}");
        }
    }

    private void OnTimeManagerNewDay(int newDay)
    {
        // TimeManager에서 새 날짜 발생 시 자동으로 dayKey 증가
        currentDayKey = newDay - 1;
        Debug.Log($"[RelationshipManager] TimeManager 새 날짜 동기화: Day={newDay}, currentDayKey={currentDayKey}");
    }


    private NpcLoader GetNpcLoader()
    {
        if (npcLoader != null) return npcLoader;
        if (NpcLoader.Instance != null) return NpcLoader.Instance;
        return null;
    }

    private string ResolveDisplayName(int npcId)
    {
        var loader = GetNpcLoader();
        if (loader != null && loader.IsLoaded && loader.NpcDb != null)
        {
            if (loader.NpcDb.TryGet(npcId, out var def) && def != null && !string.IsNullOrEmpty(def.npcDisplayName))
                return def.npcDisplayName;
        }
        return $"NPC({npcId})";
    }

    /// <summary>
    /// 특정 NPC의 우정 게이트를 완료 처리합니다.
    ///  권장 호출 시점: 우정 퀘스트 보상 수령(완료)
    /// </summary>
    public void SetFriendshipGateCleared(int npcId, int threshold, bool cleared)
    {
        if (npcId <= 0) return;

        if (Array.IndexOf(FriendshipGateThresholds, threshold) < 0)
        {
            Debug.LogWarning($"[Relationship] Invalid friendship gate threshold: {threshold} ");
            return;
        }

        if (!clearedFriendshipGates.TryGetValue(npcId, out var set) || set == null)
        {
            set = new HashSet<int>();
            clearedFriendshipGates[npcId] = set;
        }

        if (cleared) set.Add(threshold);
        else set.Remove(threshold);

        // 게이트 상태가 바뀌면 현재 호감도도 캡 규칙에 맞게 재클램프
        int cur = GetAffection(npcId);
        int clamped = ClampByFriendshipCap(npcId, Mathf.Clamp(cur, 0, maxAffection));
        if (clamped != cur)
        {
            affectionByNpcId[npcId] = clamped;
            Debug.Log($"[Relationship] Gate change reclamp: npcId={npcId}, {cur} -> {clamped}");
        }
    }

    private bool IsFriendshipGateCleared(int npcId, int threshold)
    {
        if (!clearedFriendshipGates.TryGetValue(npcId, out var set) || set == null) return false;
        return set.Contains(threshold);
    }

    /// <summary>
    /// 우정 퀘스트 미완료 시 호감도 캡 적용
    /// </summary>
    private int ClampByFriendshipCap(int npcId, int affectionValue)
    {
        // 기본 0~100 클램프
        int v = Mathf.Clamp(affectionValue, 0, maxAffection);

        // 각 구간 캡 적용
        if (v >= 20 && !IsFriendshipGateCleared(npcId, 20)) v = Mathf.Min(v, 19);
        if (v >= 40 && !IsFriendshipGateCleared(npcId, 40)) v = Mathf.Min(v, 39);
        if (v >= 60 && !IsFriendshipGateCleared(npcId, 60)) v = Mathf.Min(v, 59);
        if (v >= 80 && !IsFriendshipGateCleared(npcId, 80)) v = Mathf.Min(v, 79);

        return v;
    }


    public void IncreaseAffection(int npcId, int amount)
    {
        if (npcId <= 0) return;
        if (amount == 0) return;

        if (!affectionByNpcId.ContainsKey(npcId))
            affectionByNpcId[npcId] = 0;

        int oldAffection = affectionByNpcId[npcId];
        int next = Mathf.Clamp(oldAffection + amount, 0, maxAffection);

        // 우정 게이트/캡 규칙 반영
        next = ClampByFriendshipCap(npcId, next);

        affectionByNpcId[npcId] = next;

        Debug.Log($"[Relationship] npcId={npcId} ({ResolveDisplayName(npcId)}) 호감도 +{amount} ({oldAffection} → {next})");
    }

    public void DecreaseAffection(int npcId, int amount)
    {
        if (npcId <= 0) return;
        if (amount <= 0) return;

        if (!affectionByNpcId.ContainsKey(npcId))
            affectionByNpcId[npcId] = 0;

        int oldAffection = affectionByNpcId[npcId];
        int next = Mathf.Clamp(oldAffection - amount, 0, maxAffection);

        affectionByNpcId[npcId] = next;

        Debug.Log($"[Relationship] npcId={npcId} ({ResolveDisplayName(npcId)}) 호감도 -{amount} ({oldAffection} → {next})");
    }

    public int GetAffection(int npcId)
    {
        if (npcId <= 0) return 0;
        return affectionByNpcId.TryGetValue(npcId, out int v) ? v : 0;
    }

    /// <summary>
    /// 표시 단계 = floor(호감도/10), 0~10
    /// </summary>
    public int GetAffectionStage10(int npcId)
    {
        int affection = GetAffection(npcId);
        int stage = Mathf.FloorToInt(affection / 10f);
        return Mathf.Clamp(stage, 0, 10);
    }

    public void ShowAffectionUI(int npcId)
    {
        if (affectionUI == null) return;

        int affection = GetAffection(npcId);
        string displayName = ResolveDisplayName(npcId);

        affectionUI.ShowAffection(displayName, affection);
    }

    public void HideAffectionUI()
    {
        if (affectionUI != null)
            affectionUI.HideAffectionUI();
    }

    public int GetCurrentCapCeiling(int npcId)
    {
        if (!IsFriendshipGateCleared(npcId, 20)) return 19;
        if (!IsFriendshipGateCleared(npcId, 40)) return 39;
        if (!IsFriendshipGateCleared(npcId, 60)) return 59;
        if (!IsFriendshipGateCleared(npcId, 80)) return 79;
        return maxAffection; // 100
    }

    // =========================
    // TODO: Save/Load 연결용 API
    // =========================

    public RelationshipSaveData CaptureSnapshot()
    {
        var data = new RelationshipSaveData();

        foreach (var kv in affectionByNpcId)
        {
            data.affections.Add(new NpcAffectionEntry
            {
                npcId = kv.Key,
                affection = kv.Value
            });
        }

        foreach (var kv in clearedFriendshipGates)
        {
            var entry = new NpcFriendshipGatesEntry { npcId = kv.Key };
            if (kv.Value != null)
            {
                entry.gates.AddRange(kv.Value);
                entry.gates.Sort();
            }
            data.clearedGates.Add(entry);
        }

        data.currentDayKey = currentDayKey;

        return data;
    }

    public void RestoreSnapshot(RelationshipSaveData data)
    {
        affectionByNpcId.Clear();
        clearedFriendshipGates.Clear();

        if (data == null) return;

                currentDayKey = Mathf.Max(0, data.currentDayKey);

        if (data.affections != null)
        {
            for (int i = 0; i < data.affections.Count; i++)
            {
                var e = data.affections[i];
                if (e == null) continue;
                if (e.npcId <= 0) continue;

                affectionByNpcId[e.npcId] = Mathf.Clamp(e.affection, 0, maxAffection);
            }
        }

        if (data.clearedGates != null)
        {
            for (int i = 0; i < data.clearedGates.Count; i++)
            {
                var e = data.clearedGates[i];
                if (e == null) continue;
                if (e.npcId <= 0) continue;

                var set = new HashSet<int>();
                if (e.gates != null)
                {
                    for (int j = 0; j < e.gates.Count; j++)
                    {
                        int th = e.gates[j];
                        if (Array.IndexOf(FriendshipGateThresholds, th) < 0) continue;
                        set.Add(th);
                    }
                }

                if (set.Count > 0)
                    clearedFriendshipGates[e.npcId] = set;
            }
        }

        // 복원 후 현재 게이트 규칙에 맞춰 상승 캡 정리
        foreach (var npcId in new List<int>(affectionByNpcId.Keys))
            affectionByNpcId[npcId] = ClampByFriendshipCap(npcId, affectionByNpcId[npcId]);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("=== 치트/디버그 ===")]
    [SerializeField] private bool enableCheats = false;

    [ContextMenu("치트: 게이트+호감도 모두 풀 해금")]
    public void Cheat_SetAllAffectionTo90()
    {
        if (!enableCheats) return;

        var loader = GetNpcLoader();
        if (loader?.NpcDb == null) return;

        for (int index = 0; index < loader.NpcDb.Count; index++)
        {
            if (loader.NpcDb.TryGet(index, out var npcDef) && npcDef.npcId > 0)
            {
                int npcId = npcDef.npcId;

                // 1. 모든 우정 게이트 해금 (캡 해제)
                foreach (int threshold in FriendshipGateThresholds)
                {
                    SetFriendshipGateCleared(npcId, threshold, true);
                }

                // 2. 호감도 90 설정
                Cheat_SetAffection(npcId, 90);
            }
        }

        Debug.Log("[치트] 모든 NPC: 게이트 풀 해금 + 호감도 90 완료!");
    }



    public void Cheat_SetAffection(int npcId, int targetAffection)
    {
        if (!enableCheats) return;

        IncreaseAffection(npcId, targetAffection - GetAffection(npcId));
        Debug.Log($"[치트] NPC {npcId} ({ResolveDisplayName(npcId)}) 호감도 → {targetAffection}");
    }

    private void Update()
    {
        if (!enableCheats || !Keyboard.current.f9Key.wasPressedThisFrame) return;
        Cheat_SetAllAffectionTo90();
    }

#endif
}
