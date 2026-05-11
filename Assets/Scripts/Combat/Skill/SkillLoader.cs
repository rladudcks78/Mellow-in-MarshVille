using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SkillLoader : MonoBehaviour
{
    public static SkillLoader Instance { get; private set; }

    [Header("구글 스프레드 시트 URL")]
    [SerializeField] private string skillCsvUrl;

    public SkillDatabase skillDb { get; private set; } = new SkillDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; } = "";

    public event Action<SkillDatabase> onLoaded;
    public event Action<string> onFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[SkillLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트 파괴");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(LoadSkillSheet());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LoadSkillSheet()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(skillCsvUrl))
        {
            LastError = "skillCsvUrl is empty.";
            Debug.LogError($"[SkillLoader] {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        using var req = UnityWebRequest.Get(skillCsvUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[SkillLoader] 다운로드 실패 : {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<SkillDef> defs = ParseSkillCsv(csvText);
            skillDb.Build(defs);
            IsLoaded = true;

            Debug.Log($"[SkillLoader] Loaded SkillDefs : {defs.Count}");
            onLoaded?.Invoke(skillDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[SkillLoader] Parse failed : {e}");
            onFailed?.Invoke(LastError);
        }
    }

    private List<SkillDef> ParseSkillCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<SkillDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<SkillDef>(lines.Length - 1);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length && c < cols.Length; c++)
                row[headers[c].Trim().Trim('\r')] = cols[c].Trim().Trim('\r');

            SkillDef def = SkillDefFromRow(row);

            if (string.IsNullOrWhiteSpace(def.weaponType) || def.level <= 0)
            {
                Debug.LogWarning($"[SkillLoader] Skil invalid row. line = {i + 1}, raw = {lines[i]}");
                continue;
            }

            result.Add(def);
        }
        return result;
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
            else sb.Append(ch);
        }

        list.Add(sb.ToString());
        return list.ToArray();
    }

    private SkillDef SkillDefFromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;
        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;
        float GetFloat(string key, float fallback = 0f) => float.TryParse(Get(key), out var v) ? v : fallback;

        var def = new SkillDef();

        def.weaponType = Get("weaponType").Trim();
        def.level = GetInt("level");
        def.skillName = Get("skillName").Trim();

        def.damageMultiplier = GetFloat("damageMultiplier", 3f);
        def.cooldownSec = GetFloat("cooldownSec", 2f);

        def.castLockSec = GetFloat("castLockSec", 0.35f);
        def.hitDelaySec = GetFloat("hitDelaySec", 0.1f);

        def.overrideOffset = GetFloat("overrideOffset", 0f);
        def.overrideBoxH = GetFloat("overrideBoxH", 0f);
        def.overrideBoxW = GetFloat("overrideBoxW", 0f);

        return def;
    }

    public void Register(Action<SkillDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(skillDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<SkillDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}