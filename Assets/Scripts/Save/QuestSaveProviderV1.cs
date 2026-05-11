using System.Collections;
using UnityEngine;

/// <summary>
/// SaveGameV1.quests(QuestSaveDataV1) <-> QuestManagerCsv(QuestSaveData) 브릿지.
/// - Capture: QuestManagerCsv.CaptureSnapshot() -> QuestSaveDataV1 변환
/// - Apply: QuestSaveDataV1 -> QuestSaveData 변환 후 RestoreSnapshot()
///
/// 추가로 "인벤토리 DB 준비 전" 때문에 Collect 진행도 동기화가 누락될 수 있어,
/// 인벤토리가 IsReady가 되는 타이밍에 한 번 더 RestoreSnapshot()을 재호출(동기화 유도)한다.
/// </summary>
public sealed class QuestSaveProviderV1 : MonoBehaviour, IQuestSaveProviderV1
{
    [Header("Optional direct refs (auto-find if null)")]
    [SerializeField] private QuestManagerCsv questManager;
    [SerializeField] private InventorySystem inventorySystem;

    private Coroutine _retryRoutine;

    private void Awake()
    {
        if (questManager == null)
            questManager = FindAnyObjectByType<QuestManagerCsv>();

        if (inventorySystem == null)
            inventorySystem = FindAnyObjectByType<InventorySystem>();
    }

    private void OnDisable()
    {
        if(_retryRoutine != null)
        {
            StopCoroutine(_retryRoutine);
            _retryRoutine = null;
        }
    }

    public QuestSaveDataV1 CaptureQuest()
    {
        var qm = questManager != null ? questManager : QuestManagerCsv.Instance;
        if (qm == null)
            return new QuestSaveDataV1();

        // QuestManagerCsv의 DTO(QuestSaveData) -> V1 DTO로 변환
        QuestSaveData snap = qm.CaptureSnapshot();
        return ConvertToV1(snap);
    }

    public void ApplyQuest(QuestSaveDataV1 data)
    {
        if (data == null) return;
        data.Normalize();

        var qm = questManager != null ? questManager : QuestManagerCsv.Instance;
        if (qm == null)
        {
            // 아직 QuestManager가 없으면 생길 때까지 기다렸다가 적용
            if (_retryRoutine != null) StopCoroutine(_retryRoutine);
            _retryRoutine = StartCoroutine(ApplyWhenQuestManagerAppears(data));
            return;
        }

        // 1차 즉시 복원
        qm.RestoreSnapshot(ConvertFromV1(data));

        // 2차: 인벤/DB 준비 뒤 Collect 동기화가 필요할 수 있으니 재복원(동기화 유도)
        if (_retryRoutine != null) StopCoroutine(_retryRoutine);
        _retryRoutine = StartCoroutine(RetryRestoreForInventorySync(data));
    }

    private IEnumerator ApplyWhenQuestManagerAppears(QuestSaveDataV1 data)
    {
        // 너무 오래 기다리면 그냥 포기(무한 코루틴 방지)
        const int maxFrames = 600; // 대략 10초 내외(프레임 기준)
        int f = 0;

        while (QuestManagerCsv.Instance == null && f < maxFrames)
        {
            f++;
            yield return null;
        }

        var qm = QuestManagerCsv.Instance;
        if (qm == null)
        {
            Debug.LogWarning("[QuestSaveProviderV1] QuestManagerCsv not found. ApplyQuest skipped.");
            yield break;
        }

        questManager = qm;

        qm.RestoreSnapshot(ConvertFromV1(data));

        if (_retryRoutine != null) StopCoroutine(_retryRoutine);
        _retryRoutine = StartCoroutine(RetryRestoreForInventorySync(data));
    }

    private IEnumerator RetryRestoreForInventorySync(QuestSaveDataV1 data)
    {
        var qm = questManager != null ? questManager : QuestManagerCsv.Instance;
        if (qm == null) yield break;

        var inv = inventorySystem != null ? inventorySystem : FindAnyObjectByType<InventorySystem>();

        // 인벤이 준비되면(아이템DB 로드 완료) QuestManagerCsv.RestoreSnapshot 내부의
        // "복원 후 인벤 동기화(CollectItems)"가 동작할 수 있음.
        const int maxFrames = 900;
        int f = 0;

        while ((inv == null || !inv.IsReady) && f < maxFrames)
        {
            f++;
            if (inv == null) inv = FindAnyObjectByType<InventorySystem>();
            yield return null;
        }

        if (inv == null || !inv.IsReady)
            yield break;

        // 재복원(동일 데이터로 idempotent) -> 내부 syncCollectProgressFromInventoryIfNeeded가 적용될 확률 ↑
        qm.RestoreSnapshot(ConvertFromV1(data));
    }

    // -------------------------
    // Converters
    // -------------------------

    private static QuestSaveDataV1 ConvertToV1(QuestSaveData src)
    {
        var v1 = new QuestSaveDataV1();

        if (src?.claimedQuestIds != null)
            v1.claimedQuestIds.AddRange(src.claimedQuestIds);

        if (src?.activeStates != null)
        {
            for (int i = 0; i < src.activeStates.Count; i++)
            {
                var e = src.activeStates[i];
                if (e == null) continue;

                v1.activeStates.Add(new QuestStateEntryV1
                {
                    questId = e.questId,
                    currentAmount = e.currentAmount,
                    objectiveCompleted = e.objectiveCompleted,
                    rewardClaimed = e.rewardClaimed
                });
            }
        }

        v1.Normalize();
        return v1;
    }

    private static QuestSaveData ConvertFromV1(QuestSaveDataV1 src)
    {
        var dst = new QuestSaveData();

        if (src?.claimedQuestIds != null)
            dst.claimedQuestIds.AddRange(src.claimedQuestIds);

        if (src?.activeStates != null)
        {
            for (int i = 0; i < src.activeStates.Count; i++)
            {
                var e = src.activeStates[i];
                if (e == null) continue;

                dst.activeStates.Add(new QuestStateEntry
                {
                    questId = e.questId,
                    currentAmount = e.currentAmount,
                    objectiveCompleted = e.objectiveCompleted,
                    rewardClaimed = e.rewardClaimed
                });
            }
        }

        return dst;
    }
}