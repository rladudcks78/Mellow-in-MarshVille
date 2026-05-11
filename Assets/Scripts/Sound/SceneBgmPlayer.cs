using UnityEngine;

public class SceneBgmPlayer : MonoBehaviour
{
    [SerializeField] private BgmId bgmId;
    [SerializeField] private bool restartIfSame = false;

    private void Start()
    {
        if (SoundManager.Instance == null) return;
        SoundManager.Instance.PlayBgm(bgmId, restartIfSame);
    }
}
