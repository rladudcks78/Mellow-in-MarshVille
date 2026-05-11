using UnityEngine;
using UnityEngine.UI;

public class OptionsAudioUI : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private bool ignore;

    private void OnEnable()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;

        //현재 저장값/ 로드값을 UI에 반영
        ignore = true;
        if (masterSlider != null) masterSlider.value = sm.Master01;
        if (bgmSlider != null) bgmSlider.value = sm.Bgm01;
        if (sfxSlider != null) sfxSlider.value = sm.Sfx01;
        ignore = false;

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMaster);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgm);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfx);
    }

    private void OnDisable()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMaster);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgm);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfx);
    }

    private void OnMaster(float v)
    {
        if (ignore) return;
        SoundManager.Instance?.SetMaster01(v);
    }

    private void OnBgm(float v)
    {
        if (ignore) return;
        SoundManager.Instance?.SetBgm01(v);
    }

    private void OnSfx(float v)
    {
        if (ignore) return;
        SoundManager.Instance?.SetSfx01(v);
    }
}
