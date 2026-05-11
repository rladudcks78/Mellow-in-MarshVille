using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHarvesterInputAction : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference clickAction; // Click(<Pointer>/press)

    [Header("Refs")]
    [SerializeField] private Camera cam;

    [Header("Filter")]
    [SerializeField] private LayerMask harvestableLayer;

    [Header("Condition")]
    [SerializeField] private bool handsEmpty = true;

    private Harvestable nearby;
    PlantHoverScale currentHover;

    private void OnEnable()
    {
        if (clickAction == null || clickAction.action == null)
        {
            Debug.LogError("[채집] Click ActionReference가 비어 있습니다(Inspector에 할당 필요).", this);
            return;
        }

        clickAction.action.performed += OnClickPerformed;
        clickAction.action.Enable(); // 액션을 Enable해야 입력을 받습니다.
    }

    private void OnDisable()
    {
        if (clickAction == null || clickAction.action == null) return;

        clickAction.action.performed -= OnClickPerformed;
        clickAction.action.Disable(); // 비활성화도 같이 관리합니다.
    }

    void Update()
    {
        UpdateHover();
    }

    private void OnClickPerformed(InputAction.CallbackContext ctx)
    {
        if (nearby == null) { return; }
        if (cam == null) { Debug.Log("[채집] 실패: 카메라가 연결되지 않았습니다"); return; }
        if (Pointer.current == null) { Debug.Log("[채집] 실패: Pointer 장치가 없습니다"); return; }

        if (!handsEmpty) { Debug.Log("[채집] 실패: 손이 비어있지 않습니다"); return; }

        // Pointer.position은 윈도우 좌표의 포인터 위치입니다.
        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        Vector2 worldPos = world3;

        Collider2D hit = Physics2D.OverlapPoint(worldPos, harvestableLayer);
        if (hit == null) { return; }

        Harvestable clicked = hit.GetComponentInParent<Harvestable>();
        if (clicked == null) { return; }
        if (clicked != nearby) { return; }

        Debug.Log("[채집] 성공: 채집 실행");
        clicked.Harvest();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var h = other.GetComponentInParent<Harvestable>();
        if (h != null) nearby = h;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var h = other.GetComponentInParent<Harvestable>();
        if (h != null && h == nearby) nearby = null;
    }

    public void SetHandsEmpty(bool empty) => handsEmpty = empty;

    void UpdateHover()
    {
        // 기본: 호버 끄기
        void ClearHover()
        {
            if (currentHover != null)
            {
                currentHover.SetHover(false);
                currentHover = null;
            }
        }

        if (!handsEmpty || nearby == null || cam == null || Pointer.current == null)
        {
            ClearHover();
            return;
        }

        Vector2 screenPos = Pointer.current.position.ReadValue(); // 포인터 위치
        Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        Vector2 worldPos = world3;

        Collider2D hit = Physics2D.OverlapPoint(worldPos, harvestableLayer);
        var hoveredHarvestable = hit ? hit.GetComponentInParent<Harvestable>() : null;

        if (hoveredHarvestable != nearby)
        {
            ClearHover();
            return;
        }

        var hover = nearby.GetComponent<PlantHoverScale>();
        if (hover == null) return;

        if (currentHover != hover)
        {
            ClearHover();
            currentHover = hover;
            currentHover.SetHover(true);
        }
    }

}
