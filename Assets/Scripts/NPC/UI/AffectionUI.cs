using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AffectionUI : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private GameObject affectionPanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private Image[] hearts; // 하트 5개

    [Header("하트 스프라이트")]
    [SerializeField] private Sprite emptyHeart;
    [SerializeField] private Sprite halfHeart;
    [SerializeField] private Sprite fullHeart;

    // 호감도 UI 표시
    public void ShowAffection(string npcName, int affection)
    {
        if (affectionPanel == null) return;

        // NPC 이름 설정
        if (npcNameText != null)
        {
            npcNameText.text = npcName;
        }

        // 하트 표시
        UpdateHearts(affection);

        // 패널 표시
        affectionPanel.SetActive(true);
    }

    // 하트 업데이트
    private void UpdateHearts(int affection)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;

            int minAffection = i * 20;
            int maxAffection = minAffection + 20;

            if (affection >= maxAffection)
            {
                hearts[i].sprite = fullHeart;
            }
            else if (affection >= minAffection + 10)
            {
                hearts[i].sprite = halfHeart;
            }
            else
            {
                hearts[i].sprite = emptyHeart;
            }
        }
    }

    // 호감도 UI 숨기기
    public void HideAffectionUI()
    {
        if (affectionPanel != null)
        {
            affectionPanel.SetActive(false);
        }
    }
}
