using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartSetting : MonoBehaviour
{
    [Header("UI 참조")]
    public Button continueBtn;   // 이어하기
    public Button startBtn;      // 처음 시작
    public Button optionBtn;     // 설정
    public Button exitBtn;       // 게임 종료

    [Header("Scene")]
    [SerializeField] private string inGameSceneName = "InGame";

    private void Start()
    {
        // 버튼 이벤트 연결
        if (continueBtn != null) continueBtn.onClick.AddListener(OnContinueButtonClicked);
        if (startBtn != null) startBtn.onClick.AddListener(OnStartGame);
        if (optionBtn != null) optionBtn.onClick.AddListener(Option);
        if (exitBtn != null) exitBtn.onClick.AddListener(Exit);

        UpdateContinueButton();
    }

    private void OnEnable()
    {
        // 타이틀 씬으로 다시 돌아왔을 때도 상태 갱신되게
        UpdateContinueButton();
    }

    /// <summary>
    /// 새 게임 시작 버튼 (처음 시작)
    /// </summary>
    private void OnStartGame()
    {
        if (SaveManagerV1.Instance != null)
        {
            // 새 게임에서는 이전 이어하기 로드 예약이 남아있지 않게 정리
            SaveManagerV1.Instance.ClearPendingLoad();

            // 정책 선택:
            // 기존 저장을 즉시 삭제하고 싶으면 아래 주석 해제
            // SaveManagerV1.Instance.DeleteContinueSave();
        }

        Debug.Log("[StartSetting] 새 게임 시작 -> InGame 씬 이동");
        SceneManager.LoadScene(inGameSceneName);
    }

    /// <summary>
    /// 이어하기 버튼
    /// </summary>
    private void OnContinueButtonClicked()
    {
        Debug.Log("[StartSetting] 이어하기 클릭");

        if (SaveManagerV1.Instance == null)
        {
            Debug.LogError("[StartSetting] SaveManagerV1.Instance를 찾을 수 없습니다.");
            return;
        }

        if (!SaveManagerV1.Instance.HasContinueSave())
        {
            Debug.LogWarning("[StartSetting] 저장 데이터가 없습니다.");
            UpdateContinueButton();
            return;
        }

        SaveManagerV1.Instance.RequestLoadContinue();

        Debug.Log("[StartSetting] 이어하기 로드 예약 완료 -> InGame 씬 이동");
        SceneManager.LoadScene(inGameSceneName);
    }

    private void Option()
    {
        Debug.Log("[StartSetting] 옵션 버튼 선택");
        // TODO: 옵션 패널 열기
    }

    private void Exit()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// 이어하기 버튼 활성화/비활성화
    /// </summary>
    private void UpdateContinueButton()
    {
        bool hasSaveData = SaveManagerV1.Instance != null && SaveManagerV1.Instance.HasContinueSave();

        if (continueBtn != null)
            continueBtn.interactable = hasSaveData;

        Debug.Log($"[StartSetting] 저장 데이터 존재: {hasSaveData}");
    }
}