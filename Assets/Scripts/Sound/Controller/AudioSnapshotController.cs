using UnityEngine;
using UnityEngine.Audio;


public class AudioSnapshotController : MonoBehaviour
{
    [Header("SnapShots")]
    [SerializeField] private AudioMixerSnapshot outdoor;
    [SerializeField] private AudioMixerSnapshot indoor;

    [Header("Tuning")]
    [SerializeField] private float transitionSec = 0.35f;


    public void Apply()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Door_Enter);
        var target = (WeatherManager.Instance._IsIndoor) ? indoor : outdoor;
        if (target != null) target.TransitionTo(transitionSec);
    }
}
