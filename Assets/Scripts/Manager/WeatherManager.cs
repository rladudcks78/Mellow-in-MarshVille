using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    public enum WeatherType { Sunny, Rainy }

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float rainChance = 0.5f; 

    [Header("References")]
    [SerializeField] private ParticleSystem _rainParticle;
    [SerializeField] private GlobalLightController _lightController;

    public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;

    private bool _isIndoor = false;
    public bool _IsIndoor => _isIndoor;

    //Save/Load
    private bool _skipDetermineOnce = false;
    public void SkipDetermineOnce() => _skipDetermineOnce = true;

    [System.Serializable]
    public class WeatherSaveDataV1
    {
        public int currentWeather;
        public bool isIndoor;
    }

    public WeatherSaveDataV1 CaptureSnapshotV1()
    {
        return new WeatherSaveDataV1
        {
            currentWeather = (int)CurrentWeather,
            isIndoor = _isIndoor
        };
    }

    public void RestoreSnapshotV1(WeatherSaveDataV1 data)
    {
        if (data == null) return;
        _isIndoor = data.isIndoor;
        CurrentWeather = (WeatherType)Mathf.Clamp(data.currentWeather, 0, 1);

        ApplyWeatherEffects();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay += DetermineDailyWeather;
        }

        if (_skipDetermineOnce)
        {
            _skipDetermineOnce = false;
            ApplyWeatherEffects();
        }
        else
        {
            // 게임 시작 시 날씨 결정
            DetermineDailyWeather(TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1);
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnNewDay -= DetermineDailyWeather;
        }
    }

    // TeleportManager가 호출할 함수
    public void SetIndoorMode(bool isIndoor)
    {
        _isIndoor = isIndoor;

        Debug.Log($"{_IsIndoor}");
        // 장소가 바뀌었으니 파티클 상태를 다시 체크해서 적용
        ApplyWeatherEffects();
    }

    private void DetermineDailyWeather(int day)
    {
        float roll = Random.value;
        CurrentWeather = (roll <= rainChance) ? WeatherType.Rainy : WeatherType.Sunny;
        Debug.Log($"[WeatherManager] 날씨 결정: {CurrentWeather}");

        ApplyWeatherEffects();
    }

    private void ApplyWeatherEffects()
    {
        bool isRaining = CurrentWeather == WeatherType.Rainy && _isIndoor == false;

        if (_rainParticle != null)
        {
            if (isRaining)
            {
                _rainParticle.Play();
                Debug.Log("[WeatherManager] 비 파티클 재생");
            }
            else
            {
                _rainParticle.Stop();
                Debug.Log("[WeatherManager] 비 파티클 정지");
            }
        }
        else
        {
            Debug.LogWarning("[WeatherManager] 비 파티클이 할당되지 않았습니다!");
        }

        if (_lightController != null)
        {
            float intensityMult = isRaining ? 0.7f : 1.0f;
            _lightController.SetWeatherModifier(intensityMult);
        }

        if(SoundManager.Instance != null)
        {
            print("WeatherManager : 비 소리 낸다");
            if (isRaining) SoundManager.Instance.PlayRainLoop();
            else SoundManager.Instance.StopRainLoop();
        }
    }

    public WeatherSaveData CaptureSave()
    {
        var data = new WeatherSaveData
        {
            currentWeather = (CurrentWeather == WeatherType.Rainy)
            ? WeatherSaveData.WeatherTypeV1.Rainy
            : WeatherSaveData.WeatherTypeV1.Sunny,
            isIndoor = _IsIndoor,
            rainChance = 1f
        };

        //설정값도 저장
        data.rainChance = rainChance;

        return data;
    }

    public void ApplySave(WeatherSaveData data)
    {
        if (data == null) return;
        _isIndoor = data.isIndoor;

        //저장된 설정값을 유지하기
        if (data.rainChance >= 0f && data.rainChance <= 1f)
            rainChance = data.rainChance;

        CurrentWeather = (data.currentWeather == WeatherSaveData.WeatherTypeV1.Rainy)
            ? WeatherType.Rainy
            : WeatherType.Sunny;

        ApplyWeatherEffects();
    }
}