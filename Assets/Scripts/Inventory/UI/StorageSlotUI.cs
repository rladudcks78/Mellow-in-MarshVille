using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StorageSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    private int slotIndex;
    private StorageUI owner;

    public int SlotIndex => slotIndex;

    public void Init(StorageUI owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void Set(Sprite sprite, int amount)
    {
        if(icon != null)
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.OnSlotHoverEnter(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.OnSlotHoverExit(slotIndex);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.SetPointerPos(eventData.position);
    }
}
