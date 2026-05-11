using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ItemLoader : MonoBehaviour
{
    public static ItemLoader Instance { get; private set; }

    [Header("구글 스프레드 시트 URL")]
    [SerializeField] private string itemCsvUrl;

    public ItemDatabase itemDb { get; private set; } = new ItemDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; }

    public event Action<ItemDatabase> onLoaded;
    public event Action<string> onFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ItemLoader] 중복 생성 감지. 기존 = {Instance.gameObject.name}, 새로 생성된 오브젝트 = {gameObject.name}. 새로 생성된 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(LoadItemSheet());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LoadItemSheet()
    {
        IsLoaded = false;
        LastError = "";

        using var req = UnityWebRequest.Get(itemCsvUrl);
        yield return req.SendWebRequest();

        if(req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[ItemLoader] 다운로드 실패 : {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        //한글/UTF-8 대비 : downloadHandler.data를 UTF8로 디코딩
        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<ItemDef> defs = ParseItemCsv(csvText);
            itemDb.Build(defs);
            IsLoaded = true;

            Debug.Log($"[ItemLoader] Loaded ItemDefs : {defs.Count}");

            //성공 이벤트 발사
            onLoaded?.Invoke(itemDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[ItemLoader] Parse failed : {e}");
            onFailed?.Invoke(LastError);
        }
    }

    // csv 파싱(헤더 기반)
    private List<ItemDef> ParseItemCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length <= 1) return new List<ItemDef>();

        // 0행 : header
        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<ItemDef>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            // header -> value 딕셔너리 생성
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for(int c = 0; c < headers.Length && c < cols.Length; c++)
            {
                row[headers[c].Trim().Trim('\r')] = cols[c].Trim().Trim('\r');
            }

            // ItemDef 를 row로부터 만들기
            ItemDef def = ItemDefFromRow(row);

            if(def.itemId <= 0)
            {
                Debug.LogWarning($"[ItemLoader] Skip invalid row. line={i + 1}, raw = {lines[i]}");
                continue;
            }
            result.Add(def);
        }
        return result;
    }

    //따옴표 포함한 최소 CSV 라인 분리기 (쉼표가 텍스트에 들어갈 때 대비)
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

    // row -> ItemDef 변환
    private ItemDef ItemDefFromRow(Dictionary<string, string> row)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;

        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;

        float GetFloat(string key, float fallback = 0f) => float.TryParse(Get(key), out var v) ? v : fallback;

        bool GetBool(string key, bool fallback = false)
        {
            var s = Get(key, fallback ? "TRUE" : "FALSE");

            if (bool.TryParse(s, out var v)) return v;
            return fallback;
        }

        var def = new ItemDef();

        def.itemId = GetInt("itemId");
        def.name = Get("name");
        def.description = Get("description");
        def.maxStack = GetInt("maxStack");
        def.spritePath = Get("spritePath").Trim().Trim('"');

        def.buyPrice = GetInt("buyPrice");
        def.sellPrice = GetInt("sellPrice");

        def.isSellable = GetBool("isSellable");
        def.isCookingredient = GetBool("isCookIngredient");
        def.isQuestOnly = GetBool("isQuestOnly");
        def.isUsable = GetBool("isUsable");
        def.isGiftable = GetBool("isGiftable");
        def.isSeed = GetBool("isSeed");

        def.tier = GetInt("tier");
        
        def.toolType = Get("toolType").Trim();
        def.areaW_S = GetInt("areaW_S");
        def.areaH_S = GetInt("areaH_S");
        def.areaW_M = GetInt("areaW_M");
        def.areaH_M = GetInt("areaH_M");
        def.areaW_L = GetInt("areaW_L");
        def.areaH_L = GetInt("areaH_L");

        def.weaponType = Get("weaponType").Trim();
        def.attackDamage = GetFloat("attackDamage");
        def.attackSpeed = GetFloat("attackSpeed");

        return def;
    }
  
    public void Register(Action<ItemDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        // 이미 로드됐으면 바로 호출(이벤트 놓침 방지)
        if(IsLoaded && OnLoaded != null) 
            OnLoaded(itemDb);

        // 이미 실패 상태면 실패도 즉시 전달
        if(!string.IsNullOrEmpty(LastError) && OnFailed != null) OnFailed(LastError);
    }

    public void Unregister(Action<ItemDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}
