using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveBootstrapV1 : MonoBehaviour
{
    [SerializeField] private string inGameSceneName = "InGame";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SaveManagerV1.Instance == null) return;
        if (!SaveManagerV1.Instance.PendingLoad) return;
        if (scene.name != inGameSceneName) return;

        // 로드 직후 시간/날씨 자동 초기화가 세이브 적용값을 덮지 않도록 1회 스킵
        var tm = FindAnyObjectByType<TimeManager>();
        if (tm != null) tm.SkipAutoStartOnce();

        var wm = FindAnyObjectByType<WeatherManager>();
        if (wm != null) wm.SkipDetermineOnce();

        if (!SaveManagerV1.Instance.LoadNowContinue(out var data))
        {
            SaveManagerV1.Instance.ClearPendingLoad();
            Debug.LogWarning("[SaveBootstrapV1] Continue load failed. PendingLoad cleared");
            return;
        }

        data.ApplyToWorld();

        SaveManagerV1.Instance.ClearPendingLoad();
        Debug.Log($"[SaveBootstrapV1] Applied continue save slot = {SaveManagerV1.ContinueSlotIndex}");
    }
}