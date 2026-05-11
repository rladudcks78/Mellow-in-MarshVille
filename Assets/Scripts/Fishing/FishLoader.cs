using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FishLoader : MonoBehaviour
{
    public static FishLoader Instance { get; private set; }

    [Header("FishTable, FishingRates CSV URL")]
    [SerializeField] private string fishCsvUrl;     // FishTable
    [SerializeField] private string rateCsvUrl;     // FishingRates

    public FishingDatabase fishingDb { get; private set; } = new FishingDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; }

    public event Action<FishingDatabase> onLoaded;
    public event Action<string> onFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[FishLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트를 파괴합니다.");
            Destroy(gameObject); // 중요: Destroy(this) 금지
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(LoadAll());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LoadAll()
    {
        IsLoaded = false;
        LastError = "";

        // FishTable load
        string fishCsv = null;
        yield return DownloadCsv(fishCsvUrl, v => fishCsv = v);
        if (!string.IsNullOrEmpty(LastError)) yield break;

        // FishingRates load
        string rateCsv = null;
        yield return DownloadCsv(rateCsvUrl, v => rateCsv = v);
        if (!string.IsNullOrEmpty(LastError)) yield break;

        try
        {
            var fishDefs = ParseFishTableCsv(fishCsv);
            fishingDb.fishDb.Build(fishDefs);

            var rateDefs = ParseFishingRatesCsv(rateCsv);
            fishingDb.rateTable.Build(rateDefs);

            IsLoaded = true;
            Debug.Log($"[FishLoader] Fish = {fishDefs.Count}, Rates = {rateDefs.Count}");
            onLoaded?.Invoke(fishingDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[FishLoader] Parse failed : {e}");
            onFailed?.Invoke(LastError);
        }
    }

    private IEnumerator DownloadCsv(string url, Action<string> onOk)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            LastError = "CSV URL is empty";
            Debug.LogError("[FishLoader] " + LastError);
            onFailed?.Invoke(LastError);
            yield break;
        }

        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[FishLoader] Download failed : {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);
        onOk?.Invoke(csvText);
    }

    /// <summary>
    /// FishTable 파싱
    /// </summary>
    private List<FishDef> ParseFishTableCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<FishDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<FishDef>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < cols.Length; c++)
                row[headers[c].Trim().Trim('\r')] = cols[c].Trim().Trim('\r');

            var def = FishDefFromRow(row);

            if (def.fishId <= 0)
            {
                Debug.LogWarning($"[FishLoader] Skip invalid Fish row. line={i + 1}, raw={lines[i]}");
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    private FishDef FishDefFromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;
        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;

        var def = new FishDef();

        def.fishId = GetInt("FishId");
        def.nameKey = Get("NameKey").Trim();

        def.rarity = ParseRarity(Get("Rarity"));
        def.area = ParseArea(Get("Area"));

        def.biomeMask = GetInt("BiomeMask", 0);
        def.timeWindow = ParseTimeWindow(Get("TimeWindow"));

        def.difficulty = GetInt("Difficulty", 0);
        def.baseWeight = GetInt("BaseWeight", 0);

        return def;
    }

    private List<FishingRateDef> ParseFishingRatesCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<FishingRateDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<FishingRateDef>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < cols.Length; c++)
                row[headers[c].Trim().Trim('\r')] = cols[c].Trim().Trim('\r');

            var def = FishingRateFromRow(row);
            result.Add(def);
        }

        return result;
    }

    private FishingRateDef FishingRateFromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;
        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;

        var def = new FishingRateDef();

        def.area = ParseArea(Get("Area"));
        def.weather = ParseWeather(Get("Weather"));
        def.timeWindow = ParseTimeWindow(Get("TimeWindow"));
        def.rarity = ParseRarity(Get("Rarity"));
        def.rarityWeight = GetInt("RarityWeight", 0);

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

    private FishRarity ParseRarity(string s)
    {
        s = (s ?? "").Trim();
        if (Enum.TryParse<FishRarity>(s, true, out var v)) return v;
        throw new Exception($"Invalid Rarity : {s}");
    }

    private FishArea ParseArea(string s)
    {
        s = (s ?? "").Trim();
        if (Enum.TryParse<FishArea>(s, true, out var v)) return v;
        throw new Exception($"Invalid Area : {s}");
    }

    private WeatherManager.WeatherType ParseWeather(string s)
    {
        s = (s ?? "").Trim();
        if (Enum.TryParse<WeatherManager.WeatherType>(s, true, out var v)) return v;
        throw new Exception($"Invalid Weather : {s}");
    }

    private FishTimeWindow ParseTimeWindow(string s)
    {
        s = (s ?? "").Trim();
        if (Enum.TryParse<FishTimeWindow>(s, true, out var v)) return v;
        throw new Exception($"Invalid TimeWindow : {s}");
    }

    public void Register(Action<FishingDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(fishingDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<FishingDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}