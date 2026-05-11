using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 UI 제어
/// - 좌측: 상점 목록 + 구매 상세
/// - 우측: 기존 InventoryUI를 함께 띄운 상태에서 판매 상세만 담당
/// - 클릭 입력은 UIInteract에서 라우팅 (ShopSlotUI는 hover만 담당)
/// </summary>
public class ShopUIController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootPanel;

    [Header("Refs")]
    [SerializeField] private ShopService shopService;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerGold playerGold;
    [SerializeField] private SpriteResolver spriteResolver;

    [Header("Shop Header")]
    [SerializeField] private TMP_Text shopTitleText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text messageText;

    [Header("Shop List")]
    [SerializeField] private Transform shopListContent;
    [SerializeField] private ShopSlotUI shopSlotPrefab;
    [SerializeField] private List<ShopSlotUI> shopSlots = new();

    [Header("Buy Detail (Left Bottom)")]
    [SerializeField] private Image buyIcon;
    [SerializeField] private TMP_Text buyNameText;
    [SerializeField] private TMP_Text buyDescText;
    [SerializeField] private TMP_Text buyPriceText;   // 단가만 표시 (수량 곱 X)
    [SerializeField] private TMP_Text buyQtyText;

    [SerializeField] private Button buyLeftArrowButton;
    [SerializeField] private Button buyRightArrowButton;
    [SerializeField] private Button buyButton;

    [Header("Sell Detail (Right Bottom)")]
    [SerializeField] private Image sellIcon;
    [SerializeField] private TMP_Text sellNameText;
    [SerializeField] private TMP_Text sellDescText;
    [SerializeField] private TMP_Text sellPriceText;  // 단가만 표시 (수량 곱 X)
    [SerializeField] private TMP_Text sellQtyText;

    [SerializeField] private Button sellLeftArrowButton;
    [SerializeField] private Button sellRightArrowButton;
    [SerializeField] private Button sellButton;

    // UIInteract가 참조하는 상태값
    public bool isOpen { get; private set; }
    public int hoveredShopIndex { get; private set; } = -1;

    private Vector2 lastPointerPos;

    private int currentNpcId = -1;
    private ShopService.ShopOpenContext currentOpenContext;
    private readonly List<ShopService.ShopDisplayItem> currentDisplayItems = new();

    // 구매 선택 상태
    private int selectedBuyIndex = -1;
    private int selectedBuyQty = 0;

    // 판매 선택 상태 (인벤토리의 "클릭한 슬롯" + 같은 itemId 전체 합 기준)
    private int selectedSellSlotIndex = -1;
    private int selectedSellItemId = -1;
    private int selectedSellQty = 0;
    private int selectedSellMaxTotal = 0;

    private void Awake()
    {
        if (shopService == null) shopService = FindAnyObjectByType<ShopService>();
        if (inventorySystem == null) inventorySystem = FindAnyObjectByType<InventorySystem>();
        if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (playerGold == null) playerGold = FindAnyObjectByType<PlayerGold>();
        if (spriteResolver == null) spriteResolver = FindAnyObjectByType<SpriteResolver>();

        if (rootPanel != null)
            rootPanel.SetActive(false);

        hoveredShopIndex = -1;
        isOpen = false;
    }

    private void OnEnable()
    {
        BindButtons(true);

        if (playerGold != null)
            playerGold.OnGoldChanged += HandleGoldChanged;
    }

    private void OnDisable()
    {
        BindButtons(false);

        if (playerGold != null)
            playerGold.OnGoldChanged -= HandleGoldChanged;
    }

    private void BindButtons(bool bind)
    {
        if (buyLeftArrowButton != null)
        {
            if (bind) buyLeftArrowButton.onClick.AddListener(OnClickBuyLeft);
            else buyLeftArrowButton.onClick.RemoveListener(OnClickBuyLeft);
        }

        if (buyRightArrowButton != null)
        {
            if (bind) buyRightArrowButton.onClick.AddListener(OnClickBuyRight);
            else buyRightArrowButton.onClick.RemoveListener(OnClickBuyRight);
        }

        if (buyButton != null)
        {
            if (bind) buyButton.onClick.AddListener(OnClickBuy);
            else buyButton.onClick.RemoveListener(OnClickBuy);
        }

        if (sellLeftArrowButton != null)
        {
            if (bind) sellLeftArrowButton.onClick.AddListener(OnClickSellLeft);
            else sellLeftArrowButton.onClick.RemoveListener(OnClickSellLeft);
        }

        if (sellRightArrowButton != null)
        {
            if (bind) sellRightArrowButton.onClick.AddListener(OnClickSellRight);
            else sellRightArrowButton.onClick.RemoveListener(OnClickSellRight);
        }

        if (sellButton != null)
        {
            if (bind) sellButton.onClick.AddListener(OnClickSell);
            else sellButton.onClick.RemoveListener(OnClickSell);
        }
    }

    // ------------------------------------------------------------
    // Open / Close
    // ------------------------------------------------------------

    public bool OpenForNpc(int npcId)
    {
        currentNpcId = npcId;

        if (shopService == null)
        {
            SetMessage("ShopService 참조 없음");
            return false;
        }

        if (!shopService.TryBuildOpenContextByNpc(npcId, out currentOpenContext, out string failReason))
        {
            SetMessage(failReason);
            return false;
        }

        currentDisplayItems.Clear();
        if (currentOpenContext.visibleItems != null)
            currentDisplayItems.AddRange(currentOpenContext.visibleItems);

        BuildOrRefreshShopSlotList();

        if (shopTitleText != null)
            shopTitleText.text = string.IsNullOrWhiteSpace(currentOpenContext.shopDef.shopName)
                ? "상점"
                : currentOpenContext.shopDef.shopName;

        RefreshGoldText();

        ResetBuyDetailPanel();
        ResetSellDetailPanel();
        SetMessage(string.Empty);

        if (rootPanel != null) rootPanel.SetActive(true);
        isOpen = true;
        hoveredShopIndex = -1;

        return true;
    }

    /// <summary>
    /// inspector 버튼에서 쓰기 편한 alias
    /// </summary>
    public void OpenForNpc_InspectorInt(int npcId)
    {
        OpenForNpc(npcId);
    }

    public void CloseShop()
    {
        isOpen = false;
        hoveredShopIndex = -1;

        currentNpcId = -1;
        currentOpenContext = null;
        currentDisplayItems.Clear();

        selectedBuyIndex = -1;
        selectedBuyQty = 0;

        selectedSellSlotIndex = -1;
        selectedSellItemId = -1;
        selectedSellQty = 0;
        selectedSellMaxTotal = 0;

        if (rootPanel != null) rootPanel.SetActive(false);

        ResetBuyDetailPanel();
        ResetSellDetailPanel();
        SetMessage(string.Empty);
    }

    // ------------------------------------------------------------
    // UIInteract Hook (pointer / click)
    // ------------------------------------------------------------

    public void SetPointerPos(Vector2 screenPos)
    {
        lastPointerPos = screenPos;
        // 현재는 hover index 추적이 ShopSlotUI pointer enter/exit로 충분해서 별도 hit-test 없음
    }

    public void OnShopSlotHoverEnter(int slotIndex)
    {
        hoveredShopIndex = slotIndex;
    }

    public void OnShopSlotHoverExit(int slotIndex)
    {
        if (hoveredShopIndex == slotIndex)
            hoveredShopIndex = -1;
    }

    /// <summary>
    /// UIInteract -> 상점 리스트 좌클릭
    /// </summary>
    public void OnShopSlotLeftClick(int slotIndex)
    {
        if (!isOpen) return;
        if (!TryGetDisplayItem(slotIndex, out var item)) return;

        selectedBuyIndex = slotIndex;

        int min = Mathf.Max(1, item.minSelectableAmount);
        int max = Mathf.Max(1, item.maxSelectableAmount);

        // 좌클릭 시 기본 수량 1 (요청사항)
        selectedBuyQty = Mathf.Clamp(1, min, max);

        RefreshBuyDetailPanel();
        SetMessage(string.Empty);
    }

    /// <summary>
    /// UIInteract -> 상점 리스트 우클릭 (즉시 1개 구매)
    /// </summary>
    public void OnShopSlotRightClick(int slotIndex)
    {
        if (!isOpen) return;
        if (!TryGetDisplayItem(slotIndex, out var item)) return;

        if (shopService == null)
        {
            SetMessage("ShopService 참조 없음");
            return;
        }

        if (!shopService.TryBuyItem(inventorySystem, playerGold, item, 1, out string failReason))
        {
            SetMessage(failReason);
            return;
        }

        RefreshGoldText();

        // 우클릭 후 하단 패널 초기화 (요청사항)
        ResetBuyDetailPanel();
        SetMessage(string.Empty);
    }

    /// <summary>
    /// UIInteract -> 인벤토리 좌클릭 (판매 선택)
    /// </summary>
    public void OnInventorySlotLeftClickFromUIInteract(int slotIndex)
    {
        if (!isOpen) return;
        if (inventorySystem == null || !inventorySystem.IsReady) return;

        // ContainerGrid는 Count가 아니라 slotCount
        if (slotIndex < 0 || slotIndex >= inventorySystem.Inventory.slotCount)
        {
            ResetSellDetailPanel();
            return;
        }

        var stack = inventorySystem.Inventory.Get(slotIndex);
        if (stack.IsEmpty || stack.amount <= 0)
        {
            ResetSellDetailPanel();
            return;
        }

        if (!inventorySystem.TryGetDef(stack.itemId, out var itemDef) || itemDef == null)
        {
            ResetSellDetailPanel();
            return;
        }

        int totalOwned = inventorySystem.CountItem(stack.itemId);
        if (totalOwned <= 0)
        {
            ResetSellDetailPanel();
            return;
        }

        selectedSellSlotIndex = slotIndex;
        selectedSellItemId = stack.itemId;
        selectedSellMaxTotal = totalOwned;

        // 좌클릭 시 기본 수량 1
        selectedSellQty = 1;

        RefreshSellDetailPanel();
        SetMessage(string.Empty);
    }

    /// <summary>
    /// UIInteract -> 인벤토리 우클릭 (즉시 1개 판매, 클릭한 슬롯 우선)
    /// </summary>
    public void OnInventorySlotRightClickFromUIInteract(int slotIndex)
    {
        if (!isOpen) return;
        if (shopService == null) return;

        if (!shopService.TryQuickSellFromInventorySlot(inventorySystem, playerGold, slotIndex, out string failReason))
        {
            SetMessage(failReason);
            return;
        }

        RefreshGoldText();

        // 우클릭 후 하단 판매 패널 초기화 (요청사항)
        ResetSellDetailPanel();
        SetMessage(string.Empty);
    }

    // ------------------------------------------------------------
    // Buy Button / Arrow
    // ------------------------------------------------------------

    private void OnClickBuyLeft()
    {
        if (!isOpen) return;
        if (!TryGetDisplayItem(selectedBuyIndex, out var item)) return;

        int min = Mathf.Max(1, item.minSelectableAmount);
        int max = Mathf.Max(1, item.maxSelectableAmount);

        if (max <= 1)
        {
            selectedBuyQty = 1;
        }
        else
        {
            // 1에서 좌측 누르면 max로 래핑
            if (selectedBuyQty <= min) selectedBuyQty = max;
            else selectedBuyQty--;
        }

        RefreshBuyDetailPanel();
    }

    private void OnClickBuyRight()
    {
        if (!isOpen) return;
        if (!TryGetDisplayItem(selectedBuyIndex, out var item)) return;

        int min = Mathf.Max(1, item.minSelectableAmount);
        int max = Mathf.Max(1, item.maxSelectableAmount);

        if (max <= 1)
        {
            selectedBuyQty = 1;
        }
        else
        {
            // max에서 우측 누르면 1로 래핑
            if (selectedBuyQty >= max) selectedBuyQty = min;
            else selectedBuyQty++;
        }

        RefreshBuyDetailPanel();
    }

    private void OnClickBuy()
    {
        if (!isOpen) return;
        if (shopService == null) return;
        if (!TryGetDisplayItem(selectedBuyIndex, out var item))
        {
            SetMessage("구매할 아이템을 선택하세요.");
            return;
        }

        int qty = Mathf.Clamp(selectedBuyQty, 1, Mathf.Max(1, item.maxSelectableAmount));

        if (!shopService.TryBuyItem(inventorySystem, playerGold, item, qty, out string failReason))
        {
            SetMessage(failReason);
            return;
        }

        RefreshGoldText();

        // 구매 버튼 후 하단 구매 패널 초기화 + 수량 0 리셋 (요청사항)
        ResetBuyDetailPanel();
        SetMessage(string.Empty);
    }

    // ------------------------------------------------------------
    // Sell Button / Arrow
    // ------------------------------------------------------------

    private void OnClickSellLeft()
    {
        if (!isOpen) return;
        if (!HasSellSelection()) return;

        if (selectedSellMaxTotal <= 1)
        {
            selectedSellQty = Mathf.Clamp(selectedSellMaxTotal, 1, 1);
        }
        else
        {
            // 1에서 좌측 누르면 max(totalOwned)로 래핑
            if (selectedSellQty <= 1) selectedSellQty = selectedSellMaxTotal;
            else selectedSellQty--;
        }

        RefreshSellDetailPanel();
    }

    private void OnClickSellRight()
    {
        if (!isOpen) return;
        if (!HasSellSelection()) return;

        if (selectedSellMaxTotal <= 1)
        {
            selectedSellQty = 1;
        }
        else
        {
            // max(totalOwned)에서 우측 누르면 1로 래핑
            if (selectedSellQty >= selectedSellMaxTotal) selectedSellQty = 1;
            else selectedSellQty++;
        }

        RefreshSellDetailPanel();
    }

    private void OnClickSell()
    {
        if (!isOpen) return;
        if (!HasSellSelection())
        {
            SetMessage("판매할 아이템을 선택하세요.");
            return;
        }

        if (shopService == null)
        {
            SetMessage("ShopService 참조 없음");
            return;
        }

        int qty = Mathf.Clamp(selectedSellQty, 1, Mathf.Max(1, selectedSellMaxTotal));

        if (!shopService.TrySellSelected(inventorySystem, playerGold, selectedSellItemId, qty, out string failReason))
        {
            SetMessage(failReason);
            return;
        }

        RefreshGoldText();

        // 판매 버튼 후 하단 판매 패널 초기화 + 수량 0 리셋 (요청사항)
        ResetSellDetailPanel();
        SetMessage(string.Empty);
    }

    // ------------------------------------------------------------
    // Refresh UI
    // ------------------------------------------------------------

    private void BuildOrRefreshShopSlotList()
    {
        EnsureSlotPool(currentDisplayItems.Count);

        for (int i = 0; i < shopSlots.Count; i++)
        {
            bool active = i < currentDisplayItems.Count;
            if (shopSlots[i] != null)
                shopSlots[i].gameObject.SetActive(active);

            if (!active) continue;

            var view = currentDisplayItems[i];

            Sprite icon = ResolveSpriteFromItem(view.itemDef, view.spritePath);
            shopSlots[i].Init(this, i);
            shopSlots[i].Bind(view, icon);
            shopSlots[i].SetSelected(i == selectedBuyIndex);
        }
    }

    private void EnsureSlotPool(int neededCount)
    {
        if (shopSlotPrefab == null || shopListContent == null) return;

        while (shopSlots.Count < neededCount)
        {
            var slot = Instantiate(shopSlotPrefab, shopListContent);
            slot.Init(this, shopSlots.Count);
            shopSlots.Add(slot);
        }
    }

    private void RefreshBuyDetailPanel()
    {
        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null && shopSlots[i].gameObject.activeSelf)
                shopSlots[i].SetSelected(i == selectedBuyIndex);
        }

        if (!TryGetDisplayItem(selectedBuyIndex, out var item))
        {
            ResetBuyDetailPanel();
            return;
        }

        selectedBuyQty = Mathf.Clamp(selectedBuyQty, 1, Mathf.Max(1, item.maxSelectableAmount));

        SetImage(buyIcon, ResolveSpriteFromItem(item.itemDef, item.spritePath));
        SetText(buyNameText, item.itemName);
        SetText(buyDescText, item.description);
        SetText(buyPriceText, item.unitPrice.ToString()); // 단가만 표시
        SetText(buyQtyText, selectedBuyQty.ToString());

        bool canAdjust = item.maxSelectableAmount > 1;
        SetButtonInteractable(buyLeftArrowButton, true);
        SetButtonInteractable(buyRightArrowButton, true);
        SetButtonInteractable(buyButton, true);

        // 장비/무기 max=1이어도 버튼 눌러도 1 유지되게 해달라고 했으니 interactable은 true 유지
        // 필요하면 canAdjust로 false 처리 가능
    }

    private void RefreshSellDetailPanel()
    {
        if (!HasSellSelection())
        {
            ResetSellDetailPanel();
            return;
        }

        if (inventorySystem == null || !inventorySystem.IsReady)
        {
            ResetSellDetailPanel();
            return;
        }

        // 선택된 itemId 기준 현재 총량 다시 계산 (구매/판매 직후 갱신 대응)
        selectedSellMaxTotal = inventorySystem.CountItem(selectedSellItemId);
        if (selectedSellMaxTotal <= 0)
        {
            ResetSellDetailPanel();
            return;
        }

        selectedSellQty = Mathf.Clamp(selectedSellQty, 1, selectedSellMaxTotal);

        if (!inventorySystem.TryGetDef(selectedSellItemId, out var itemDef) || itemDef == null)
        {
            ResetSellDetailPanel();
            return;
        }

        int unitSellPrice = shopService != null
            ? shopService.GetSellUnitPrice(inventorySystem, selectedSellItemId)
            : Mathf.Max(0, itemDef.sellPrice);

        SetImage(sellIcon, ResolveSpriteFromItem(itemDef, itemDef.spritePath));
        SetText(sellNameText, itemDef.name);
        SetText(sellDescText, itemDef.description);
        SetText(sellPriceText, unitSellPrice.ToString()); // 단가만 표시
        SetText(sellQtyText, selectedSellQty.ToString());

        SetButtonInteractable(sellLeftArrowButton, true);
        SetButtonInteractable(sellRightArrowButton, true);
        SetButtonInteractable(sellButton, unitSellPrice > 0);
    }

    private void ResetBuyDetailPanel()
    {
        selectedBuyIndex = -1;
        selectedBuyQty = 0;

        SetImage(buyIcon, null);
        SetText(buyNameText, "");
        SetText(buyDescText, "");
        SetText(buyPriceText, "");
        SetText(buyQtyText, "0");

        for (int i = 0; i < shopSlots.Count; i++)
        {
            if (shopSlots[i] != null && shopSlots[i].gameObject.activeSelf)
                shopSlots[i].SetSelected(false);
        }

        SetButtonInteractable(buyButton, false);
    }

    private void ResetSellDetailPanel()
    {
        selectedSellSlotIndex = -1;
        selectedSellItemId = -1;
        selectedSellQty = 0;
        selectedSellMaxTotal = 0;

        SetImage(sellIcon, null);
        SetText(sellNameText, "");
        SetText(sellDescText, "");
        SetText(sellPriceText, "");
        SetText(sellQtyText, "0");

        SetButtonInteractable(sellButton, false);
    }

    private void RefreshGoldText()
    {
        if (goldText == null) return;

        long gold = playerGold != null ? playerGold.CurrentGold : 0;
        goldText.text = gold.ToString();
    }

    private void HandleGoldChanged(long _)
    {
        RefreshGoldText();
    }

    // ------------------------------------------------------------
    // Utility
    // ------------------------------------------------------------

    private bool TryGetDisplayItem(int index, out ShopService.ShopDisplayItem item)
    {
        item = null;
        if (index < 0 || index >= currentDisplayItems.Count) return false;
        item = currentDisplayItems[index];
        return item != null;
    }

    private bool HasSellSelection()
    {
        return selectedSellItemId > 0 && selectedSellQty > 0;
    }

    private Sprite ResolveSpriteFromItem(ItemDef itemDef, string spritePath)
    {
        // 1) ItemDef.spritePath 우선
        string path = string.IsNullOrWhiteSpace(spritePath) ? itemDef?.spritePath : spritePath;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        // 2) 프로젝트 SpriteResolver 사용 (Get 아님, Load)
        if (spriteResolver == null) spriteResolver = FindAnyObjectByType<SpriteResolver>();
        if (spriteResolver != null)
        {
            var s = spriteResolver.Load(path);
            if (s != null) return s;
        }

        // 3) 마지막 fallback
        var res = Resources.Load<Sprite>(path);
        if (res != null) return res;

        return null;
    }

    private void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value ?? "";
    }

    private void SetImage(Image img, Sprite sprite)
    {
        if (img == null) return;

        img.sprite = sprite;
        img.enabled = (sprite != null);

        // 아이콘이 없어도 프레임은 보이고 싶으면 enabled 대신 color alpha만 조절하면 됨.
    }

    private void SetButtonInteractable(Button button, bool value)
    {
        if (button != null) button.interactable = value;
    }

    private void SetMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg ?? "";
    }
}