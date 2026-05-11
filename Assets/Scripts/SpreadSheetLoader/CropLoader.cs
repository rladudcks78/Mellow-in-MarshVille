using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CropLoader : MonoBehaviour
{
    public static CropLoader Instance { get; private set; }

    [Header("설정")]
    [SerializeField] private string _csvUrl; // 구글 시트 URL
    [SerializeField] private SpriteResolver _spriteResolver;

    public CropDatabase CropDb { get; private set; } = new CropDatabase();

    public bool IsLoaded { get; private set; }
    public string LastError { get; private set; }

    public event Action<CropDatabase> onLoaded;
    public event Action<string> onFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[CropLoader] 중복 생성 감지. 기존={Instance.gameObject.name}, 신규={gameObject.name}. 신규 오브젝트를 파괴합니다.");
            Destroy(gameObject); // 중요
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(LoadProcess());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator LoadProcess()
    {
        IsLoaded = false;
        LastError = "";

        if (string.IsNullOrWhiteSpace(_csvUrl))
        {
            LastError = "CSV URL is empty";
            Debug.LogError("[CropLoader] " + LastError);
            onFailed?.Invoke(LastError);
            yield break;
        }

        using var req = UnityWebRequest.Get(_csvUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastError = req.error;
            Debug.LogError($"[CropLoader] CSV 다운로드 실패: {LastError}");
            onFailed?.Invoke(LastError);
            yield break;
        }

        string csvText = Encoding.UTF8.GetString(req.downloadHandler.data);

        try
        {
            List<CropDef> cropList = ParseCsv(csvText);

            CropDb.Build(cropList);

            IsLoaded = true;
            Debug.Log($"[CropLoader] Loaded CropDefs : {cropList.Count}");

            onLoaded?.Invoke(CropDb);
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Debug.LogError($"[CropLoader] 데이터 처리 중 오류 발생: {e}");
            onFailed?.Invoke(LastError);
        }
    }

    private List<CropDef> ParseCsv(string csv)
    {
        var list = new List<CropDef>();
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        if (lines.Length <= 1) return list;

        string[] headers = SplitCsvLine(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = SplitCsvLine(lines[i].TrimEnd('\r'));

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int h = 0; h < headers.Length && h < cols.Length; h++)
            {
                row[headers[h].Trim().Trim('\r')] = cols[h].Trim().Trim('\r');
            }

            CropDef crop = CreateCropFromRow(row);
            if (crop != null) list.Add(crop);
        }

        return list;
    }

    private CropDef CreateCropFromRow(Dictionary<string, string> row)
    {
        int GetInt(string key) => int.TryParse(row.ContainsKey(key) ? row[key] : "0", out int v) ? v : 0;

        int cropId = GetInt("CropID");
        if (cropId == 0) return null;

        var crop = new CropDef
        {
            cropId = cropId,
            seedItemId = GetInt("SeedItemID"),
            harvestItemId = GetInt("HarvestItemID"),
            growDays = GetInt("GrowDays"),
            harvestMin = GetInt("HarvestMin"),
            harvestMax = GetInt("HarvestMax")
        };

        if (_spriteResolver != null)
        {
            string path = $"Sprites/Crops/{cropId}";
            crop.growthSprites = _spriteResolver.LoadAllSpritesAtFolder(path);

            if (crop.growthSprites == null || crop.growthSprites.Length == 0)
                Debug.LogWarning($"[CropLoader] CropID {cropId}의 이미지를 찾을 수 없습니다. 경로: {path}");
        }

        return crop;
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

    public void Register(Action<CropDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded += OnLoaded;
        if (OnFailed != null) onFailed += OnFailed;

        if (IsLoaded && OnLoaded != null)
            OnLoaded(CropDb);

        if (!string.IsNullOrEmpty(LastError) && OnFailed != null)
            OnFailed(LastError);
    }

    public void Unregister(Action<CropDatabase> OnLoaded, Action<string> OnFailed = null)
    {
        if (OnLoaded != null) onLoaded -= OnLoaded;
        if (OnFailed != null) onFailed -= OnFailed;
    }
}