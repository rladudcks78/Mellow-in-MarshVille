using UnityEngine;

public class PlayerHotbarInput : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private InventorySystem inventorySystem;

    private void OnEnable()
    {
        if (inputReader != null)
            inputReader.HotbarEvent += OnHotbar;
    }

    private void OnDisable()
    {
        if (inputReader != null)
            inputReader.HotbarEvent -= OnHotbar;
    }

    private void OnHotbar(int hotbarNumber)
    {
        Debug.Log($"[PlayerHotbarInput] hotbarNumber={hotbarNumber}");
        Debug.Log($"inv={(inventorySystem != null)} ready={(inventorySystem != null && inventorySystem.IsReady)}");

        if (inventorySystem == null || !inventorySystem.IsReady) return;

        int slotIndex = hotbarNumber - 1;
        if (slotIndex < 0 || slotIndex > 9) return;


        inventorySystem.ToggleActiveSlot(slotIndex);
    }
}
