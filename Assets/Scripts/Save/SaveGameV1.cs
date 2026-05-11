// SaveGameV1.cs  (세이브 스키마/데이터만)
// Newtonsoft.Json 기반, enum은 문자열 저장
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

[Serializable]
public sealed class SaveGameV1
{
    public const int SchemaVersion = 1;

    [Header("Meta")]
    public int version = SchemaVersion;
    public string savedAtUtcIso;
    public string note;

    public Meta meta = new Meta();

    [Header("Systems")]
    public TimeSaveData time = new TimeSaveData();
    public WeatherSaveData weather = new WeatherSaveData();
    public InventorySaveData inventory = new InventorySaveData();
    public StorageSaveData storage = new StorageSaveData();
    public FarmSaveData farm = new FarmSaveData();
    public QuestSaveDataV1 quests = new QuestSaveDataV1();

    [Serializable]
    public sealed class Meta
    {
        public int version = SchemaVersion;
        public string savedAtUtc;
        public int selectedSlot;
    }

    public static SaveGameV1 CaptureFromWorld(string note = null)
    {
        if (SaveWorldRootV1.Instance == null)
        {
            var fallback = CreateEmpty(note);
            fallback.Normalize();
            return fallback;
        }

        return SaveWorldRootV1.Instance.CaptureToSave(note);
    }

    public void ApplyToWorld()
    {
        if (SaveWorldRootV1.Instance == null) return;
        SaveWorldRootV1.Instance.ApplyFromSave(this);
    }

    public static SaveGameV1 CreateEmpty(string note = null)
    {
        var now = DateTime.UtcNow.ToString("o");
        return new SaveGameV1
        {
            version = SchemaVersion,
            savedAtUtcIso = now,
            note = note,
            meta = new Meta { version = SchemaVersion, savedAtUtc = now, selectedSlot = 0 },

            time = new TimeSaveData(),
            weather = new WeatherSaveData(),
            inventory = InventorySaveData.CreateDefault(),
            storage = StorageSaveData.CreateDefault(),
            farm = new FarmSaveData(),
            quests = new QuestSaveDataV1()
        };
    }

    public void Normalize()
    {
        if (version <= 0) version = SchemaVersion;
        if (string.IsNullOrEmpty(savedAtUtcIso)) savedAtUtcIso = DateTime.UtcNow.ToString("o");

        meta ??= new Meta();
        if (meta.version <= 0) meta.version = SchemaVersion;
        if (string.IsNullOrEmpty(meta.savedAtUtc)) meta.savedAtUtc = savedAtUtcIso;

        time ??= new TimeSaveData();
        weather ??= new WeatherSaveData();
        inventory ??= InventorySaveData.CreateDefault();
        storage ??= StorageSaveData.CreateDefault();
        farm ??= new FarmSaveData();
        quests ??= new QuestSaveDataV1();

        inventory.Normalize();
        storage.Normalize();
        farm.Normalize();
        quests.Normalize();
    }

    public bool IsValid(out string reason)
    {
        reason = "";

        if (version != SchemaVersion)
        {
            reason = $"지원하지 않는 세이브 버전 : {version} (지원 : {SchemaVersion})";
            return false;
        }

        if (inventory == null || storage == null || time == null || farm == null || weather == null || quests == null)
        {
            reason = "세이브 구조가 비정상(null 섹션 존재)";
            return false;
        }

        if (inventory.slotCount <= 0 || inventory.slots == null)
        {
            reason = "Inventory 데이터 이상";
            return false;
        }

        if (storage.capacity <= 0 || storage.slots == null)
        {
            reason = "Storage 데이터 이상";
            return false;
        }

        return true;
    }
}

[Serializable]
public sealed class TimeSaveData
{
    public int currentDay = 1;
    public float currentGameTimeHours = 6f;
    public bool wasPassOutLastNight = false;
    public bool isTimeStopped = false;
}

[Serializable]
public sealed class WeatherSaveData
{
    [JsonConverter(typeof(StringEnumConverter))]
    public WeatherTypeV1 currentWeather = WeatherTypeV1.Sunny;

    public bool isIndoor = false;
    public float rainChance = -1f;

    public enum WeatherTypeV1 { Sunny, Rainy }
}

[Serializable]
public sealed class InventorySaveData
{
    public int cols = 10;
    public int rows = 4;
    public int slotCount = 40;
    public List<ItemStackSave> slots = new List<ItemStackSave>(40);

