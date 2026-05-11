using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDSkillView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private Image cooldownMask;
    [SerializeField] private TMP_Text cooldownText;

    private Coroutine cooldownCo;

    public void SetSkillIcon(Sprite icon)
    {
        if(skillIcon != null)
        {
            skillIcon.sprite = icon;
            skillIcon.enabled = (icon != null);
        }
    }

    public void StartCooldown(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            ClearCooldownVisual();
            return;
        }

        if(cooldownCo != null) StopCoroutine(cooldownCo);
        cooldownCo = StartCoroutine(CoCooldown(durationSeconds));
    }

    public void ClearCooldownVisual()
    {
        if(cooldownCo != null)
        {
            StopCoroutine(cooldownCo);
            cooldownCo = null;
        }

        if (cooldownMask != null)
            cooldownMask.gameObject.SetActive(false);

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }

    private IEnumerator CoCooldown(float duration)
    {
        float remain = duration;

        if (cooldownMask != null) cooldownMask.gameObject.SetActive(true);
        if (cooldownText != null) cooldownText.gameObject.SetActive(true);

        while(remain > 0f)
        {
            remain -= Time.deltaTime;

            float t = Mathf.Clamp01(remain / duration);

            if (cooldownMask != null) cooldownMask.fillAmount = t;

            if(cooldownText != null)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, remain));
                cooldownText.text = sec.ToString();
            }

            yield return null;
        }

        ClearCooldownVisual();
    }
}
