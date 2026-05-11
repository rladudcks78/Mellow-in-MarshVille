using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FoodLoader : MonoBehaviour
{
    public static FoodLoader Instance { get; private set; }

    [Header("Food CSV URL")]
    [SerializeField] private string foodCsvUrl;

    public FoodDatabase foodDb { get; private set; } = new FoodDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; }

    public event Action<FoodDatabase> onLoaded;
    public event Action<string> onFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[FoodLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트를 파괴합니다.");
            Destroy(gameObject); // 중요
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(LoadFoodSheet());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LoadFoodSheet()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(foodCsvUrl))
        {
            LastError = "CSV URL is empty";
            Debug.LogError("[FoodLoader] " + LastError);
            onFailed?.Invoke(LastError);
            yield break;
        }

        using var req = UnityWebRequest.Get(foodCsvUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[FoodLoader] 다운로드 실패: {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<FoodDef> defs = ParseFoodCsv(csvText);
            foodDb.Build(defs);

            IsLoaded = true;
            Debug.Log($"[FoodLoader] Loaded FoodDefs : {defs.Count}");

            onLoaded?.Invoke(foodDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[FoodLoader] Parse failed : {e}");
            onFailed?.Invoke(LastError);
        }
    }

    private List<FoodDef> ParseFoodCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var result = new List<FoodDef>();

        if (lines.Length <= 1) return result;

        string[] headers = SplitCsvLine(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < cols.Length; c++)
                row[headers[c].Trim().Trim('\r')] = cols[c].Trim().Trim('\r');

            var def = FoodDefFromRow(row);

            if (def.itemId <= 0)
            {
                Debug.LogWarning($"[FoodLoader] Skip invalid row. line={i + 1}, raw={lines[i]}");
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    private FoodDef FoodDefFromRow(Dictionary<string, string> row)
    {
        float GetFloat(string key) => float.TryParse(row.ContainsKey(key) ? row[key] : "0", out float v) ? v : 0f;
        int GetInt(string key) => int.TryParse(row.ContainsKey(key) ? row[key] : "0", out int v) ? v : 0;

        return new FoodDef
        {
            itemId = GetInt("itemId"),
            usingTime = GetFloat("UsingTime"),
            healHP = GetFloat("HealHP"),
            healDuration = GetFloat("HealDuration"),
            buffDuration = GetFloat("BuffDuration"),
            addMaxHP = GetFloat("AddMaxHP"),
            addAttackDamage = GetFloat("AddAttackDamage"),
            addAttackSpeed = GetFloat("AddAttackSpeed"),
            addPlayerSpeed = GetFloat("AddPlayerSpeed"),
            resistCold = GetFloat("ResistCold"),
            resistHeat = GetFloat("ResistHeat")
        };
    }

    private string[] SplitCsvLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '\"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }

        list.Add(sb.ToString());
        return list.ToArray();
    }

    public void Register(Action<FoodDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(foodDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<FoodDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}