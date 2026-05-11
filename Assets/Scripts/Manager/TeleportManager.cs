using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TeleportManager : MonoBehaviour
{
    public static TeleportManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private AudioSnapshotController snapshotController;

    [Header("설정")]
    [SerializeField] private float fadeDuration = 0.8f; // 깜빡이는 속도

    [Header("연결 필요")]
    [SerializeField] private Image fadeImage;      
    [SerializeField] private InputReader inputReader; 
    [SerializeField] private Transform playerTransform; 

    // LocationAnchor 정보를 통째로 저장
    private Dictionary<string, LocationAnchor> locationRegistry = new Dictionary<string, LocationAnchor>();

    private bool isTeleporting = false;
    private Material fadeMaterial;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this; 
            //DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            // 쉐이더 재질 복제 및 초기화
            fadeMaterial = new Material(fadeImage.material);
            fadeImage.material = fadeMaterial;
            fadeMaterial.SetFloat("_Radius", 1.5f); // 구멍 열어둠
            fadeImage.gameObject.SetActive(true);
        }
    }

    // 앵커 등록 함수가 LocationAnchor 타입을 받음
    public void RegisterLocation(string locationID, LocationAnchor anchor)
    {
        if (!locationRegistry.ContainsKey(locationID))
        {
            locationRegistry.Add(locationID, anchor);
        }
    }

    public void RequestTeleport(string targetLocationID)
    {
        if (isTeleporting) return;

        if (locationRegistry.ContainsKey(targetLocationID))
        {
            StartCoroutine(IrisTeleportRoutine(locationRegistry[targetLocationID]));
        }
        else
        {
            Debug.LogError($"[TeleportManager] ID를 찾을 수 없습니다: {targetLocationID}");
        }
    }

    private IEnumerator IrisTeleportRoutine(LocationAnchor targetAnchor)
    {
        isTeleporting = true;
        inputReader.DisableAllInput();

        // 1. 암전
        yield return StartCoroutine(AnimateIris(1.5f, 0f));

        Vector3 targetPos = targetAnchor.transform.position;
        targetPos.z = playerTransform.position.z;
        playerTransform.position = targetPos;

        // 1. 카메라 설정
        if (CameraManager.Instance != null)
            CameraManager.Instance.UpdateCameraState(targetAnchor);

        // 2. 조명 설정
        if (GlobalLightController.Instance != null)
            GlobalLightController.Instance.SetIndoorMode(targetAnchor.isIndoor);

        Debug.Log($"{targetAnchor.isIndoor}");

        // 3. 날씨
        if (WeatherManager.Instance != null)
            WeatherManager.Instance.SetIndoorMode(targetAnchor.isIndoor);

        // 4. 대기 및 화면 열기
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(AnimateIris(0f, 1.5f));

        snapshotController.Apply();

        inputReader.EnablePlayerInput();
        isTeleporting = false;
    }

    private IEnumerator AnimateIris(float startRadius, float endRadius)
    {
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            t = t * t * (3f - 2f * t);      // 부드러운 움직임

            fadeMaterial.SetFloat("_Radius", Mathf.Lerp(startRadius, endRadius, t));
            yield return null;
        }
        fadeMaterial.SetFloat("_Radius", endRadius);
    }
}