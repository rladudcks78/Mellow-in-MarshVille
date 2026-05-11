using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UISoundController : MonoBehaviour
{
    [SerializeField] private SfxId clickSfx = SfxId.UI_Click;

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
        if (btn == null) btn = GetComponentInChildren<Button>();
    }

    private void OnEnable()
    {
        if (btn == null) return;

        //중복 등록 방지
        btn.onClick.RemoveListener(PlayClickSfx);
        btn.onClick.AddListener(PlayClickSfx);
    }

    private void OnDisable()
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(PlayClickSfx);
    }


    private void PlayClickSfx()
    {
        if (clickSfx == SfxId.None) return;
        if (SoundManager.Instance == null) return;

        SoundManager.Instance.PlaySfx(clickSfx);
    }
}
