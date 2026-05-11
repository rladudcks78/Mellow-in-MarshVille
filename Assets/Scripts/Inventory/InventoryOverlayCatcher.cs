using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryOverlayCatcher : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private InventoryUI inventoryUI;

    public void OnPointerClick(PointerEventData eventData)
    {
        inventoryUI?.OnClickOutside();
    }
}
