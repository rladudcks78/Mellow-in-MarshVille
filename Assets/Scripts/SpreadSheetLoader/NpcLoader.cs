using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NpcLoader : MonoBehaviour
{
    public static NpcLoader Instance { get; private set; }

    [Header("구글 스프레드시트 CSV URL")]
    [SerializeField] private string npcCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;
    [SerializeField] private bool sendNoCacheHeaders = true;

    public NpcDatabase NpcDb { get; private set; } = new NpcDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<NpcDatabase> onLoaded;
    public event Action<string> onFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[NpcLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
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

        loadRoutine = StartCoroutine(LoadNpcSheet());
    }

    private IEnumerator LoadNpcSheet()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(npcCsvUrl))
        {
            LastError = "npcCsvUrl is empty.";
            Debug.LogError($"[NpcLoader] {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(npcCsvUrl);

        using var req = UnityWebRequest.Get(url);
        ApplyNoCacheHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[NpcLoader] 다운로드 실패 : {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<NpcDef> defs = ParseNpcCsv(csvText);
            NpcDb.Build(defs);

            IsLoaded = true;
            Debug.Log($"[NpcLoader] Loaded NpcDefs : {defs.Count}");
            onLoaded?.Invoke(NpcDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[NpcLoader] Parse failed : {e}");
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

    private List<NpcDef> ParseNpcCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<NpcDef>();

        string[] headers = CsvUtil.SplitCsvLine(lines[0]);
        var result = new List<NpcDef>(Mathf.Max(0, lines.Length - 1));

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = CsvUtil.SplitCsvLine(lines[i].TrimEnd('\r'));
            var row = CsvUtil.MakeRow(headers, cols);

            NpcDef def = NpcDef.FromRow(row);
            if (def == null)
            {
                Debug.LogWarning($"[NpcLoader] Skip invalid row. line={i + 1}, raw={lines[i]}");
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    public void Register(Action<NpcDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(NpcDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<NpcDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}