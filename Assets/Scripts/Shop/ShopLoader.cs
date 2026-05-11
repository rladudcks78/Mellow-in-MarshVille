using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ShopLoader : MonoBehaviour
{
    public static ShopLoader Instance { get; private set; }

    [Header("구글 스프레드시트 CSV URL")]
    [SerializeField] private string shopCsvUrl;       // Shop 탭
    [SerializeField] private string shopItemsCsvUrl;  // ShopItems 탭

    [Header("로드 설정")]
    [SerializeField] private bool loadOnStart = true;

    public ShopDatabase ShopDb { get; private set; } = new ShopDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; }

    public event Action<ShopDatabase> onLoaded;
    public event Action<string> onFailed;

    private Coroutine loadRoutine;

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Equipment", "Weapon", "Fishing", "Food", "Ingredient"
    };

    private static readonly HashSet<string> AllowedUnlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "None", "Day", "Quest"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ShopLoader] 중복 생성 감지. 기존 = {Instance.gameObject.name}, 새 = {gameObject.name}. 새 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (loadOnStart)
            Reload();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Reload()
    {
        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        loadRoutine = StartCoroutine(CoLoadAll());
    }

    private IEnumerator CoLoadAll()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(shopCsvUrl))
        {
            Fail("shopCsvUrl is empty");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(shopItemsCsvUrl))
        {
            Fail("shopItemsCsvUrl is empty");
            yield break;
        }

        string shopCsv = null;
        string shopItemsCsv = null;

        yield return StartCoroutine(CoDownloadCsv(shopCsvUrl, text => shopCsv = text, err => LastError = $"Shop CSV download failed: {err}"));
        if (!string.IsNullOrEmpty(LastError))
        {
            Fail(LastError);
            yield break;
        }

        yield return StartCoroutine(CoDownloadCsv(shopItemsCsvUrl, text => shopItemsCsv = text, err => LastError = $"ShopItems CSV download failed: {err}"));
        if (!string.IsNullOrEmpty(LastError))
        {
            Fail(LastError);
            yield break;
        }

        try
        {
            List<ShopDef> shops = ParseShopCsv(shopCsv);
            List<ShopItemEntryDef> items = ParseShopItemsCsv(shopItemsCsv);

            ShopDb.Build(shops, items);

            IsLoaded = true;
            LastError = "";

            Debug.Log($"[ShopLoader] Loaded Shops={shops.Count}, ShopItems={items.Count}");
            onLoaded?.Invoke(ShopDb);
        }
        catch (Exception e)
        {
            Fail($"Parse/Build failed: {e.Message}\n{e}");
        }
        finally
        {
            loadRoutine = null;
        }
    }

    private void Fail(string error)
    {
        IsLoaded = false;
        LastError = error ?? "Unknown error";
        Debug.LogError($"[ShopLoader] {LastError}");
        onFailed?.Invoke(LastError);
    }

    private IEnumerator CoDownloadCsv(string url, Action<string> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);
        onSuccess?.Invoke(csvText);
    }

    // =========================================================
    // Parse: Shop
    // =========================================================

    private List<ShopDef> ParseShopCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<ShopDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<ShopDef>(Mathf.Max(0, lines.Length - 1));

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));
            var row = BuildRow(headers, cols);

            var def = ShopDefFromRow(row, i + 1, lines[i]);
            if (def == null) continue;

            result.Add(def);
        }

        return result;
    }

    private ShopDef ShopDefFromRow(Dictionary<string, string> row, int lineNo, string raw)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;
        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;

        var def = new ShopDef
        {
            shopId = GetInt("shopId"),
            shopName = Get("shopName"),
            npcId = GetInt("npcId"),
            openMin = Mathf.Clamp(GetInt("openMin"), 0, 1439),
            closeMin = Mathf.Clamp(GetInt("closeMin"), 0, 1439),
            weekdayMask = GetInt("weekdayMask"),
            refreshType = Get("refreshType").Trim()
        };

        if (def.shopId <= 0)
        {
            Debug.LogWarning($"[ShopLoader] Skip Shop row (invalid shopId). line={lineNo}, raw={raw}");
            return null;
        }

        if (def.npcId <= 0)
        {
            Debug.LogWarning($"[ShopLoader] Shop row has invalid npcId. line={lineNo}, shopId={def.shopId}");
        }

        return def;
    }

    // =========================================================
    // Parse: ShopItems
    // =========================================================

    private List<ShopItemEntryDef> ParseShopItemsCsv(string csv)
    {
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length <= 1) return new List<ShopItemEntryDef>();

        string[] headers = SplitCsvLine(lines[0]);

        var result = new List<ShopItemEntryDef>(Mathf.Max(0, lines.Length - 1));

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));
            var row = BuildRow(headers, cols);

            var def = ShopItemEntryFromRow(row, i + 1, lines[i]);
            if (def == null) continue;

            result.Add(def);
        }

        return result;
    }

    private ShopItemEntryDef ShopItemEntryFromRow(Dictionary<string, string> row, int lineNo, string raw)
    {
        string Get(string key, string fallback = "") => row.TryGetValue(key, out var v) ? v : fallback;
        int GetInt(string key, int fallback = 0) => int.TryParse(Get(key), out var v) ? v : fallback;

        string NormalizeUnlockType(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "None";
            s = s.Trim();

            if (!AllowedUnlockTypes.Contains(s))
            {
                Debug.LogWarning($"[ShopLoader] Unknown unlockType '{s}' at line={lineNo}. Fallback to None.");
                return "None";
            }

            if (s.Equals("none", StringComparison.OrdinalIgnoreCase)) return "None";
            if (s.Equals("day", StringComparison.OrdinalIgnoreCase)) return "Day";
            if (s.Equals("quest", StringComparison.OrdinalIgnoreCase)) return "Quest";
            return s;
        }

        string NormalizeCategory(string s)
        {
            s = (s ?? "").Trim();

            if (string.IsNullOrEmpty(s))
            {
                Debug.LogWarning($"[ShopLoader] Empty category at line={lineNo}, itemId={GetInt("itemId")}");
                return "";
            }

            if (!AllowedCategories.Contains(s))
            {
                Debug.LogWarning($"[ShopLoader] Unknown category '{s}' at line={lineNo}. Allowed: Equipment/Weapon/Fishing/Food/Ingredient");
                return s; // 그대로 두되 경고
            }

            // canonical
            if (s.Equals("equipment", StringComparison.OrdinalIgnoreCase)) return "Equipment";
            if (s.Equals("weapon", StringComparison.OrdinalIgnoreCase)) return "Weapon";
            if (s.Equals("fishing", StringComparison.OrdinalIgnoreCase)) return "Fishing";
            if (s.Equals("food", StringComparison.OrdinalIgnoreCase)) return "Food";
            if (s.Equals("ingredient", StringComparison.OrdinalIgnoreCase)) return "Ingredient";
            return s;
        }

        string NormalizeStockType(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Infinite";
            s = s.Trim();

            if (s.Equals("infinite", StringComparison.OrdinalIgnoreCase)) return "Infinite";
            if (s.Equals("limited", StringComparison.OrdinalIgnoreCase)) return "Limited";

            Debug.LogWarning($"[ShopLoader] Unknown stockType '{s}' at line={lineNo}. Fallback to Infinite.");
            return "Infinite";
        }

        int shopId = GetInt("shopId");
        int itemId = GetInt("itemId");

        // 임시로 unlockType만 적어놓은 행 같은 것 방어
        if (shopId <= 0 || itemId <= 0)
        {
            Debug.LogWarning($"[ShopLoader] Skip ShopItems row (invalid key). line={lineNo}, shopId={shopId}, itemId={itemId}, raw={raw}");
            return null;
        }

        int stackMin = GetInt("stackMin", 1);
        if (stackMin <= 0) stackMin = 1;

        int stackMax = GetInt("stackMax", stackMin);
        if (stackMax < stackMin)
        {
            Debug.LogWarning($"[ShopLoader] stackMax < stackMin at line={lineNo}, itemId={itemId}. Adjust stackMax={stackMin}");
            stackMax = stackMin;
        }

        var def = new ShopItemEntryDef
        {
            shopId = shopId,
            itemId = itemId,
            stockType = NormalizeStockType(Get("stockType")),
            stock = Mathf.Max(0, GetInt("stock", 0)),
            restock = Mathf.Max(0, GetInt("restock", 0)),
            stackMin = stackMin,
            stackMax = stackMax,
            category = NormalizeCategory(Get("category")),
            sortOrder = GetInt("sortOrder", 0),
            unlockType = NormalizeUnlockType(Get("unlockType")),
            unlockParam = GetInt("unlockParam", 0)
        };

        return def;
    }

    // =========================================================
    // CSV helpers
    // =========================================================

    private Dictionary<string, string> BuildRow(string[] headers, string[] cols)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int c = 0; c < headers.Length && c < cols.Length; c++)
        {
            string key = (headers[c] ?? "").Trim().Trim('\r');
            string val = (cols[c] ?? "").Trim().Trim('\r');

            if (string.IsNullOrEmpty(key)) continue;
            row[key] = val;
        }

        return row;
    }

    // 따옴표 포함 최소 CSV 분리기
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

    // =========================================================
    // Register / Unregister (기존 Loader 패턴 유지)
    // =========================================================

    public void Register(Action<ShopDatabase> onLoadedHandler, Action<string> onFailedHandler = null)
    {
        if (onLoadedHandler != null) onLoaded += onLoadedHandler;
        if (onFailedHandler != null) onFailed += onFailedHandler;

        if (IsLoaded && onLoadedHandler != null)
            onLoadedHandler(ShopDb);

        if (!string.IsNullOrEmpty(LastError) && onFailedHandler != null)
            onFailedHandler(LastError);
    }

    public void Unregister(Action<ShopDatabase> onLoadedHandler, Action<string> onFailedHandler = null)
    {
        if (onLoadedHandler != null) onLoaded -= onLoadedHandler;
        if (onFailedHandler != null) onFailed -= onFailedHandler;
    }
}