using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점 목록 슬롯 UI
/// - 클릭 처리 없음 (UIInteract에서 처리)
/// - hover / pointer move만 담당
/// </summary>
public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text priceText;

    [Header("Optional Visual")]
    [SerializeField] private Image background;
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color selectedBgColor = new Color(0.85f, 0.95f, 1f, 1f);

    private ShopUIController owner;
    private int slotIndex;

    public int SlotIndex => slotIndex;

    public void Init(ShopUIController owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
        SetSelected(false);
    }

    public void Bind(ShopService.ShopDisplayItem data, Sprite sprite)
    {
        if (icon != null)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }

        if (nameText != null)
            nameText.text = data != null ? (data.itemName ?? "") : "";

        if (descText != null)
            descText.text = data != null ? (data.description ?? "") : "";

        if (priceText != null)
            priceText.text = data != null ? data.unitPrice.ToString() : "";
    }

    public void Clear()
    {
        if (icon != null)
        {
            icon.enabled = false;
            icon.sprite = null;
        }

        if (nameText != null) nameText.text = "";
        if (descText != null) descText.text = "";
        if (priceText != null) priceText.text = "";
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedBgColor : normalBgColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.OnShopSlotHoverEnter(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.OnShopSlotHoverExit(slotIndex);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData != null)
            owner?.SetPointerPos(eventData.position);
    }
}