using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 스프레드시트 CSV(또는 일반 CSV URL)를 다운로드해서 QuestDatabase를 구축합니다.
/// - Reload 지원(시트 갱신 즉시 반영용)
/// - 캐시 회피(옵션)
/// </summary>
public class QuestLoader : MonoBehaviour
{
    public static QuestLoader Instance { get; private set; }

    [Header("구글 스프레드시트 CSV URL")]
    [SerializeField] private string questCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;        // URL에 t=... 붙여 캐시 회피
    [SerializeField] private bool sendNoCacheHeaders = true;  // Cache-Control/Pragma/Expires 헤더

    public QuestDatabase QuestDb { get; private set; } = new QuestDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<QuestDatabase> onLoaded;
    public event Action<string> onFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[QuestLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
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

    /// <summary>
    /// 시트 갱신 내용을 즉시 반영하고 싶을 때 호출.
    /// - 이미 로드됐어도 다시 다운로드/파싱/Build 합니다.
    /// - 진행 중 로드가 있으면 중단하고 새로 시작합니다.
    /// </summary>
    public void Reload()
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadQuestSheet());
    }

    private IEnumerator LoadQuestSheet()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(questCsvUrl))
        {
            LastError = "questCsvUrl is empty.";
            Debug.LogError($"[QuestLoader] {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(questCsvUrl);

        using var req = UnityWebRequest.Get(url);
        ApplyNoCacheHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[QuestLoader] 다운로드 실패 : {LastError}");
            onFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<QuestDef> defs = ParseQuestCsv(csvText);
            QuestDb.Build(defs);

            IsLoaded = true;
            Debug.Log($"[QuestLoader] Loaded QuestDefs : {defs.Count}");

            onLoaded?.Invoke(QuestDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[QuestLoader] Parse failed : {e}");
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

    private List<QuestDef> ParseQuestCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<QuestDef>();

        string[] headers = CsvUtil.SplitCsvLine(lines[0]);
        var result = new List<QuestDef>(Mathf.Max(0, lines.Length - 1));

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = CsvUtil.SplitCsvLine(lines[i].TrimEnd('\r'));
            var row = CsvUtil.MakeRow(headers, cols);

            QuestDef def = QuestDef.FromRow(row);
            if (def == null)
            {
                Debug.LogWarning($"[QuestLoader] Skip invalid row. line={i + 1}, raw={lines[i]}");
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    public void Register(Action<QuestDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(QuestDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<QuestDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}