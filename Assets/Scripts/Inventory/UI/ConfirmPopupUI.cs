using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirm;
    private Action onCancel;

    public bool isOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    public void Show(string message, Action confirm, Action cancel)
    {
        //사운드 실행
        SoundManager.Instance.PlaySfx(SfxId.UI_Confirm);

        onConfirm = confirm;
        onCancel = cancel;

        if (messageText != null)
            messageText.text = message;

        root.SetActive(true);
    }

    public void Confirm()
    {
        if (!isOpen) return;

        try
        {
            onConfirm?.Invoke();
        }

        finally
        {
            Hide();
        }
    }

    public void Cancel()
    {
        if (!isOpen) return;

        try
        {
            onCancel?.Invoke();
        }
        finally
        {
            Hide();
        }
    }

    public void Hide()
    {
        onConfirm = null;
        onCancel = null;
        root.SetActive(false);
    }
}
