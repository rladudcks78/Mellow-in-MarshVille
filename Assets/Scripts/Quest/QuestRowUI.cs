using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 목록의 한 줄(Row) UI.
/// - questId를 클릭 콜백으로 전달.
/// </summary>
public class QuestRowUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statusText;

    private int questId;
    private System.Action<int> onClicked;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() =>
            {
                if (questId > 0) onClicked?.Invoke(questId);
            });
    }

    public void Setup(string title, string status, int questId, System.Action<int> onClicked)
    {
        this.questId = questId;
        this.onClicked = onClicked;

        if (titleText != null) titleText.text = title;
        if (statusText != null) statusText.text = status;

        // questId가 -1이면 정보행이므로 클릭 비활성
        if (button != null) button.interactable = questId > 0;
    }
}
