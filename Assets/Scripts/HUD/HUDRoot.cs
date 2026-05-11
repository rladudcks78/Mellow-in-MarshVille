using UnityEngine;

public class HUDRoot : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] private HUDHealthView healthView;
    [SerializeField] private HUDSkillView skillView;
    [SerializeField] private HUDTimeView timeView;
    [SerializeField] private HUDCurrencyView currencyView;
    [SerializeField] private bool useDebugClock = true;
    [SerializeField] private int debugMinutesPerDay = 24 * 60;           //하루 24시간이라 치고
    [SerializeField] private int debugMinuteOfDay = 6 * 60;              //06 : 00
    [SerializeField] private int debugDayIndex = 1;                      //1일차
    [SerializeField] private int debugWeekdayIndex = 0;                  //0 = 월요일 가정
    [SerializeField] private bool debugTick = true;                      //시간이 흘러가는 것 표현
    [SerializeField] private float debugTickSecondsPerMinute = 0.25f;    //0.25초에 1분

    private float debugAcc;

    private void Start()
    {
        //healthView?.SetHealth(0, 0);
        currencyView?.SetGold(0);
        skillView?.SetSkillIcon(null);
        skillView?.ClearCooldownVisual();

        if(useDebugClock)
            timeView?.SetTime(debugMinuteOfDay, debugMinutesPerDay, debugDayIndex, debugWeekdayIndex);
    }

    private void Update()
    {
        if (!useDebugClock || !debugTick) return;
        if (timeView == null) return;

        debugAcc += Time.deltaTime;
        if (debugAcc < debugTickSecondsPerMinute) return;
        debugAcc = 0f;

        debugMinuteOfDay++;
        if(debugMinuteOfDay >= debugMinutesPerDay)
        {
            debugMinuteOfDay = 0;
            debugDayIndex++;
            debugWeekdayIndex = (debugWeekdayIndex + 1) % 7;
        }

        timeView.SetTime(debugMinuteOfDay, debugMinutesPerDay, debugDayIndex, debugWeekdayIndex);

        if (!useDebugClock && TimeManager.Instance != null)
        {
            // TimeManager에서 현재 시간 정보를 가져옴
            int currentHour = TimeManager.Instance.CurrentHour;
            int currentMinute = TimeManager.Instance.CurrentMinute;

            // 분 단위로 변환
            int totalMinOfDay = currentHour * 60 + currentMinute;

            // UI 갱신
            SetTime(totalMinOfDay, 24 * 60, TimeManager.Instance.CurrentDay, 0);
            // 주의: TimeManager에 현재 요일(Weekday) 정보가 없다면 추가 구현이 필요합니다.
            // 현재 제공된 TimeManager 코드에는 요일 변수가 없습니다. 
            // (CurrentDay % 7) 로 계산해서 넣으시면 됩니다.
        }
    }

    public void SetHealth(int current, int max) => healthView?.SetHealth(current, max);
    public void SetGold(int gold) => currencyView?.SetGold(gold);
    public void SetSkillIcon(Sprite icon) => skillView?.SetSkillIcon(icon);

    
    public void TriggerSkillCooldown(float durationSeconds) 
        => skillView?.StartCooldown(durationSeconds);

    public void SetTime(int minuteOfDay, int minutesPerDay, int dayIndex, int weekdayIndex)
        => timeView?.SetTime(minuteOfDay, minutesPerDay, dayIndex, weekdayIndex);
}
