using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDHealthView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFill;

    public void SetHealth(int current, int max)
    {
        if (max < 0) max = 0;
        if (current < 0) current = 0;
        if (max > 0 && current > max) current = max;

        if(hpText != null)
            hpText.text = $"{current}/{max}";

        if(hpFill != null)
        {
            float t = (max <= 0) ? 0f : (current / (float)max);
            hpFill.fillAmount = Mathf.Clamp01(t);
        }
    }
}
