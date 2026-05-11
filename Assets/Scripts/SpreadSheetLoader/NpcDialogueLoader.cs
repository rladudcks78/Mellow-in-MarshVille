using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NpcDialogueLoader : MonoBehaviour
{
    public static NpcDialogueLoader Instance { get; private set; }

    [Header("구글 스프레드시트 CSV URL")]
    [SerializeField] private string dialogueCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;
    [SerializeField] private bool sendNoCacheHeaders = true;

    public NpcDialogueDatabase DialogueDb { get; private set; } = new NpcDialogueDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<NpcDialogueDatabase> OnLoaded;
    public event Action<string> OnLoadFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[NpcDialogueLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
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
            ReloadDialogues();
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

    public void ReloadDialogues()
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(LoadDialoguesCoroutine());
    }

    private IEnumerator LoadDialoguesCoroutine()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(dialogueCsvUrl))
        {
            LastError = "dialogueCsvUrl is empty.";
            Debug.LogError($"[NpcDialogueLoader] {LastError}");
            OnLoadFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(dialogueCsvUrl);

        using var req = UnityWebRequest.Get(url);
        if (sendNoCacheHeaders)
        {
            req.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
            req.SetRequestHeader("Pragma", "no-cache");
            req.SetRequestHeader("Expires", "0");
        }

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[NpcDialogueLoader] 다운로드 실패 : {LastError}");
            OnLoadFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            var rows = CsvUtil.ParseCsv(csvText);
            if (rows == null || rows.Count == 0)
                throw new Exception("CSV 파싱 실패 또는 빈 파일");

            var newDb = new NpcDialogueDatabase();
            foreach (var row in rows)
            {
                var def = NpcDialogueDef.FromRow(row);
                if (def != null) newDb.Add(def);
            }

            DialogueDb = newDb;
            IsLoaded = true;

            Debug.Log($"[NpcDialogueLoader] 로드 완료: {DialogueDb.Count}개 대사");
            OnLoaded?.Invoke(DialogueDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[NpcDialogueLoader] Parse failed : {e}");
            OnLoadFailed?.Invoke(LastError);
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

    public void Register(Action<NpcDialogueDatabase> onLoaded, Action<string> onFailed = null)
    {
        if (onLoaded != null) OnLoaded += onLoaded;
        if (onFailed != null) OnLoadFailed += onFailed;

        if (IsLoaded && onLoaded != null) onLoaded(DialogueDb);
        if (!string.IsNullOrEmpty(LastError) && onFailed != null) onFailed(LastError);
    }

    public void Unregister(Action<NpcDialogueDatabase> onLoaded, Action<string> onFailed = null)
    {
        if (onLoaded != null) OnLoaded -= onLoaded;
        if (onFailed != null) OnLoadFailed -= onFailed;
    }
}