using UnityEngine;

public sealed class AutoSaveOnDayChangeV1 : MonoBehaviour
{
    private bool _initialized = false;
    private int _lastSeenDay = -1;

    private void OnEnable()
    {
        TryBind();
    }

    private void Start()
    {
        if (TimeManager.Instance != null) TryBind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void TryBind()
    {
        if (TimeManager.Instance == null) return;

        Unbind();
        TimeManager.Instance.OnDayEndFinish += HandleDayEndFinish;

        _initialized = false;
        _lastSeenDay = TimeManager.Instance.CurrentDay;
    }

    private void Unbind()
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.OnDayEndFinish -= HandleDayEndFinish;
    }

    private void HandleDayEndFinish()
    {
        if (TimeManager.Instance == null) return;

        int day = TimeManager.Instance.CurrentDay;

        if (day == _lastSeenDay) return;
        _lastSeenDay = day;

        if (SaveManagerV1.Instance == null)
        {
            Debug.LogWarning("[AutoSaveOnDayChangeV1] SaveManagerV1.Instance is null");
            return;
        }

        bool ok = SaveManagerV1.Instance.SaveNowContinue();

        Debug.Log(ok
            ? $"[AutoSaveOnDayChangeV1] Auto-saved (day={day}, slot={SaveManagerV1.ContinueSlotIndex})"
            : $"[AutoSaveOnDayChangeV1] Auto-save FAILED (day={day}, slot={SaveManagerV1.ContinueSlotIndex})");
    }
}