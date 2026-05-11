using UnityEngine;

public class SaveWorldRootV1 : MonoBehaviour
{
    public static SaveWorldRootV1 Instance { get; private set; }

    [Header("Core")]
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private WeatherManager weatherManager;

    [Header("Providers")]
    [SerializeField] private InventorySaveProviderV1 inventoryProvider;
    [SerializeField] private StorageSaveProviderV1 storageProvider;
    [SerializeField] private FarmSaveProviderV1 farmProvider;
    [SerializeField] private QuestSaveProviderV1 questProvider;

    private IInventorySaveProviderV1 Inv => inventoryProvider as IInventorySaveProviderV1;
    private IStorageSaveProviderV1 Sto => storageProvider as IStorageSaveProviderV1;
    private IFarmSaveProviderV1 Farm => farmProvider as IFarmSaveProviderV1;
    private IQuestSaveProviderV1 Quests => questProvider as IQuestSaveProviderV1;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public SaveGameV1 CaptureToSave(string note = null)
    {
        var s = SaveGameV1.CreateEmpty(note);

        //meta
        s.meta.version = SaveGameV1.SchemaVersion;
        s.meta.savedAtUtc = s.savedAtUtcIso;
        s.meta.selectedSlot = SaveManagerV1.Instance != null ? SaveManagerV1.Instance.SelectedSlot : 0;


        //core
        if (timeManager == null) timeManager = TimeManager.Instance;
        if (weatherManager == null) weatherManager = WeatherManager.Instance;

        s.time = timeManager != null ? timeManager.CaptureSave() : new TimeSaveData();
        s.weather = weatherManager != null ? weatherManager.CaptureSave() : new WeatherSaveData();

        //providers
        s.inventory = Inv != null ? Inv.CaptureInventory() : InventorySaveData.CreateDefault();
        s.storage = Sto != null ? Sto.CaptureStorage() : StorageSaveData.CreateDefault();
        s.farm = Farm != null ? Farm.CaptureFarm() : new FarmSaveData();
        s.quests = Quests != null ? Quests.CaptureQuest() : new QuestSaveDataV1();

        s.Normalize();
        return s;
    }

    public void ApplyFromSave(SaveGameV1 s)
    {
        if (s == null) return;
        s.Normalize();

        if (timeManager == null) timeManager = TimeManager.Instance;
        if (weatherManager == null) weatherManager = WeatherManager.Instance;

        //적용 순서 : time -> weather -> inventory/storage -> farm -> quest
        if (timeManager != null) timeManager.ApplySave(s.time);
        if(weatherManager != null) weatherManager.ApplySave(s.weather);

        if (Inv != null) Inv.ApplyInventory(s.inventory);
        if (Sto != null) Sto.ApplyStorage(s.storage);
        if (Farm != null) Farm.ApplyFarm(s.farm);
        if (Quests != null) Quests.ApplyQuest(s.quests);

        //들고있는 아이템 로드시 비워두기
        if (SharedHeldSystem.Instance != null) SharedHeldSystem.Instance.Clear();
    }
}

