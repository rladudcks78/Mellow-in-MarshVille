using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RecipeLoader : MonoBehaviour
{
    public static RecipeLoader Instance { get; private set; }

    [Header("구글 스프레드 시트 CSV 주소")]
    [SerializeField] private string recipeCsvUrl;

    [Header("옵션")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool cacheBustUrl = true;
    [SerializeField] private bool sendNoCacheHeaders = true;

    public RecipeDatabase RecipeDb { get; private set; } = new RecipeDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<RecipeDatabase> OnLoaded;
    public event Action<string> OnFailed;

    private Coroutine loadRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[RecipeLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
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

        loadRoutine = StartCoroutine(LoadRecipeSheet());
    }

    private IEnumerator LoadRecipeSheet()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(recipeCsvUrl))
        {
            LastError = "recipeCsvUrl is empty.";
            Debug.LogError($"[RecipeLoader] {LastError}");
            OnFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string url = BuildRequestUrl(recipeCsvUrl);

        using var req = UnityWebRequest.Get(url);
        ApplyNoCacheHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[RecipeLoader] 다운로드 실패: {LastError}");
            OnFailed?.Invoke(LastError);
            loadRoutine = null;
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<RecipeDef> recipes = ParseRecipeCsv(csvText);

            RecipeDb.Build(recipes);

            IsLoaded = true;
            Debug.Log($"[RecipeLoader] 로드 성공. {recipes.Count}개의 레시피 등록됨.");

            OnLoaded?.Invoke(RecipeDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[RecipeLoader] 파싱 중 에러 발생: {e}");
            OnFailed?.Invoke(LastError);
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

    private List<RecipeDef> ParseRecipeCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<RecipeDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<RecipeDef>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < cols.Length; c++)
            {
                row[headers[c].Trim()] = cols[c].Trim();
            }

            RecipeDef def = CreateRecipeFromRow(row);

            if (def != null) result.Add(def);
        }

        return result;
    }

    private RecipeDef CreateRecipeFromRow(Dictionary<string, string> row)
    {
        string Get(string key) => row.ContainsKey(key) ? row[key] : "";
        int GetInt(string key) => int.TryParse(Get(key), out int v) ? v : 0;
        float GetFloat(string key, float defVal = 0f) => float.TryParse(Get(key), out float v) ? v : defVal;

        int id = GetInt("RecipeID");
        if (id == 0) return null;

        RecipeDef def = new RecipeDef();

        def.recipeId = id;
        def.resultItemId = GetInt("ResultItemID");
        def.recipeName = Get("RecipeName");

        for (int k = 1; k <= 5; k++)
        {
            int ingId = GetInt($"Ingredient{k}");
            if (ingId > 0)
            {
                def.ingredients.Add(ingId);
            }
        }

        def.sloppyPenalty = GetFloat("SloppyPenalty", 0.5f);
        def.perfectBonus = GetFloat("PerfectBonus", 1.5f);

        def.description = Get("Description");
        def.discoveryHint = Get("DiscoveryHint");

        def.buyPrice = GetInt("buyPrice");
        def.sellPrice = GetInt("sellPrice");
        def.spritePath = Get("spritePath").Trim().Trim('"');

        return def;
    }

    private string[] SplitCsvLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (ch == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        list.Add(sb.ToString());
        return list.ToArray();
    }

    public void Register(Action<RecipeDatabase> onLoaded, Action<string> onFailed = null)
    {
        if (onLoaded != null) OnLoaded += onLoaded;
        if (onFailed != null) OnFailed += onFailed;

        if (IsLoaded && onLoaded != null)
            onLoaded(RecipeDb);

        if (!string.IsNullOrEmpty(LastError) && onFailed != null)
            onFailed(LastError);
    }

    public void Unregister(Action<RecipeDatabase> onLoaded, Action<string> onFailed = null)
    {
        if (onLoaded != null) OnLoaded -= onLoaded;
        if (onFailed != null) OnFailed -= onFailed;
    }
}