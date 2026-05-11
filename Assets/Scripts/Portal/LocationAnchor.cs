using UnityEngine;

public class LocationAnchor : MonoBehaviour
{
    [Header("기본 설정")]
    [Tooltip("이 위치의 고유 ID (예: Home_In, Village_Out)")]
    public string locationID;

    [Header("카메라 설정")]
    [Tooltip("체크하면 실내(고정 카메라), 해제하면 실외(따라가기)")]
    public bool isIndoor = false;

    [Tooltip("실내일 때 카메라가 고정될 좌표 (씬 뷰에서 카메라 위치를 잡고 그 값을 복사하세요)")]
    public Vector3 fixedCameraPosition;

    private void Start()
    {
        if (TeleportManager.Instance != null)
        {
            TeleportManager.Instance.RegisterLocation(locationID, this);
        }
    }

    private void OnDrawGizmos()
    {
        // 발 위치 (이동 지점)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        // 실내일 경우 카메라가 고정될 위치를 노란색으로 표시
        if (isIndoor)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(fixedCameraPosition, 0.5f);
            Gizmos.DrawWireCube(fixedCameraPosition, new Vector3(16, 9, 0)); // 16:9 화면 비율 예시
        }
    }
}