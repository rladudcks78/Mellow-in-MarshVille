using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.VisualScripting;

public class OptionsMenuUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private GameObject optionsRoot;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Optional Value Text")]
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text sfxValueText;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button toTitleButton;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";

    private bool isSyncing;     //초기 세팅 중 이벤트 폭주 방지
    private bool listenersBound;

    public bool IsOpen => optionsRoot != null && optionsRoot.activeSelf;

    private void Awake()
    {
        if (optionsRoot != null) optionsRoot.SetActive(false);
        BindOnce();
    }

    private void BindOnce()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (toTitleButton != null) toTitleButton.onClick.AddListener(GoToTitle);

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        if (SoundManager.Instance != null)
            SoundManager.Instance.OnVolumeChanged += SyncFromSoundManager;
    }

    private void OnDestroy()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.OnVolumeChanged -= SyncFromSoundManager;
    }

    public void Open()
    {
        if (optionsRoot == null) return;
        if (IsOpen) return;

        optionsRoot.SetActive(true);

        //게임 멈춤
        if (GamePauseManager.Instance != null)
            GamePauseManager.Instance.Enter(GamePauseManager.Modal.Option);

        //입력 UI로
        inputReader?.EnableUIInput();

        //슬라이더 기본값 자동 적용(SoundManager 현재 값 반영)
        SyncFromSoundManager();
    }

    public void Close()
    {
        if (optionsRoot == null) return;
        if (!IsOpen) return;

        optionsRoot.SetActive(false);

        //게임 재개
        if (GamePauseManager.Instance != null)
            GamePauseManager.Instance.Exit(GamePauseManager.Modal.Option);

        //입력 Player로 바꾸기
        inputReader?.EnablePlayerInput();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;

        isSyncing = true;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(sm.Master01);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(sm.Bgm01);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sm.Sfx01);

        UpdateValueText(masterValueText, sm.Master01);
        UpdateValueText(bgmValueText, sm.Bgm01);
        UpdateValueText(sfxValueText, sm.Sfx01);

        isSyncing = false;
    }

    private void OnMasterChanged(float v)
    {
        if (isSyncing) return;
        SoundManager.Instance?.SetMaster01(v);
        UpdateValueText(masterValueText, v);
    }

    private void OnBgmChanged(float v)
    {
        if (isSyncing) return;
        SoundManager.Instance?.SetBgm01(v);
        UpdateValueText(bgmValueText, v);
    }

    private void OnSfxChanged(float v)
    {
        if (isSyncing) return;
        SoundManager.Instance?.SetSfx01(v);
        UpdateValueText(sfxValueText, v);
    }

    private void UpdateValueText(TMP_Text t, float v01)
    {
        if (t == null) return;
        int percent = Mathf.RoundToInt(Mathf.Clamp01(v01) * 100f);
        t.text = $"{percent} %";
    }

    private void GoToTitle()
    {
        //멈춤 남지않게 정리
        Close();

        //보험용
        Time.timeScale = 1f;

        SceneManager.LoadScene(titleSceneName);
    }
}
