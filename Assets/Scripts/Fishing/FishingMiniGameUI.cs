using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.InputSystem;

public class FishingMiniGameUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Slider gaugeSlider;
    [SerializeField] private TMP_Text infoText;

    [Header("Tuning")]
    [SerializeField] private float targetGauge = 100f;
    [SerializeField] private float gainPerPress = 6f;
    [SerializeField] private float decayPerSec = 8f;
    [SerializeField] private float timeLimitSec = 10f;

    [SerializeField] private GamePauseManager gpm;

    private float gauge;
    private float timer;
    private bool running;
    private Action<bool> _onFinished;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (gpm == null) gpm = FindAnyObjectByType<GamePauseManager>();
        if (gpm != null) gpm = GamePauseManager.Instance;
        SetActive(false);
    }

    public void Open(float gain, float decay, Action<bool> onFinished)
    {
        gainPerPress = gain;
        decayPerSec = decay;
        _onFinished = onFinished;

        gauge = 0f;
        timer = 0f;
        running = true;

        if(gaugeSlider != null)
        {
            gaugeSlider.minValue = 0f;
            gaugeSlider.maxValue = targetGauge;
            gaugeSlider.value = gauge;
        }

        if (infoText != null)
            infoText.text = "Space를 연타해";

        //SFX 재생
        if(SoundManager.Instance != null) SoundManager.Instance.PlaySfxStoppable(SfxId.Fish_MinigameReel);

        gpm?.Enter(GamePauseManager.Modal.FishingMiniGame);

        SetActive(true);
    }

    public void Close()
    {
        running = false;
        _onFinished = null;

        SetActive(false);

        gpm?.Exit(GamePauseManager.Modal.FishingMiniGame);
    }

    private void Update()
    {
        if (!running) return;

        timer += Time.unscaledDeltaTime;

        bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if (pressed)
        {
            //입력이 있는 프레임에는 감소 없음
            gauge += gainPerPress;
        }
        else
        {
            gauge -= decayPerSec * Time.unscaledDeltaTime;
        }

        gauge = Mathf.Clamp(gauge, 0f, targetGauge);

        if (gaugeSlider != null)
            gaugeSlider.value = gauge;

        //성공
        if(gauge >= targetGauge)
        {
            //Reel 정지
            if (SoundManager.Instance != null)  SoundManager.Instance.StopSfx(SfxId.Fish_MinigameReel);

            //SFX 재생
            if (SoundManager.Instance != null)  SoundManager.Instance.PlaySfx(SfxId.Fish_Success);

            Finish(true);
            return;
        }
        
        //실패
        if(timer >= timeLimitSec)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.StopSfx(SfxId.Fish_MinigameReel);

            //SFX재생
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Fish_Fail);

            Finish(false);
            return;
        }
    }

    private void Finish(bool success)
    {
        running = false;
        SetActive(false);

        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke(success);

        gpm?.Exit(GamePauseManager.Modal.FishingMiniGame);
    }

    private void SetActive(bool on)
    {
        if (root != null) root.SetActive(on);
        else gameObject.SetActive(on);
    }
}
