using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [역할] 하루가 넘어갈 때 화면 암전(Fade In/Out) 및 '저장 중' 연출을 담당
// [변경] 시계 표시 기능은 HUDTimeView로 이관되어 제거됨
public class DayTimeUI : MonoBehaviour
{
    [Header("--- Transition UI References ---")]
    [Tooltip("전체 화면을 가리는 검은 패널 (CanvasGroup 필수)")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("암전 시 중앙에 표시될 이미지 (달, 침대 아이콘 등) - 옵션")]
    [SerializeField] private Image phaseImage;

    [Tooltip("'저장 중...' 텍스트 오브젝트")]
    [SerializeField] private GameObject saveTextObj;

    [Header("--- Settings ---")]
    [Tooltip("화면이 어두워지거나 밝아지는 데 걸리는 시간")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Tooltip("TimeManager의 Phase(Morning, Afternoon, Night)에 따른 연출 이미지들")]
    [SerializeField] private Sprite[] phaseSprites;

    [Header("--- Input ---")]
    [Tooltip("연출 중 조작을 막기 위한 InputReader")]
    [SerializeField] private InputReader inputReader;

    private void Start()
    {
        // 1. UI 초기 상태 설정
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f; // 투명하게 시작
            fadeCanvasGroup.blocksRaycasts = false; // 클릭 통과
        }
        if (saveTextObj != null) saveTextObj.SetActive(false);

        // 2. TimeManager 이벤트 구독
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayEndStart += HandleDayEndStart;
            TimeManager.Instance.OnDayEndFinish += HandleDayEndFinish;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayEndStart -= HandleDayEndStart;
            TimeManager.Instance.OnDayEndFinish -= HandleDayEndFinish;
        }
    }

    // [이벤트 1] 하루가 끝날 때 (잠자기/기절) -> 화면 어두워짐
    private void HandleDayEndStart()
    {
        StartCoroutine(FadeOutRoutine());
    }

    // [이벤트 2] 다음 날 아침이 밝았을 때 -> 화면 밝아짐
    private void HandleDayEndFinish()
    {
        StartCoroutine(FadeInRoutine());
    }

    // 화면이 어두워지는 코루틴 (Fade Out: Alpha 0 -> 1)
    private IEnumerator FadeOutRoutine()
    {
        // 1. 플레이어 입력 차단
        if (inputReader != null) inputReader.DisableAllInput();

        // 2. 현재 시간대에 맞는 이미지 세팅 (옵션)
        // 보통 잘 때는 '밤' 이미지 혹은 '달' 이미지를 보여줌
        if (phaseImage != null && phaseSprites != null && phaseSprites.Length > 0)
        {
            int spriteIndex = (int)TimeManager.Instance.CurrentPhase;
            spriteIndex = Mathf.Clamp(spriteIndex, 0, phaseSprites.Length - 1);
            phaseImage.sprite = phaseSprites[spriteIndex];
            phaseImage.gameObject.SetActive(true);
        }

        // 3. 페이드 애니메이션 (어두워짐)
        yield return StartCoroutine(Fade(0f, 1f));

        // 4. 저장 텍스트 표시
        if (saveTextObj != null) saveTextObj.SetActive(true);

        // *참고: 실제 날짜 변경 로직은 TimeManager가 수행합니다.
        // 여기서는 오직 시각적 연출만 담당합니다.
    }

    // 화면이 밝아지는 코루틴 (Fade In: Alpha 1 -> 0)
    private IEnumerator FadeInRoutine()
    {
        // 1. 저장 텍스트 숨기기
        if (saveTextObj != null) saveTextObj.SetActive(false);

        // 2. 페이드 애니메이션 (밝아짐)
        yield return StartCoroutine(Fade(1f, 0f));

        // 3. 이미지 숨기기
        if (phaseImage != null) phaseImage.gameObject.SetActive(false);

        // 4. 플레이어 입력 복구
        if (inputReader != null) inputReader.EnablePlayerInput();
    }

    // 알파값 조절 유틸리티
    private IEnumerator Fade(float start, float end)
    {
        float timer = 0f;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = (end > 0.5f); // 어두워지면 클릭 차단
        }

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // TimeScale이 0이어도 연출은 되어야 함
            float progress = timer / fadeDuration;

            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = Mathf.Lerp(start, end, progress);

            yield return null;
        }

        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = end;
    }
}