using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class GlobalLightController : MonoBehaviour
{
    // 싱글톤 추가 (어디서든 접근 가능하게)
    public static GlobalLightController Instance { get; private set; }

    [Header("Outdoor Settings (Time Based)")]
    [Tooltip("하루 24시간의 조명 색상 변화")]
    [SerializeField] private Gradient dayNightColor;
    [Tooltip("하루 24시간의 조명 밝기 변화")]
    [SerializeField] private AnimationCurve dayNightIntensity;
    [SerializeField] private AnimationCurve customTimeFlow = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Indoor Settings (Fixed)")]
    [Tooltip("실내에 들어갔을 때 적용할 고정 색상 (보통 따뜻한 살구색 추천)")]
    [SerializeField] private Color indoorBaseColor = new Color(1f, 0.95f, 0.85f);
    [Tooltip("실내 기본 밝기")]
    [SerializeField] private float indoorBaseIntensity = 1.0f;

    [Header("Transition Settings")]
    [Tooltip("조명이 바뀌는 속도 (높을수록 빠름)")]
    [SerializeField] private float changeSpeed = 5.0f;

    private Light2D _globalLight;
    private float _weatherIntensityMultiplier = 1.0f;

    // 현재 상태 변수
    private bool _isIndoorMode = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _globalLight = GetComponent<Light2D>();
    }

    // TeleportManager가 호출할 함수
    public void SetIndoorMode(bool isIndoor)
    {
        _isIndoorMode = isIndoor;
        Debug.Log($"[LightController] 조명 모드 변경: {(isIndoor ? "실내(고정)" : "실외(시간흐름)")}");
    }

    // 날씨 시스템 연결
    public void SetWeatherModifier(float multiplier)
    {
        _weatherIntensityMultiplier = multiplier;
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;

        // 최종적으로 적용할 목표 색상과 밝기 계산
        Color targetColor;
        float targetIntensity;

        if (_isIndoorMode)
        {
            // [실내 모드] : 시간 무시, 고정값 사용
            targetColor = indoorBaseColor;
            targetIntensity = indoorBaseIntensity;
        }
        else
        {
            // [실외 모드] : 시간 흐름에 따른 계산 (기존 로직)
            float currentHour = TimeManager.Instance.CurrentHour;
            float currentMinute = TimeManager.Instance.CurrentMinute;
            float linearTimePercent = (currentHour + (currentMinute / 60f)) / 24f;
            float distortedTime = customTimeFlow.Evaluate(linearTimePercent);

            targetColor = dayNightColor.Evaluate(distortedTime);
            // 실외일 때만 날씨의 영향을 받음 (보통 실내는 비가 와도 밝으니까)
            targetIntensity = dayNightIntensity.Evaluate(distortedTime) * _weatherIntensityMultiplier;
        }

        // 부드러운 전환 (Lerp) 적용
        // 현재 값에서 목표 값으로 조금씩 이동
        _globalLight.color = Color.Lerp(_globalLight.color, targetColor, Time.deltaTime * changeSpeed);
        _globalLight.intensity = Mathf.Lerp(_globalLight.intensity, targetIntensity, Time.deltaTime * changeSpeed);
    }
}