    public static InventorySaveData CreateDefault()
    {
        var d = new InventorySaveData
        {
            cols = 10,
            rows = 4,
            slotCount = 40,
            slots = new List<ItemStackSave>(40)
        };
        for (int i = 0; i < d.slotCount; i++) d.slots.Add(ItemStackSave.Empty);
        return d;
    }

    public void Normalize()
    {
        if (cols <= 0) cols = 10;
        if (rows <= 0) rows = 4;

        int expected = Mathf.Max(1, cols * rows);
        slotCount = expected;

        slots ??= new List<ItemStackSave>(expected);

        if (slots.Count < expected)
        {
            int add = expected - slots.Count;
            for (int i = 0; i < add; i++) slots.Add(ItemStackSave.Empty);
        }
        else if (slots.Count > expected)
        {
            slots.RemoveRange(expected, slots.Count - expected);
        }

        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null) slots[i] = ItemStackSave.Empty;
    }
}

[Serializable]
public sealed class StorageSaveData
{
    public int upgradeCount = 0;
    public int capacity = 20;
    public List<ItemStackSave> slots = new List<ItemStackSave>(20);

    public static StorageSaveData CreateDefault()
    {
        var d = new StorageSaveData
        {
            upgradeCount = 0,
            capacity = 20,
            slots = new List<ItemStackSave>(20)
        };
        for (int i = 0; i < d.capacity; i++) d.slots.Add(ItemStackSave.Empty);
        return d;
    }

    public void Normalize()
    {
        if (capacity <= 0) capacity = 20;
        if (upgradeCount < 0) upgradeCount = 0;

        slots ??= new List<ItemStackSave>(capacity);

        if (slots.Count < capacity)
        {
            int add = capacity - slots.Count;
            for (int i = 0; i < add; i++) slots.Add(ItemStackSave.Empty);
        }
        else if (slots.Count > capacity)
        {
            slots.RemoveRange(capacity, slots.Count - capacity);
        }

        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null) slots[i] = ItemStackSave.Empty;
    }
}

[Serializable]
public sealed class FarmSaveData
{
    public List<TileSaveData> tiles = new List<TileSaveData>();

    public void Normalize()
    {
        tiles ??= new List<TileSaveData>();
        for (int i = tiles.Count - 1; i >= 0; i--)
            if (tiles[i] == null) tiles.RemoveAt(i);
    }
}

[Serializable]
public sealed class TileSaveData
{
    public int x;
    public int y;

    [JsonConverter(typeof(StringEnumConverter))]
    public TileStateV1 state = TileStateV1.Dirted;

    public int currentCropId;
    public bool isWatered;
    public int progressDay;
    public bool isDry;

    public enum TileStateV1 { Dirted, Tilled, Seeded, Dried, Water }
}

[Serializable]
public sealed class QuestSaveDataV1
{
    public List<int> claimedQuestIds = new List<int>();
    public List<QuestStateEntryV1> activeStates = new List<QuestStateEntryV1>();

    public void Normalize()
    {
        claimedQuestIds ??= new List<int>();
        activeStates ??= new List<QuestStateEntryV1>();

        for (int i = activeStates.Count - 1; i >= 0; i--)
        {
            var e = activeStates[i];
            if (e == null || e.questId <= 0)
            {
                activeStates.RemoveAt(i);
                continue;
            }
            if (e.currentAmount < 0) e.currentAmount = 0;
        }
    }
}

[Serializable]
public sealed class QuestStateEntryV1
{
    public int questId;
    public int currentAmount;
    public bool objectiveCompleted;
    public bool rewardClaimed;
}

[Serializable]
public sealed class ItemStackSave
{
    public int itemId;
    public int amount;
    public int quality;

    public static ItemStackSave Empty => new ItemStackSave(0, 0, 0);

    public ItemStackSave() { }
    public ItemStackSave(int itemId, int amount, int quality = 0)
    {
        this.itemId = itemId;
        this.amount = amount;
        this.quality = quality;
    }

    public bool IsEmpty => itemId <= 0 || amount <= 0;

    public static ItemStackSave FromRuntime(ItemStack runtime)
    {
        if (runtime.IsEmpty) return Empty;
        return new ItemStackSave(runtime.itemId, runtime.amount, runtime.quality);
    }

    public ItemStack ToRuntime()
    {
        if (IsEmpty) return ItemStack.empty;
        return new ItemStack(itemId, amount, quality);
    }
}
