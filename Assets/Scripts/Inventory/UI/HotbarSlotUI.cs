using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class HotbarSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    private int inventoryIndex;
    private HotbarUI owner;

    public int InventoryIndex => inventoryIndex;

    public void Init(HotbarUI owner, int inventoryIndex)
    {
        this.owner = owner;
        this.inventoryIndex = inventoryIndex;

        Clear();
    }

    public void Set(Sprite sprite, int amount)
    {
        if (icon != null)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }

        if (amountText != null)
            amountText.text = amount > 0 ? amount.ToString() : "";
    }

    public void Clear()
    {
        if(icon != null)
        {
            icon.enabled = false;
            icon.sprite = null;
        }

        if (amountText != null)
            amountText.text = "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(owner == null) return;

        if (!owner.ClickToSelectEnabled) return;

        if(eventData.button == PointerEventData.InputButton.Left)
        {
            owner.RequestSelect(inventoryIndex);
        }
    }
}
