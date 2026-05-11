using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

public class SaveManagerV1 : MonoBehaviour
{
    public static SaveManagerV1 Instance { get; private set; }

    // =========================
    // Single-slot (Continue) 설정
    // =========================
    public const int ContinueSlotIndex = 0;   // 고정 슬롯
    public const int SlotCount = 1;           // 현재 실제 사용 슬롯 수(고정 1개)

    private const string FolderName = "Saves";

    // 호환성 유지용 (기존 코드 로그/참조 깨지지 않게 남김)
    public int SelectedSlot { get; private set; } = ContinueSlotIndex;
    public bool PendingLoad { get; private set; } = false;

    private JsonSerializerSettings _jsonSettings;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SelectedSlot = ContinueSlotIndex;

        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new StringEnumConverter() }
        };
    }

    // =========================
    // Single-slot 전용 API (앞으로 이 메서드들만 사용)
    // =========================

    /// <summary>
    /// 이어하기 저장 존재 여부 (고정 슬롯 0)
    /// </summary>
    public bool HasContinueSave()
    {
        return File.Exists(GetSlotPath(ContinueSlotIndex));
    }

    /// <summary>
    /// 이어하기 저장 삭제 (필요 시 새 게임 초기화 정책에 사용)
    /// </summary>
    public bool DeleteContinueSave()
    {
        string path = GetSlotPath(ContinueSlotIndex);
        if (!File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// 이어하기 로드 예약 (타이틀 -> InGame 진입 전에 호출)
    /// </summary>
    public void RequestLoadContinue()
    {
        SelectedSlot = ContinueSlotIndex;
        PendingLoad = true;
    }

    /// <summary>
    /// 새 게임 진입 시 혹시 남아있는 로드 예약 상태 제거
    /// </summary>
    public void ClearPendingLoad()
    {
        PendingLoad = false;
    }

    /// <summary>
    /// 이어하기 슬롯 즉시 저장
    /// </summary>
    public bool SaveNowContinue()
    {
        return SaveNowInternal(ContinueSlotIndex);
    }

    /// <summary>
    /// 이어하기 슬롯 즉시 로드
    /// </summary>
    public bool LoadNowContinue(out SaveGameV1 data)
    {
        return LoadNowInternal(ContinueSlotIndex, out data);
    }

    // =========================
    // (선택) 기존 코드 호환용 래퍼
    // - 외부 다른 스크립트가 아직 호출 중이어도 깨지지 않게 유지
    // - 내부적으로는 전부 0번 슬롯만 사용
    // =========================

    public void SelectSlot(int slot)
    {
        // 현재는 0번만 허용
        ValidateSingleSlotOrThrow(slot);
        SelectedSlot = ContinueSlotIndex;
    }

    public void RequestLoadSelectedSlot()
    {
        // SelectedSlot은 항상 0번으로 고정 사용
        SelectedSlot = ContinueSlotIndex;
        PendingLoad = true;
    }

    public void RequestLoad(int slot)
    {
        ValidateSingleSlotOrThrow(slot);
        RequestLoadContinue();
    }

    public bool HasSave(int slot)
    {
        if (slot != ContinueSlotIndex) return false;
        return HasContinueSave();
    }

    public bool DeleteSave(int slot)
    {
        if (slot != ContinueSlotIndex) return false;
        return DeleteContinueSave();
    }

    public bool SaveNowSelectedSlot()
    {
        return SaveNowContinue();
    }

    public bool SaveNow(int slot)
    {
        ValidateSingleSlotOrThrow(slot);
        return SaveNowContinue();
    }

    public bool LoadNowSelectedSlot(out SaveGameV1 data)
    {
        return LoadNowContinue(out data);
    }

    public bool LoadNow(int slot, out SaveGameV1 data)
    {
        ValidateSingleSlotOrThrow(slot);
        return LoadNowContinue(out data);
    }

    // =========================
    // 내부 실제 저장/로드 구현
    // =========================

    private bool SaveNowInternal(int slot)
    {
        ValidateSingleSlotOrThrow(slot);

        try
        {
            var data = SaveGameV1.CaptureFromWorld();

            // ✅ SaveMetaV1가 아니라 SaveGameV1.Meta 사용
            if (data.meta == null)
                data.meta = new SaveGameV1.Meta();

            data.meta.selectedSlot = ContinueSlotIndex;
            data.savedAtUtcIso = DateTime.UtcNow.ToString("o");
            data.meta.savedAtUtc = data.savedAtUtcIso;

            data.Normalize();

            string json = JsonConvert.SerializeObject(data, _jsonSettings);
            string path = GetSlotPath(ContinueSlotIndex);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // 원자적 저장(간이): tmp -> replace
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tmp, path);

            Debug.Log($"[SaveManagerV1] Saved continue slot={ContinueSlotIndex} path={path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManagerV1] Save failed: {e}");
            return false;
        }
    }

    private bool LoadNowInternal(int slot, out SaveGameV1 data)
    {
        data = null;
        ValidateSingleSlotOrThrow(slot);

        string path = GetSlotPath(ContinueSlotIndex);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManagerV1] No continue save file at path={path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            data = JsonConvert.DeserializeObject<SaveGameV1>(json, _jsonSettings);

            if (data == null)
            {
                Debug.LogError("[SaveManagerV1] Deserialize returned null");
                return false;
            }

            data.Normalize();

            if (!data.IsValid(out string reason))
            {
                Debug.LogError($"[SaveManagerV1] Save invalid: {reason}");
                return false;
            }

            // 메타 슬롯값도 현재 정책에 맞게 보정
            if (data.meta != null)
                data.meta.selectedSlot = ContinueSlotIndex;

            Debug.Log($"[SaveManagerV1] Loaded continue slot={ContinueSlotIndex} path={path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManagerV1] Load failed: {e}");
            return false;
        }
    }

    private string GetSlotPath(int slot)
    {
        // 현재는 0번 슬롯만 사용
        return Path.Combine(Application.persistentDataPath, FolderName, $"slot_{ContinueSlotIndex:D2}.json");
    }

    private static void ValidateSingleSlotOrThrow(int slot)
    {
        if (slot != ContinueSlotIndex)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Single-slot mode only supports slot 0.");
    }

    // =========================================================================
    // [임시 보관] 기존 멀티 슬롯 관련 필드/함수 (참고용 주석)
    // 필요하면 나중에 다시 복구해서 멀티 슬롯 확장 가능
    // =========================================================================
    /*
    public const int SlotCount = 3;

    public int SelectedSlot { get; private set; } = 0;
    public bool PendingLoad { get; private set; } = false;

    public void SelectSlot(int slot)
    {
        ValidateSlotOrThrow(slot);
        SelectedSlot = slot;
    }

    public void RequestLoadSelectedSlot() => PendingLoad = true;

    public void RequestLoad(int slot)
    {
        SelectSlot(slot);
        PendingLoad = true;
    }

    public void ClearPendingLoad() => PendingLoad = false;

    public bool HasSave(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        return File.Exists(GetSlotPath(slot));
    }

    public bool DeleteSave(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        string path = GetSlotPath(slot);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool SaveNowSelectedSlot() => SaveNow(SelectedSlot);

    public bool SaveNow(int slot)
    {
        ValidateSlotOrThrow(slot);
        // ...
    }

    public bool LoadNowSelectedSlot(out SaveGameV1 data) => LoadNow(SelectedSlot, out data);

    public bool LoadNow(int slot, out SaveGameV1 data)
    {
        ValidateSlotOrThrow(slot);
        // ...
    }

    private string GetSlotPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, FolderName, $"slot_{slot:D2}.json");
    }

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    private static void ValidateSlotOrThrow(int slot)
    {
        if (!IsValidSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"slot must be 0~{SlotCount - 1}");
    }
    */
}