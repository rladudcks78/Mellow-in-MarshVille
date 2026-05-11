using TMPro;
using UnityEngine;

public class HUDCurrencyView : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;

    public void SetGold(int gold)
    {
        if (gold < 0) gold = 0;
        if (goldText != null)
            goldText.text = gold.ToString("N0");
    }
}
