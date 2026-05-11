using System;
using System.Collections;
using UnityEngine;

// 게임 시간 관리자 
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    public enum DayPhase { Morning, Afternoon, Night }

    [Header("--- UI Link ---")]
    [SerializeField] private HUDRoot hudRoot;

    [Header("--- Time Settings ---")]
    [Tooltip("현실 시간 몇 초가 게임의 하루인가? (기본 1200초 = 20분)")]
    [SerializeField] private float realSecondsPerGameDay = 1200f;

    [Header("--- Hour Settings ---")]
    [SerializeField] private int morningStartHour = 6;
    [SerializeField] private int penaltyWakeUpHour = 10;
    [SerializeField] private int passOutHour = 2; // 새벽 2시

    [Header("--- Pass Out Settings ---")]
    [SerializeField] private float passOutAnimDuration = 3.0f;
    [SerializeField] private int passOutPenaltyMoney = 100;

    [Header("--- Phase Settings ---")]
    [SerializeField] private int afternoonStartHour = 12;
    [SerializeField] private int nightStartHour = 18;

    [Header("--- Debug ---")]
    [SerializeField, Range(0.1f, 100f)] private float timeScaleMultiplier = 1f;

    // --- Properties ---
    public int CurrentDay { get; private set; } = 1;
    public int CurrentWeekdayIndex => (CurrentDay - 1) % 7;
    public DayPhase CurrentPhase { get; private set; }

    // 이 값이 true면 시간 흐름과 UI 갱신이 완전히 멈춥니다.
    public bool IsTimeStopped { get; set; } = false;

    private float _currentGameTime;
    private bool _wasPassOutLastNight = false;

    // --- Events ---
    public event Action<int> OnNewDay;
    public event Action<DayPhase> OnPhaseChange;
    public event Action OnPassOutAnimTrigger;
    public event Action OnDayEndStart;
    public event Action OnDayEndFinish;

    //Save/Load
    private bool _skipAutoStartOnce = false;
    public void SkipAutoStartOnce() => _skipAutoStartOnce = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (hudRoot == null) hudRoot = FindFirstObjectByType<HUDRoot>();

        if (_skipAutoStartOnce)
        {
            _skipAutoStartOnce = false;
            return;
        }

        StartDay(morningStartHour);
    }

    private void Update()
    {
        //  시간이 멈춰있으면 하위 로직(시간 증가, UI 갱신, 기절 체크)을 아예 실행하지 않음
        if (IsTimeStopped) return;

        // 1. 시간 흐름 계산
        float gameHoursPerRealSec = 24f / realSecondsPerGameDay;
        _currentGameTime += Time.deltaTime * gameHoursPerRealSec * timeScaleMultiplier;

        // 2. UI 및 상태 갱신
        CheckDayPhase();
        UpdateHUDTime();

        // 3. 기절 체크 (02:00 = 26:00)
        // 시간이 다 되었으면 즉시 코루틴 시작하고 Update 종료
        if (_currentGameTime >= 24 + passOutHour)
        {
            StartCoroutine(ProcessPassOutSequence());
        }
    }

    // [기능 1] 잠자기
    public void Sleep()
    {
        // 이미 멈춰있다면 중복 실행 방지
        if (IsTimeStopped) return;

        Debug.Log("[TimeManager] 잠을 잡니다.");
        _wasPassOutLastNight = false;

        // 코루틴 시작 전, 호출 즉시 시간을 멈춤 (안전장치)
        IsTimeStopped = true;

        StartCoroutine(SleepRoutine());
    }

    private IEnumerator SleepRoutine()
    {
        // 혹시 모르니 한번 더 확실하게 멈춤
        IsTimeStopped = true;

        // 암전 시작 (페이드 아웃)
        OnDayEndStart?.Invoke();

        // 페이드 아웃되는 동안 대기 (UI 애니메이션 시간)
        yield return new WaitForSeconds(2.0f);

        ProceedToNextDay();
    }

    // [기능 2] 기절 시퀀스
    private IEnumerator ProcessPassOutSequence()
    {
        // 기절 시작 즉시 시간 정지
        IsTimeStopped = true;
        _wasPassOutLastNight = true;

        Debug.Log("[TimeManager] 02:00! 기절합니다.");

        // 1. 쓰러지는 애니메이션 재생 등
        OnPassOutAnimTrigger?.Invoke();

        // 2. 애니메이션 재생 시간동안 대기 (이때 시간은 멈춰있어야 함)
        yield return new WaitForSeconds(passOutAnimDuration);

        // 3. 암전 시작
        OnDayEndStart?.Invoke();

        // 4. 암전 대기
        yield return new WaitForSeconds(2.0f);

        ProceedToNextDay();
    }

    private void ProceedToNextDay()
    {
        CurrentDay++;
        Debug.Log($"[디버그] 날짜가 증가했습니다! 현재: {CurrentDay}일차");

        int wakeUpHour = _wasPassOutLastNight ? penaltyWakeUpHour : morningStartHour;

        if (_wasPassOutLastNight)
        {
            Debug.Log($"[Economy] 기절 패널티 {passOutPenaltyMoney}G 차감");
        }

        StartDay(wakeUpHour);
    }

    private void StartDay(int hour)
    {
        // 1. 데이터 세팅 (시간은 아직 멈춘 상태 IsTimeStopped = true)
        _currentGameTime = hour;
        CheckDayPhase();

        // 2. UI 강제 갱신 
        // (암전된 상태여도 시계 바늘과 텍스트는 미리 06:00/10:00로 맞춰둠)
        UpdateHUDTime();

        // 3. 새 하루 이벤트 발생
        OnNewDay?.Invoke(CurrentDay);

        // 4. 화면 밝아짐 시작 
        OnDayEndFinish?.Invoke();

        Debug.Log($"[TimeManager] {CurrentDay}일차 {hour}시 기상! (화면 밝아지는 중...)");

        // 5. 화면이 완전히 밝아질 때까지 기다린 후 시간을 흐르게 함
        StartCoroutine(WaitAndStartDayRoutine());
    }

    // 화면 연출 대기 코루틴
    private IEnumerator WaitAndStartDayRoutine()
    {
        // DayTimeUI의 Fade Duration(약 1.0초)보다 조금 더 넉넉하게 대기
        // 1.5초 정도 기다리면 안정적입니다.
        yield return new WaitForSeconds(1.5f);

        // 연출이 다 끝난 뒤에 시간 흐름 재개
        IsTimeStopped = false;
        Debug.Log("[TimeManager] 화면 연출 종료. 시간 흐름 재개.");
    }

    // UI 갱신 로직
    private void UpdateHUDTime()
    {
        if (hudRoot == null) return;
        int currentMinuteOfDay = Mathf.FloorToInt(_currentGameTime * 60f);
        hudRoot.SetTime(currentMinuteOfDay, 24 * 60, CurrentDay, CurrentWeekdayIndex);
    }

    private void CheckDayPhase()
    {
        float checkHour = _currentGameTime >= 24 ? _currentGameTime - 24 : _currentGameTime;
        DayPhase nextPhase = CurrentPhase;

        if (checkHour >= morningStartHour && checkHour < afternoonStartHour) nextPhase = DayPhase.Morning;
        else if (checkHour >= afternoonStartHour && checkHour < nightStartHour) nextPhase = DayPhase.Afternoon;
        else nextPhase = DayPhase.Night;

        if (nextPhase != CurrentPhase)
        {
            CurrentPhase = nextPhase;
            OnPhaseChange?.Invoke(CurrentPhase);
        }
    }

    public string GetTimeOfDay()
    {
        int hour = CurrentHour;

        if (hour >= 6 && hour < 12)
            return "morning";
        else if (hour >= 12 && hour < 17)
            return "afternoon";
        else if (hour >= 17 && hour < 21)
            return "evening";
        else
            return "night";
    }

    public int CurrentHour => Mathf.FloorToInt(_currentGameTime) % 24;
    public int CurrentMinute => Mathf.FloorToInt((_currentGameTime - Mathf.Floor(_currentGameTime)) * 60f);

    //Save Load관련
    public float CurrentGameTimeHours => _currentGameTime;
    public bool WasPassOutLastNight => _wasPassOutLastNight;

    [System.Serializable]
    public class TimeSaveDataV1
    {
        public int currentDay;
        public float currentGameTime;
        public bool wasPassOutLastNight;
        public bool isTimeStopped;
    }

    public TimeSaveDataV1 CaptureSnapshotV1()
    {
        return new TimeSaveDataV1
        {
            currentDay = CurrentDay,
            currentGameTime = _currentGameTime,
            wasPassOutLastNight = _wasPassOutLastNight,
            isTimeStopped = IsTimeStopped
        };
    }

    public void RestoreSnapshotV1(TimeSaveDataV1 data)
    {
        if (data == null) return;

        CurrentDay = Mathf.Max(1, data.currentDay);
        _currentGameTime = data.currentGameTime;
        _wasPassOutLastNight = data.wasPassOutLastNight;
        IsTimeStopped = data.isTimeStopped;

        CheckDayPhase();
        UpdateHUDTime();
    }

    /// <summary>
    /// 세이브용 스냅샷 생성
    /// </summary>
    /// <returns></returns>
    public TimeSaveData CaptureSave()
    {
        return new TimeSaveData
        {
            currentDay = CurrentDay,
            currentGameTimeHours = _currentGameTime,
            wasPassOutLastNight = _wasPassOutLastNight,
            isTimeStopped = IsTimeStopped
        };

    }

    /// <summary>
    /// 세이브에서 상태 복원
    /// </summary>
    /// <param name="data"></param>
    public void ApplySave(TimeSaveData data)
    {
        if (data == null) return;

        StopAllCoroutines();

        CurrentDay = Mathf.Max(1, data.currentDay);
        _currentGameTime = Mathf.Max(0f, data.currentGameTimeHours);
        _wasPassOutLastNight = data.wasPassOutLastNight;

        // 세이브 데이터의 값(data.isTimeStopped)을 무시하고, 
        // 로드 직후에는 무조건 시간이 정상적으로 흐르도록 강제 해제합니다.
        IsTimeStopped = false;

        // 상태/UI 갱신
        CheckDayPhase();
        UpdateHUDTime();
    }


}