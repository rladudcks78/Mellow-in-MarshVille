using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상호작용 메뉴 정책 전용:
/// - 메뉴 버튼 구성(인사/선물/상점/퀘스트/떠나기)
/// - 인사 1일 1회
/// - 선물 가능 여부 회색처리
/// - 상점NPC/퀘스트 조건
/// </summary>
public class NPCInteractionMenuController : MonoBehaviour
{
    public event Action<int> QuestRequested;
    public event Action<int> ShopRequested;
    public event Action<int> GiftRequested;
    public event Action<int> LeftNpc;

    public event Action<string> SystemLineRequested;

    [Header("문구")]
    [SerializeField] private string playerDisplayName = "나";
    [SerializeField] private string interactionPromptLine = "무엇을 할까요?";

    [Header("회색 처리")]
    [SerializeField] private Color greyedTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("Refs")]
    [SerializeField] private GiftManager giftManager;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private GiftReactionLoader giftReactionLoader;


    [Header("입력 제어")]
    [SerializeField] private InputReader inputReader;

    private readonly Dictionary<int, int> greetedDayKeyByNpcId = new Dictionary<int, int>();

    public string PlayerDisplayName => playerDisplayName;
    public string InteractionPromptLine => interactionPromptLine;

    private bool rebuildQueued = false;

    private bool _inputBlocked = false;

    private void OnEnable()
    {
        if (giftManager == null)
            giftManager = GiftManager.Instance != null ? GiftManager.Instance : FindAnyObjectByType<GiftManager>();

        if (inventorySystem == null)
            inventorySystem = FindAnyObjectByType<InventorySystem>();

        if (giftReactionLoader == null)
            giftReactionLoader = GiftReactionLoader.Instance != null ? GiftReactionLoader.Instance : FindAnyObjectByType<GiftReactionLoader>();

        if (inputReader == null)
            inputReader = Resources.Load<InputReader>("InputReader");
    }

    public void BuildMenu(NPCDialogueView view, int npcId, bool childPopupOpen, bool setPromptText)
    {
        if (view == null) return;

        BlockPlayerInput();

        view.ClearButtons();
        view.SetChoicePanel(true);
        view.SetContinue(false, "");

        if (setPromptText)
            view.SetDialogueInstant(interactionPromptLine);

        bool greetedToday = IsGreetedToday(npcId);

        // 대화하기 버튼
        view.SpawnButton("대화하기", (btn, txt) =>
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (childPopupOpen) return;

                // 호감도 + TalkToNPC
                OnMenuGreet(view, npcId);  // 기존 호감도 로직

                // 시스템키 호출
                SystemLineRequested?.Invoke("talkstart");
                return;
            });
        });

        // 선물하기 버튼
        view.SpawnButton("선물하기", (btn, txt) =>
        {
            if (giftManager != null)
            {
                bool canGiftToday = giftManager.CanGiftToday(npcId);
                bool canGiftWeek = !giftManager.IsGiftLimitReachedThisWeek(npcId);

                btn.interactable = canGiftToday && canGiftWeek;

                txt.color = btn.interactable ? Color.black : greyedTextColor;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (childPopupOpen) return;

                    else
                    SystemLineRequested?.Invoke("giftchooseitem");
                    GiftRequested?.Invoke(npcId);
                });
            }
        });

        // 상점 버튼
        if (IsShopNpc(npcId))
        {
            view.SpawnButton("상점", (btn, txt) =>
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (childPopupOpen) return;
                    SystemLineRequested?.Invoke("shopprompt");
                    ShopRequested?.Invoke(npcId);
                });
            });
        }

        // 퀘스트 버튼
        if (HasQuestForNpc(npcId))
        {
            view.SpawnButton("퀘스트", (btn, txt) =>
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (childPopupOpen) return;
                    SystemLineRequested?.Invoke("questprompt");
                    QuestRequested?.Invoke(npcId);
                });
            });
        }


        view.SpawnButton("떠나기", (btn, txt) =>
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (childPopupOpen) return;
                LeftNpc?.Invoke(npcId);

                UnblockPlayerInput();
            });
        });
    }

    private void OnMenuGreet(NPCDialogueView view, int npcId)
    {
        if (view == null) return;

        QuestManagerCsv.Instance?.NotifyTalkedToNpc(npcId);

        if (!IsGreetedToday(npcId))
        {
            MarkGreetedToday(npcId);
            RelationshipManager.Instance?.IncreaseAffection(npcId, 1);
            RelationshipManager.Instance?.ShowAffectionUI(npcId);
        }
    }

    public bool TryGetGiftReactionLine(int npcId, GiftTaste taste, out string line)
    {
        line = "";

        if (giftReactionLoader == null)
            giftReactionLoader = GiftReactionLoader.Instance != null ? GiftReactionLoader.Instance : FindAnyObjectByType<GiftReactionLoader>();

        if (giftReactionLoader != null && giftReactionLoader.IsLoaded && giftReactionLoader.Db != null &&
            giftReactionLoader.Db.TryGetRandomLine(npcId, taste, out var custom))
        {
            line = custom;
            return true;
        }

        return false;
    }

    private bool IsShopNpc(int npcId)
    {
        var loader = NpcLoader.Instance;
        if (loader != null && loader.IsLoaded && loader.NpcDb != null &&
            loader.NpcDb.TryGet(npcId, out var def) && def != null)
            return def.isShopNpc;

        return false;
    }

    private bool HasQuestForNpc(int npcId)
    {
        var qm = QuestManagerCsv.Instance;
        if (qm == null) return false;
        if (!qm.IsReady()) return false;

        return qm.HasAnyVisibleQuestForNpc(npcId, inventorySystem);
    }

    private int GetTodayKey()
    {
        if (RelationshipManager.Instance == null) return -1;
        return RelationshipManager.Instance.CurrentDayKey;
    }

    private bool IsGreetedToday(int npcId)
    {
        if (npcId <= 0) return false;

        int todayKey = GetTodayKey();
        if (todayKey < 0) return false;

        return greetedDayKeyByNpcId.TryGetValue(npcId, out var dayKey) && dayKey == todayKey;
    }

    private void MarkGreetedToday(int npcId)
    {
        if (npcId <= 0) return;

        int todayKey = GetTodayKey();
        if (todayKey < 0) return;

        greetedDayKeyByNpcId[npcId] = todayKey;
    }

    private void RequestRebuildMenuNextFrame(NPCDialogueView view, int npcId, bool setPromptText)
    {
        if (rebuildQueued) return;
        rebuildQueued = true;
        StartCoroutine(CoRebuildMenuNextFrame(view, npcId, setPromptText));
    }

    private System.Collections.IEnumerator CoRebuildMenuNextFrame(NPCDialogueView view, int npcId, bool setPromptText)
    {
        yield return null; // 다음 프레임
        rebuildQueued = false;

        // childPopupOpen은 "메뉴 재구성" 자체에는 보통 false
        BuildMenu(view, npcId, childPopupOpen: false, setPromptText: setPromptText);
    }

    private void BlockPlayerInput()
    {
        if (inputReader != null && !_inputBlocked)
        {
            inputReader.DisableAllInput();
            _inputBlocked = true;
        }
    }

    private void UnblockPlayerInput()
    {
        if (inputReader != null && _inputBlocked)
        {
            inputReader.EnablePlayerInput();
            _inputBlocked = false;
        }
    }

}
