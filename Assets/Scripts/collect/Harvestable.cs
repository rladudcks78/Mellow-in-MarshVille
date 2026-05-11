using System;
using UnityEngine;

public class Harvestable : MonoBehaviour
{
    [field: SerializeField] public int RespawnAfterMinutes { get; private set; } = 120;

    [Header("획득 아이템")]
    [SerializeField] private int itemId;
    [SerializeField] private int amount = 1;
    [SerializeField] private string itemName;  // 퀘스트용 이름

    private InventorySystem inventorySystem;
    public event Action<Harvestable> OnHarvested;

    private void Start()
    {
        // 미리 찾아서 캐싱
        inventorySystem = FindFirstObjectByType<InventorySystem>();
    }

    public void Harvest()
    {
        if (inventorySystem != null && inventorySystem.IsReady)
        {
            // 채집 시도
            bool success = inventorySystem.TryPickup(itemId, amount);

            if (success)
            {
                Debug.Log($"[Harvestable] 아이템 획득: itemId={itemId}, amount={amount}");
            }
            else
            {
                Debug.LogWarning("[Harvestable] 인벤토리 공간 부족");
                return; // 인벤토리가 꽉 차면 채집 안됨
            }

            // 퀘스트 진행도 업데이트
            if (QuestManagerCsv.Instance != null && !string.IsNullOrEmpty(itemName))
            {
                //QuestManagerCsv.Instance.UpdateQuestProgress(itemName, amount, QuestGoalType.CollectItems);
            }
        }

        gameObject.SetActive(false); // 채집되면 비활성화
        OnHarvested?.Invoke(this);
    }
}


