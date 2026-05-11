using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화 선택지 버튼
/// </summary>
public class ChoiceButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonText;

    private int nodeId;
    private System.Action<int> onClickCallback;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    /// <summary>
    /// 선택지 설정
    /// </summary>
    public void Setup(string text, int targetNodeId, System.Action<int> callback)
    {
        if (buttonText != null)
            buttonText.text = text;

        nodeId = targetNodeId;
        onClickCallback = callback;
    }

    private void OnButtonClicked()
    {
        onClickCallback?.Invoke(nodeId);
    }
}

