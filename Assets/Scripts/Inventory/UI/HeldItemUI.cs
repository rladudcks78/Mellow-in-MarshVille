using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HeldItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform rect;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    [Header("Refs")]
    [SerializeField] private SharedHeldSystem held;              // 추가
    [SerializeField] private InventorySystem inventorySystem;    // 추가(스프라이트 찾으려면 필요)
    [SerializeField] private SpriteResolver spriteResolver;      // 추가(너 프로젝트에 이미 쓰는거)

    private void Awake()
    {
        if (rect == null) rect = (RectTransform)transform;

        if (held == null) held = SharedHeldSystem.Instance;
        if (held == null) held = FindFirstObjectByType<SharedHeldSystem>();

        if (inventorySystem == null) inventorySystem = FindFirstObjectByType<InventorySystem>();

        Hide();
    }

    private void OnEnable()
    {
        if (held != null) held.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (held != null) held.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (held == null || !held.HasItem)
        {
            Hide();
            return;
        }

        var stack = held.Stack;

        // 스프라이트 해석
        Sprite sp = null;
        if (inventorySystem != null && inventorySystem.TryGetDef(stack.itemId, out var def))
            sp = spriteResolver != null ? spriteResolver.Load(def.spritePath) : null;

        Show(sp, stack.amount);
    }

    public void Show(Sprite sprite, int amount)
    {
        gameObject.SetActive(true);

        icon.enabled = sprite != null;
        icon.sprite = sprite;

        amountText.text = amount > 1 ? amount.ToString() : ""; // <- 1도 보이게 하려면 아래 참고
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void TickFollowMouse()
    {
        if (Mouse.current == null) return;

        Vector2 pos = Mouse.current.position.ReadValue();
        rect.position = pos;
    }
}
