using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Refs")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    [Header("Visual")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color blockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private InventoryUI owner;
    private int index;

    private bool blocked; // 슬롯 차단 상태(이 상태에서는 클릭 무시)

    public bool IsBlocked => blocked;

    public void Init(InventoryUI owner, int index)
    {
        this.owner = owner;
        this.index = index;
        blocked = false;

        if (icon != null) icon.enabled = true;
        Clear();
        SetBlocked(false);
    }

    public void Set(Sprite sprite, int amount)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.color = normalColor;

        }

        if (amountText != null)
        {
            amountText.text = (amount > 0) ? amount.ToString() : "";
        }
    }

    public void Clear()
    {
        if (icon != null)
        {
            icon.sprite = null;
            icon.color = new Color(1f, 1f, 1f, 0f);
        }

        if (amountText != null)
        {
            amountText.text = "";
        }
    }

    public void SetBlocked(bool value)
    {
        blocked = value;

        // 회색 처리
        if (icon.sprite != null) icon.color = blocked ? blockedColor : normalColor;
        if (amountText != null) amountText.color = blocked ? blockedColor : normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData) => owner?.OnSlotHoverEnter(index);
    public void OnPointerExit(PointerEventData eventData) => owner?.OnSlotHoverExit(index);

    // 클릭은 Gift 모드에서만 동작(다른 모드/다른 입력 시스템과 충돌 방지)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (blocked) return;
        if (owner == null) return;
        if (eventData == null) return;

        // 우클릭은 무시(요청사항)
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Gift 모드에서만 InventoryUI로 클릭 전달
        if (owner.CurrentLayout != InventoryUI.LayoutMode.Gift) return;

        owner.OnSlotLeftClick(index);
    }
}
