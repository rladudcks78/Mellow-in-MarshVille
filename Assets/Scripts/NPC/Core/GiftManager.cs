using System;
using System.Collections.Generic;
using UnityEngine;

public class GiftManager : MonoBehaviour
{
    public static GiftManager Instance;

    public event Action<int> GiftStateChanged; // npcId

    [Header("Refs")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private GiftTasteLoader giftTasteLoader;
    [SerializeField] private NPCDialogueUI dialogueUI;
    [SerializeField] private GiftInventoryController giftInventoryController;

    [Header("기획값")]
    [SerializeField] private int maxGiftsPerWeek = 3;

    private GiftTasteDatabase giftTasteDb;

    private readonly Dictionary<int, int> giftCountThisWeekByNpc = new Dictionary<int, int>();
    private readonly Dictionary<int, int> giftedDayKeyByNpc = new Dictionary<int, int>(); // key: npcId, value: dayKey
    private Dictionary<int, int> giftCountByWeek = new Dictionary<int, int>();


    private void OnEnable()
    {
        if (dialogueUI != null)
            dialogueUI.GiftRequested += BeginGiftSessionForNpc;
    }

    private void OnDisable()
    {
        if (dialogueUI != null)
            dialogueUI.GiftRequested -= BeginGiftSessionForNpc;
    }

    private int GetTodayKey()
    {
        if (RelationshipManager.Instance == null)
        {
            Debug.LogWarning("[GiftManager] RelationshipManager.Instance가 null입니다. 날짜 체크 불가.");
            return -1;
        }

        int dayKey = RelationshipManager.Instance.CurrentDayKey;
        if (dayKey < 0)
        {
            Debug.LogWarning("[GiftManager] 유효하지 않은 dayKey: " + dayKey);
            return -1;
        }

        return dayKey;
    }


    public bool HasGiftedToday(int npcId)
    {
        if (npcId <= 0) return false;

        int todayKey = GetTodayKey();
        if (todayKey < 0) return false;

        return giftedDayKeyByNpc.TryGetValue(npcId, out var dayKey) && dayKey == todayKey;
    }

    public bool CanGiftToday(int npcId) => !HasGiftedToday(npcId);

    private void MarkGiftedToday(int npcId)
    {
        int todayKey = GetTodayKey();
        if (todayKey < 0) return;

        giftedDayKeyByNpc[npcId] = todayKey;
        GiftStateChanged?.Invoke(npcId);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (inventorySystem == null) inventorySystem = FindAnyObjectByType<InventorySystem>();
        if (giftTasteLoader == null) giftTasteLoader = FindAnyObjectByType<GiftTasteLoader>();
        if (dialogueUI == null) dialogueUI = FindAnyObjectByType<NPCDialogueUI>();
        if (giftInventoryController == null) giftInventoryController = FindAnyObjectByType<GiftInventoryController>();

        if (giftTasteLoader != null)
            giftTasteLoader.Register(db => giftTasteDb = db, err => giftTasteDb = null);
    }

    public void BeginGiftSessionForNpc(int npcId)
    {
        if (npcId <= 0) return;

        if (inventorySystem == null || !inventorySystem.IsReady)
        {
            dialogueUI?.ShowSystemLine("인벤토리가 준비되지 않았습니다.");
            return;
        }

        int todayKey = GetTodayKey();

        if (todayKey < 0)
        {
            dialogueUI?.ShowSystemLine("날짜 시스템 오류로 선물 불가합니다.");
            return;
        }

        if (giftInventoryController == null)
        {
            dialogueUI?.ShowSystemLine("선물 UI를 찾을 수 없습니다.");
            return;
        }

        if (HasGiftedToday(npcId))
        {
            dialogueUI?.ShowSystemLine("오늘은 이미 선물했습니다.");
            return;
        }

        if (IsGiftLimitReachedThisWeek(npcId))
        {
            dialogueUI?.ShowSystemLine("이번 주 선물 횟수를 초과했습니다.");
            return;
        }

        giftInventoryController.BeginGiftSession(npcId);
    }


    public bool TryGiftFromInventorySlot(int npcId, int slotIndex)
    {
        if (npcId <= 0) return false;
        if (inventorySystem == null || !inventorySystem.IsReady) return false;
        if (slotIndex < 0) return false;

        if (HasGiftedToday(npcId))
        {
            dialogueUI?.ShowSystemLine("오늘은 이미 선물을 줬어요.");
            return false;
        }

        var stack = inventorySystem.Inventory.Get(slotIndex);
        if (stack.IsEmpty)
        {
            dialogueUI?.ShowSystemLine("빈 슬롯이에요.");
            return false;
        }

        int itemId = stack.itemId;

        if (!IsGiftableByIdRule(itemId))
        {
            dialogueUI?.ShowSystemLine("이 아이템은 선물할 수 없어요.");
            return false;
        }

        if (!inventorySystem.RemoveItem(itemId, 1))
        {
            dialogueUI?.ShowSystemLine("아이템을 꺼낼 수 없어요.");
            return false;
        }

        MarkGiftedToday(npcId);

        GiftTaste taste = GiftTaste.Soso;
        int delta = 1;

        if (giftTasteDb != null && giftTasteDb.TryGet(npcId, itemId, out var def) && def != null)
        {
            taste = def.taste;
            delta = def.affectionDelta;
        }

        ApplyAffectionDelta(npcId, delta);

        int todayKey = GetTodayKey();
        int weekKey = todayKey / 7;
        int npcWeekKey = npcId * 1000 + weekKey;

        giftCountThisWeekByNpc[npcId] = GetGiftCountThisWeek(npcId) + 1;
        giftCountByWeek[npcWeekKey] = GetGiftCountThisWeek(npcId) + 1;

        dialogueUI?.ShowGiftResultLine(npcId, taste, delta);
        RelationshipManager.Instance?.ShowAffectionUI(npcId);

        return true;
    }

    private bool IsGiftableByIdRule(int itemId)
    {
        int group = itemId / 10000;

        if (group == 0)
        {
            int k = itemId / 1000;
            if (k == 1) group = 1;
            else if (k == 2) group = 2;
            else if (k == 3) group = 3;
            else if (k == 5) group = 5;
        }

        return group == 1 || group == 2 || group == 3 || group == 5;
    }

    private void ApplyAffectionDelta(int npcId, int delta)
    {
        if (RelationshipManager.Instance == null) return;

        if (delta > 0) RelationshipManager.Instance.IncreaseAffection(npcId, delta);
        else if (delta < 0) RelationshipManager.Instance.DecreaseAffection(npcId, -delta);
    }

    private int GetGiftCountThisWeek(int npcId)
    {
        int todayKey = GetTodayKey();
        if (todayKey < 0) return 0;

        int weekKey = todayKey / 7;
        int npcWeekKey = npcId * 1000 + weekKey;

        return giftCountByWeek.TryGetValue(npcWeekKey, out var count) ? count : 0;
    }


    public bool IsGiftLimitReachedThisWeek(int npcId)
    {
        return GetGiftCountThisWeek(npcId) >= maxGiftsPerWeek;
    }

}
