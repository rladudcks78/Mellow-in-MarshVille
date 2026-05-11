using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// gift_reaction 시트 CSV 로더
/// - GiftTasteLoader와 동일한 패턴(캐시버스트/노캐시/timeout/Reload/Register)
/// - CsvUtil.ParseCsv 사용(따옴표/콤마/개행 지원 버전으로 교체한 것 전제)
/// </summary>
public class GiftReactionLoader : MonoBehaviour
{
    public static GiftReactionLoader Instance { get; private set; }

    [Header("gift_reaction CSV URL")]
    [SerializeField] private string giftReactionCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;
    [SerializeField] private bool sendNoCacheHeaders = true;
    [SerializeField] private int timeoutSeconds = 10;

    public GiftReactionDatabase Db { get; private set; } = new GiftReactionDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<GiftReactionDatabase> onLoaded;
    public event Action<string> onFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GiftReactionLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 씬 전용 로더라면 DontDestroyOnLoad 사용하지 않음
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (loadOnStart)
            Reload();
    }

    private void OnDestroy()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        if (Instance == this)
            Instance = null;
    }

    public void Reload()
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadGiftReactionSheet());
    }

    private IEnumerator LoadGiftReactionSheet()
    {
        IsLoaded = false;
        LastError = "";
        Db = new GiftReactionDatabase();

        if (string.IsNullOrWhiteSpace(giftReactionCsvUrl))
        {
            LastError = "giftReactionCsvUrl is empty.";
            Debug.LogError($"[GiftReactionLoader] {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(giftReactionCsvUrl);

        using var req = UnityWebRequest.Get(url);
        req.timeout = Mathf.Max(0, timeoutSeconds);
        ApplyNoCacheHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[GiftReactionLoader] Download failed : {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<Dictionary<string, string>> rows = CsvUtil.ParseCsv(csvText);

            for (int i = 0; i < rows.Count; i++)
            {
                var def = GiftReactionDef.FromRow(rows[i]);
                if (def == null)
                {
                    Debug.LogWarning($"[GiftReactionLoader] Skip invalid row. index={i}");
                    continue;
                }

                Db.Add(def);
            }

            IsLoaded = true;
            Debug.Log($"[GiftReactionLoader] Loaded GiftReactionDefs : {Db.Count}");
            onLoaded?.Invoke(Db);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[GiftReactionLoader] Parse failed : {e}");
            onFailed?.Invoke(LastError);
        }
        finally
        {
            loadRoutine = null;
        }
    }

    private string BuildRequestUrl(string baseUrl)
    {
        if (!cacheBustUrl) return baseUrl;

        string sep = baseUrl.Contains("?") ? "&" : "?";
        return baseUrl + sep + "t=" + DateTime.UtcNow.Ticks;
    }

    private void ApplyNoCacheHeaders(UnityWebRequest req)
    {
        if (!sendNoCacheHeaders) return;

        req.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
        req.SetRequestHeader("Pragma", "no-cache");
        req.SetRequestHeader("Expires", "0");
    }

    public void Register(Action<GiftReactionDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(Db);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<GiftReactionDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}