public interface IInventorySaveProviderV1
{
    InventorySaveData CaptureInventory();
    void ApplyInventory(InventorySaveData data);
}

public interface IStorageSaveProviderV1
{
    StorageSaveData CaptureStorage();
    void ApplyStorage(StorageSaveData data);
}

public interface IFarmSaveProviderV1
{
    FarmSaveData CaptureFarm();
    void ApplyFarm(FarmSaveData data);
}

public interface IQuestSaveProviderV1
{
    QuestSaveDataV1 CaptureQuest();
    void ApplyQuest(QuestSaveDataV1 data);
}
