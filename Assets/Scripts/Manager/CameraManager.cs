using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 카메라의 상태 전환 및 순간 이동(Warp)을 관리하는 클래스입니다.
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("연결 필요")]
    [SerializeField] private CinemachineCamera vCam;     // 시네머신 가상 카메라
    [SerializeField] private Transform playerTransform;  // 플레이어 위치 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// TeleportManager에서 호출하는 카메라 상태 업데이트 함수입니다.
    /// 실내/실외 여부에 상관없이 플레이어 위치로 카메라를 즉시 동기화합니다.
    /// </summary>
    /// <param name="anchor">이동할 목적지의 앵커 정보</param>
    public void UpdateCameraState(LocationAnchor anchor)
    {
        if (vCam == null || playerTransform == null) return;

        // 1. 목적지 좌표 설정 (Z축은 카메라 기본값인 -10 유지)
        Vector3 targetPos = anchor.transform.position;
        targetPos.z = -10f;

        // 2. 현재 카메라 위치와 목적지 위치의 차이 계산 (Warp 연산용)
        Vector3 positionDelta = targetPos - vCam.transform.position;

        // 3. 카메라 트랜스폼 위치 즉시 수정
        vCam.transform.position = targetPos;

        // 4. 시네머신 엔진에 워프 사실을 통보 (화면 튐 방지 핵심 로직)
        vCam.OnTargetObjectWarped(playerTransform, positionDelta);

        // 5. 시네머신 내부 캐시 강제 업데이트
        vCam.ForceCameraPosition(targetPos, Quaternion.identity);

        Debug.Log($"[CameraManager] {anchor.name}으로 카메라 워프 완료.");
    }
}