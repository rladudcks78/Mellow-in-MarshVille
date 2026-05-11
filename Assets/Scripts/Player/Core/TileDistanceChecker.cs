using UnityEngine;
using UnityEngine.InputSystem;

public class TileDistanceChecker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerData _playerData; 

    private Camera _mainCam;

    private void Awake()
    {
        _mainCam = Camera.main;
    }

    /// <summary>
    /// 현재 마우스 위치가 플레이어 상호작용 사거리 내에 있는지 판정
    /// </summary>
    public bool IsInInteractionRange()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // nearClipPlane 대신 카메라의 Z축 절대값을 사용해야 정확한 월드 좌표가 나옵니다.
        float distanceToPlane = Mathf.Abs(_mainCam.transform.position.z);
        Vector3 worldPos = _mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, distanceToPlane));

        worldPos.z = 0;

        // transform.position(발밑)이 아니라 콜라이더의 중심점(배꼽) 기준 거리를 추천합니다.
        Vector2 playerCenter = GetComponent<Collider2D>().bounds.center;
        float distance = Vector2.Distance(playerCenter, worldPos);

        // Debug.Log($"현재 계산된 거리: {distance} / 사거리: {_playerData.interactRange}");

        return distance <= _playerData.interactRange;
    }

    /// <summary>
    /// 마우스가 가리키는 월드 좌표를 반환합니다. (중복 계산 방지용)
    /// </summary>
    public Vector3 GetMouseWorldPosition()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return Vector3.zero;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        Vector3 worldPos = _mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));

        worldPos.z = 0; // 판정을 위해 0으로 고정
        return worldPos;
    }
}