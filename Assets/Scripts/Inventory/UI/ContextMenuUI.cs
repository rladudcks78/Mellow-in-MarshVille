using UnityEngine;
using UnityEngine.Analytics;

public class ContextMenuUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private RectTransform useButtonRect;
    [SerializeField] private Canvas canvas;

    public bool isOpen => root != null && root.activeSelf;
    public int SlotIndex { get; private set; } = -1;
    public RectTransform UseButtonRect => useButtonRect;

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
        if(root == null) root = gameObject;
        if (panelRect == null && root != null) panelRect = root.GetComponent<RectTransform>();
        Hide();
    }

    /// <summary>
    /// 메뉴 보여주기
    /// </summary>
    /// <param name="screenPos"></param>
    /// <param name="slotIndex"></param>
    public void Show(Vector2 screenPos, int slotIndex)
    {
        SlotIndex = slotIndex;

        if (root != null) root.SetActive(true);

        SetPosition(screenPos);
    }

    public void Hide()
    {
        SlotIndex = -1;
        if(root != null) root.SetActive(false);
    }

    private void SetPosition(Vector2 screenPos)
    {
        if (panelRect == null) return;

        // Canvas/RenderMode에 따라 Screen -> Local 변환
        var canvasRect = CanvasRect;
        if(canvas == null)
        {
            panelRect.anchoredPosition = screenPos;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            UICamera,
            out var localPoint 
            );

        panelRect.anchoredPosition = localPoint;
    }
}
