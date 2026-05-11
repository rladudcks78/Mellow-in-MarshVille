using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCDialogueView : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("닫기 판정용(창 영역)")]
    [SerializeField] private RectTransform windowRect;

    [Header("초상화")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite defaultPortrait;

    [Header("대사 영역")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("선택지 영역")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Transform choiceButtonContainer;
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("계속 버튼")]
    [SerializeField] private GameObject continueButton;
    [SerializeField] private TextMeshProUGUI continueButtonText;

    [Header("타이핑")]
    [SerializeField] private bool useTypingEffect = true;
    [SerializeField] private float textSpeed = 0.03f;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private Coroutine typingCoroutine;

    private bool isTyping;
    private string typingFullText = "";
    private int typingVisibleCount = 0;

    public RectTransform WindowRect => windowRect;
    public bool IsTyping => isTyping;
    public bool UseTypingEffect => useTypingEffect;

    public event Action TypingCompleted;

    public void Open()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
    }

    public void CloseAndClear()
    {
        StopTyping();
        ClearButtons();

        SetChoicePanel(false);
        SetContinue(false, "");

        SetSpeakerName("");
        SetDialogueInstant("");

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    public void SetChoicePanel(bool on)
    {
        if (choicePanel != null) choicePanel.SetActive(on);
    }

    public void SetContinue(bool on, string label)
    {
        if (continueButton != null) continueButton.SetActive(on);
        if (continueButtonText != null) continueButtonText.text = label ?? "";
    }

    public void SetSpeakerName(string name)
    {
        if (speakerNameText != null) speakerNameText.text = name ?? "";
    }

    public void SetPortrait(Sprite sprite)
    {
        if (portraitImage == null) return;
        portraitImage.sprite = (sprite != null) ? sprite : defaultPortrait;
    }

    public void SetDialogueInstant(string text)
    {
        StopTyping();

        typingFullText = text ?? "";
        typingVisibleCount = typingFullText.Length;

        if (dialogueText != null)
        {
            dialogueText.text = typingFullText;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    public void PlayDialogue(string text)
    {
        if (!useTypingEffect)
        {
            SetDialogueInstant(text);
            TypingCompleted?.Invoke();
            return;
        }

        StartTyping(text);
    }

    public void SkipTyping()
    {
        if (!isTyping) return;

        StopTyping();

        if (dialogueText != null)
        {
            dialogueText.text = typingFullText;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        TypingCompleted?.Invoke();
    }

    public void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
                Destroy(spawnedButtons[i]);
        }
        spawnedButtons.Clear();
    }

    public void SpawnButton(string label, Action<Button, TextMeshProUGUI> configure)
    {
        if (choiceButtonPrefab == null || choiceButtonContainer == null)
        {
            Debug.LogWarning("[NPCDialogueView] choiceButtonPrefab 또는 container가 연결되지 않았습니다.");
            return;
        }

        var go = Instantiate(choiceButtonPrefab, choiceButtonContainer);
        go.SetActive(true);
        spawnedButtons.Add(go);

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.GetComponentInChildren<Button>();

        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = label ?? "";

        if (btn == null)
        {
            Debug.LogWarning("[NPCDialogueView] choiceButtonPrefab에 Button 컴포넌트가 없습니다.");
            return;
        }

        configure?.Invoke(btn, txt);
    }

    private void StartTyping(string fullText)
    {
        StopTyping();

        typingFullText = fullText ?? "";
        typingVisibleCount = 0;

        if (dialogueText != null)
        {
            dialogueText.text = typingFullText;
            dialogueText.maxVisibleCharacters = 0;
        }

        typingCoroutine = StartCoroutine(CoType());
    }

    private IEnumerator CoType()
    {
        isTyping = true;

        if (string.IsNullOrEmpty(typingFullText))
        {
            isTyping = false;
            TypingCompleted?.Invoke();
            yield break;
        }

        int total = typingFullText.Length;

        while (typingVisibleCount < total)
        {
            typingVisibleCount++;

            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = typingVisibleCount;

            yield return new WaitForSeconds(textSpeed);
        }

        isTyping = false;
        TypingCompleted?.Invoke();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        isTyping = false;
    }
}
