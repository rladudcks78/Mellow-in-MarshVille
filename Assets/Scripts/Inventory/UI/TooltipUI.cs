using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Canvas canvas;

    [Header("Offset")]
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, 0f);
    [SerializeField] private bool clampToCanvas = true;

    [Header("Content")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text amountText;

    [Header("Quality Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color sloppyColor = new Color(0.7f, 0.7f, 0.7f); // 회색
    [SerializeField] private Color perfectColor = new Color(1f, 0.84f, 0f);   // 금색

    public bool isOpen => root != null && root.activeSelf;

    private Camera UICamera
    {
        get
        {
            if (canvas == null) return null;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }
    }

    private RectTransform CanvasRect => canvas != null ? canvas.GetComponent<RectTransform>() : null;



    private void Awake()
    {
        if (root == null) root = gameObject;
        if (panelRect == null) panelRect = root.GetComponent<RectTransform>();
        Hide();
    }


    public void Show(Sprite sprite, string name, string desc, int amount, int quality = 0)
    {
        if (root != null)
            root.SetActive(true);

        if (icon != null)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }

        // 품질 텍스트 처리 (이름 옆에 붙이기)
        if (nameText != null)
        {
            string finalName = name ?? "";
            string qualitySuffix = "";
            string colorHex = ColorUtility.ToHtmlStringRGB(normalColor);

            if (quality == 1) // Sloppy
            {
                qualitySuffix = " (엉성함)";
                colorHex = ColorUtility.ToHtmlStringRGB(sloppyColor);
            }
            else if (quality == 2) // Perfect
            {
                qualitySuffix = " (완벽함!)";
                colorHex = ColorUtility.ToHtmlStringRGB(perfectColor);
            }

            // RichText를 이용해 품질 부분만 색상 변경
            nameText.text = $"{finalName}<size=80%><color=#{colorHex}>{qualitySuffix}</color></size>";
        }

        if (descText != null)
            descText.text = desc ?? "";
        if (amountText != null)
            amountText.text = $"{amount}";
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    public void SetPosition(Vector2 screenPos)
    {
        if (panelRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            CanvasRect,
            screenPos,
            UICamera,
            out var localPoint 
            );

        Vector2 p = localPoint + screenOffset;

        if(clampToCanvas)
            p = ClampToCanvas(p, CanvasRect, panelRect);

        panelRect.anchoredPosition = p;
    }

    private Vector2 ClampToCanvas(Vector2 anchoredPos, RectTransform canvasRect, RectTransform panel)
    {
        var canvasSize = canvasRect.rect.size;
        var panelSize = panel.rect.size;
        var pivot = panel.pivot;

        float minX = -canvasSize.x * 0.5f + panelSize.x * pivot.x;
        float maxX = canvasSize.x * 0.5f - panelSize.x * (1f -pivot.x);

        float minY = -canvasSize.y * 0.5f + panelSize.y * pivot.y;
        float maxY = canvasSize.y * 0.5f - panelSize.y * (1f - pivot.y);

        anchoredPos.x = Mathf.Clamp(anchoredPos.x, minX, maxX);
        anchoredPos.y = Mathf.Clamp(anchoredPos.y, minY, maxY);

        return anchoredPos;
    }
}
