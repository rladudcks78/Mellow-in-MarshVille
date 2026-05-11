using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GiftTasteLoader : MonoBehaviour
{
    public static GiftTasteLoader Instance { get; private set; }

    [Header("gift_taste 스프레드시트 CSV URL")]
    [SerializeField] private string giftTasteCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;
    [SerializeField] private bool sendNoCacheHeaders = true;
    [SerializeField] private int timeoutSeconds = 10;

    public GiftTasteDatabase Db { get; private set; } = new GiftTasteDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<GiftTasteDatabase> onLoaded;
    public event Action<string> onFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GiftTasteLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
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

        loadRoutine = StartCoroutine(LoadGiftTasteSheet());
    }

    private IEnumerator LoadGiftTasteSheet()
    {
        IsLoaded = false;
        LastError = "";
        Db = new GiftTasteDatabase();

        if (string.IsNullOrWhiteSpace(giftTasteCsvUrl))
        {
            LastError = "giftTasteCsvUrl is empty.";
            Debug.LogError($"[GiftTasteLoader] {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(giftTasteCsvUrl);

        using var req = UnityWebRequest.Get(url);
        req.timeout = Mathf.Max(0, timeoutSeconds);
        ApplyNoCacheHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[GiftTasteLoader] 다운로드 실패 : {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            var rows = CsvUtil.ParseCsv(csvText);

            for (int i = 0; i < rows.Count; i++)
            {
                var def = GiftTasteDef.FromRow(rows[i]);
                if (def == null)
                {
                    Debug.LogWarning($"[GiftTasteLoader] Skip invalid row. index={i}");
                    continue;
                }

                Db.Add(def);
            }

            IsLoaded = true;
            Debug.Log($"[GiftTasteLoader] Loaded GiftTasteDefs : {Db.Count}");
            onLoaded?.Invoke(Db);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[GiftTasteLoader] Parse failed : {e}");
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

    public void Register(Action<GiftTasteDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(Db);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<GiftTasteDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}