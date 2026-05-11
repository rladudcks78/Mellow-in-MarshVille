using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDTimeView : MonoBehaviour
{
    [Header("--- Text References ---")]
    [Tooltip("시간을 표시할 텍스트 (예: 06:00)")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Tooltip("오전/오후를 표시할 텍스트 (예: AM/PM)")]
    [SerializeField] private TextMeshProUGUI ampmText; // 변수명을 phaseText에서 ampmText로 변경하여 직관적으로 수정

    [Tooltip("요일을 표시할 텍스트 (예: Mon)")]
    [SerializeField] private TextMeshProUGUI weekdayText;

    [Tooltip("일차를 표시할 텍스트 (예: Day 1)")]
    [SerializeField] private TextMeshProUGUI dayCountText;

    [Header("--- Gauge Visuals ---")]
    [Tooltip("회전할 시계바늘 혹은 해/달 아이콘의 Pivot(부모)")]
    [SerializeField] private RectTransform clockHandOrSun;

    [Tooltip("게이지가 시작되는 각도 (왼쪽 = 90)")]
    [SerializeField] private float startAngleZ = 90f;

    [Tooltip("게이지가 끝나는 각도 (오른쪽 = -90)")]
    [SerializeField] private float endAngleZ = -90f;

    [Header("--- Settings ---")]
    [Tooltip("시간 표시를 몇 분 단위로 끊을 것인가? (5분 권장)")]
    [SerializeField] private int timeSnapMinutes = 5;

    // 요일 문자열 캐싱
    private static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public void SetTime(int currentMinuteOfDay, int totalMinutesPerDay, int dayIndex, int weekdayIndex)
    {
        if (totalMinutesPerDay <= 0) totalMinutesPerDay = 24 * 60;
        dayIndex = Mathf.Max(1, dayIndex);
        weekdayIndex = Mathf.Clamp(weekdayIndex, 0, 6);

        // 1. 5분 단위 스냅 처리
        int snappedTotalMinutes = (currentMinuteOfDay / timeSnapMinutes) * timeSnapMinutes;

        int hour24 = (snappedTotalMinutes / 60) % 24;
        int minute = snappedTotalMinutes % 60;

        // 2. 12시간제 변환 및 AM/PM 판단
        bool isPM = hour24 >= 12;
        int hour12 = hour24 % 12;
        if (hour12 == 0) hour12 = 12;

        // [변경] 시간 텍스트에는 이제 숫자만 표시 (12:00)
        if (timeText != null)
            timeText.text = $"{hour12:00}:{minute:00}";

        // [변경] Morning/Night 대신 정확히 AM/PM 표시
        if (ampmText != null)
            ampmText.text = isPM ? "PM" : "AM";

        // 3. 요일 및 날짜 텍스트 갱신
        if (weekdayText != null)
            weekdayText.text = Weekdays[weekdayIndex];

        if (dayCountText != null)
            dayCountText.text = $"Day {dayIndex}";

        // 4. 해/달 회전 연산
        if (clockHandOrSun != null)
        {
            float normalizedTime = (float)currentMinuteOfDay / totalMinutesPerDay;
            float currentAngle = Mathf.Lerp(startAngleZ, endAngleZ, normalizedTime);
            clockHandOrSun.localEulerAngles = new Vector3(0f, 0f, currentAngle);
        }
    }
}