using UnityEngine;
using UnityEngine.EventSystems;

public class CursorController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CursorManager _cursorManager;
    [SerializeField] private TileDistanceChecker _distChecker;

    [Header("Settings")]
    [SerializeField] private LayerMask _interactableLayers; // NPC와 Pickable 레이어 모두 체크

    private void Update()
    {
        UpdateCursorState();
    }

    private void UpdateCursorState()
    {
        // 1. UI 우선순위 체크
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _cursorManager.SetCursorState(CursorState.Normal);
            return;
        }

        // 2. 상호작용 범위 내 체크 
        bool isInRange = _distChecker.IsInInteractionRange();

        if (isInRange)
        {
            Vector3 mouseWorldPos = _distChecker.GetMouseWorldPosition();
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos, _interactableLayers);

            if (hit != null)
            {
                // [핵심] 레이어 이름을 확인하여 커서 모양을 결정합니다.
                string layerName = LayerMask.LayerToName(hit.gameObject.layer);

                if (layerName == "NPC") // NPC 레이어일 경우 
                {
                    _cursorManager.SetCursorState(CursorState.Talk);
                    return;
                }
                else if (layerName == "Pickable") // 아이템 줍기 레이어일 경우 
                {
                    _cursorManager.SetCursorState(CursorState.Grab);
                    return;
                }
            }
        }

        // 3. 그 외 (농사 중이거나 빈 땅일 때) 
        _cursorManager.SetCursorState(CursorState.Normal);
    }
}