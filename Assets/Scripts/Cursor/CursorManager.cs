using UnityEngine;
using UnityEngine.UI;

// [Portfolio] 기획서에 맞춰 커서 상태를 세분화하여 구현력을 어필합니다.
public enum CursorState
{
     Normal,      // 기본 화살표 (이동, 농사 도구 사용 시) [cite: 21, 75]
     Talk,        // NPC와 대화 가능 시 (말풍선 아이콘) [cite: 22, 57]
     Grab         // 아이템 줍기 가능 시 (손바닥 아이콘) [cite: 22, 108]
}

public class CursorManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image _cursorImage;
    [SerializeField] private RectTransform _rectTransform;

    [Header("Sprites")]
    [SerializeField] private Sprite _normalSprite; // 기본 화살표
    [SerializeField] private Sprite _talkSprite;   // 말풍선 아이콘
    [SerializeField] private Sprite _grabSprite;   // 손바닥 아이콘

    private void Awake()
    {
        Cursor.visible = false; // 시스템 커서 숨김
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        // 마우스 위치 실시간 추적
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        _rectTransform.position = mousePos;
    }

    public void SetCursorState(CursorState state)
    {
        switch (state)
        {
            case CursorState.Normal:
                UpdateVisual(_normalSprite, new Vector2(0, 1)); // 화살표는 좌상단 피벗
                break;

            case CursorState.Talk:
                UpdateVisual(_talkSprite, new Vector2(0.5f, 0.5f)); // 아이콘은 중앙 피벗
                break;

            case CursorState.Grab:
                UpdateVisual(_grabSprite, new Vector2(0.5f, 0.5f)); // 아이콘은 중앙 피벗
                break;
        }
    }

    private void UpdateVisual(Sprite sprite, Vector2 pivot)
    {
        if (_cursorImage.sprite != sprite) _cursorImage.sprite = sprite;
        if (_rectTransform.pivot != pivot) _rectTransform.pivot = pivot;
        if (_cursorImage.color != Color.white) _cursorImage.color = Color.white;
    }

    private void OnDisable() => Cursor.visible = true;
}