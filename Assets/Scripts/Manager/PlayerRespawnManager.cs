using System.Collections;
using UnityEngine;

public class PlayerRespawnManager : MonoBehaviour
{
    [Header("--- References ---")]
    [Tooltip("이동시킬 플레이어의 Transform")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("플레이어의 체력 컴포넌트")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("플레이어가 일어날 침대의 스폰 포인트")]
    [SerializeField] private Transform bedSpawnPoint;

    private void Start()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDied += HandlePlayerDied;
        }
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayEndStart += HandleDayEndStart;
        }

        // 씬이 로드될 때(이어하기 시) 무조건 실내 환경으로 초기화 
        InitializeIndoorEnvironment();
    }

    private void InitializeIndoorEnvironment()
    {
        Debug.Log("[PlayerRespawnManager] 게임 시작/로드 감지. 실내 환경으로 초기화합니다.");

        if (GlobalLightController.Instance != null)
            GlobalLightController.Instance.SetIndoorMode(true);

        if (WeatherManager.Instance != null)
            WeatherManager.Instance.SetIndoorMode(true);

        if (bedSpawnPoint != null)
        {
            LocationAnchor bedAnchor = bedSpawnPoint.GetComponent<LocationAnchor>();
            if (CameraManager.Instance != null && bedAnchor != null)
            {
                CameraManager.Instance.UpdateCameraState(bedAnchor);
            }
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
        if (TimeManager.Instance != null) TimeManager.Instance.OnDayEndStart -= HandleDayEndStart;
    }

    private void HandlePlayerDied()
    {
        Debug.Log("[PlayerRespawnManager] 플레이어 사망 감지. 하루를 강제로 종료합니다.");

        if (TimeManager.Instance != null && !TimeManager.Instance.IsTimeStopped)
        {
            TimeManager.Instance.Sleep();
        }
    }

    private void HandleDayEndStart()
    {
        StartCoroutine(MovePlayerToBedRoutine());
    }

    private IEnumerator MovePlayerToBedRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (playerTransform != null && bedSpawnPoint != null)
        {
            playerTransform.position = bedSpawnPoint.position;
            Debug.Log("[PlayerRespawnManager] 플레이어를 침대로 이동 완료.");

            if (GlobalLightController.Instance != null)
                GlobalLightController.Instance.SetIndoorMode(true);

            if (WeatherManager.Instance != null)
                WeatherManager.Instance.SetIndoorMode(true);

            LocationAnchor bedAnchor = bedSpawnPoint.GetComponent<LocationAnchor>();
            if (CameraManager.Instance != null && bedAnchor != null)
                CameraManager.Instance.UpdateCameraState(bedAnchor);

            // ======================================================
        }

        if (playerHealth != null)
        {
            playerHealth.RestoreHealth(); // 체력 회복
        }
    }
}