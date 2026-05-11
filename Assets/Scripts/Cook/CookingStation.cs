using System.Collections.Generic;
using UnityEngine;

// [기획 반영] 도마 슬롯에는 수량 정보 없이 ItemId만 저장합니다.
[System.Serializable]
public class BoardSlotData
{
    public int ItemId;
    public BoardSlotData(int id) { ItemId = id; }
}

public class CookingStation : MonoBehaviour
{
    [Header("Data & Logic")]
    [SerializeField] private RecipeLoader recipeLoader;
   // [SerializeField] private PlayerData playerData;

    [Header("UI Connections")]
    [SerializeField] private CookingUI cookingUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private StorageUI storageUI;
    [SerializeField] private CookingMiniGame miniGame;

    [Header("Settings")]
    [SerializeField] private int failureItemId = 5999;

    private InventorySystem inventorySystem;
    private StorageSystem storageSystem;
    private SharedHeldSystem sharedHeld;
    private UIInteract uiInteract;

    // 도마 데이터 (고정 크기 5)
    private BoardSlotData[] _boardSlots = new BoardSlotData[5];
    public RecipeDef MatchedRecipe { get; private set; }

    // [기획 반영] 만들 개수 (기본 1)
    private int _targetCookCount = 1;
    public int TargetCookCount => _targetCookCount;

    private void Start()
    {
        inventorySystem = FindFirstObjectByType<InventorySystem>();
        storageSystem = FindFirstObjectByType<StorageSystem>();
        sharedHeld = FindFirstObjectByType<SharedHeldSystem>();
        uiInteract = FindFirstObjectByType<UIInteract>();

        // UI 자동 연결
        if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (storageUI == null) storageUI = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);

        if (cookingUI != null) cookingUI.Init(this);

        if (miniGame != null) miniGame.gameObject.SetActive(false);

        if (cookingUI == null) Debug.LogError($"{gameObject.name}: 요리 UI 연결 실패!");
        else Debug.Log($"{gameObject.name}: 요리 UI 연결 성공: {cookingUI.name}");
    }

    public void Interact()
    {
        Debug.Log("조리대 클릭됨!");
        if (uiInteract != null) uiInteract.OpenCooking(this);
    }

    // --------------------------------------------------------------------------
    // 1. 초기화 및 설정 (UIInteract에서 호출)
    // --------------------------------------------------------------------------
    public void OpenCookingModeInternal()
    {
        ShowMiniGameUI();

        // [기획 반영] 열릴 때 도마 초기화 (반환 없음)
        for (int i = 0; i < _boardSlots.Length; i++) _boardSlots[i] = null;

        // 수량 1로 초기화
        _targetCookCount = 1;

        UpdateCookingState();
    }

    public void RequestClose()
    {
        if (uiInteract != null) uiInteract.CloseCooking();
        else CloseCooking();
    }

    public void CloseCooking()
    {
        if (miniGame != null) miniGame.ForceStop();

        // [기획 반영] 닫을 때 도마 비우기 (아이템 반환하지 않음)
        for (int i = 0; i < _boardSlots.Length; i++) _boardSlots[i] = null;
    }

    public void ShowMiniGameUI()
    {
        if (miniGame != null) miniGame.gameObject.SetActive(true);
    }

    // [기획 반영] 만들 개수 변경 로직
    public void ChangeCookCount(int amount)
    {
        _targetCookCount = Mathf.Clamp(_targetCookCount + amount, 1, 99);
        UpdateCookingState();
    }

    // --------------------------------------------------------------------------
    // 2. 도마 조작 (소모 X, 반환 X -> 순수 ID 등록/해제)
    // --------------------------------------------------------------------------

    // [우클릭] 인벤/창고 아이템을 빈 슬롯에 자동 등록 (소모 없음)
    public bool TryAddIngredient_FromRightClick(int itemId)
    {
        if (!IsValidIngredient(itemId)) return false;

        // 빈 슬롯 찾기 (앞에서부터)
        int targetSlot = -1;
        for (int i = 0; i < _boardSlots.Length; i++)
        {
            if (_boardSlots[i] == null) { targetSlot = i; break; }
        }

        if (targetSlot == -1) return false; // 꽉 참

        _boardSlots[targetSlot] = new BoardSlotData(itemId);

        //SFX 재생
        SoundManager.Instance?.PlaySfx(SfxId.Cook_AddIng);

        UpdateCookingState();
        return true;
    }

    // [좌클릭] 손에 든(Held) 아이템을 지정 슬롯에 등록하고, 원래 자리로 복귀 (소모 없음)
    public void TrySetIngredient_FromHeld(int slotIndex)
    {
        if (sharedHeld == null || !sharedHeld.HasItem) return;
        if (slotIndex < 0 || slotIndex >= _boardSlots.Length) return;

        int itemId = sharedHeld.Stack.itemId;
        if (!IsValidIngredient(itemId)) return;

        // 도마에 ID 등록
        _boardSlots[slotIndex] = new BoardSlotData(itemId);

        // [핵심] 아이템은 소모되지 않으므로 원래 있던 슬롯으로 되돌려 보냄
        ReturnHeldItemToOrigin();

        UpdateCookingState();
    }

    private void ReturnHeldItemToOrigin()
    {
        if (sharedHeld == null || !sharedHeld.HasItem) return;

        if (sharedHeld.Origin.kind == ContainerKind.Inventory)
        {
            if (inventorySystem != null) inventorySystem.ReturnHeldToOrigin();
        }
        else if (sharedHeld.Origin.kind == ContainerKind.Storage)
        {
            if (storageSystem != null) storageSystem.ReturnHeldToOrigin();
        }
    }

    // [좌클릭] 도마 슬롯 비우기 (반환 없음)
    public void ClearIngredientAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _boardSlots.Length) return;

        // [기획 반영] 단순히 슬롯만 비움 (인벤토리에 Add 하지 않음)
        _boardSlots[slotIndex] = null;

        //SFX 재생
        SoundManager.Instance?.PlaySfx(SfxId.Cook_RemoveOneIng);

        UpdateCookingState();
    }

    public void ClearBoard()
    {
        for (int i = 0; i < _boardSlots.Length; i++) _boardSlots[i] = null;

        //SFX 재생
        SoundManager.Instance?.PlaySfx(SfxId.Cook_RemoveAllIng);

        UpdateCookingState();
    }

    private bool IsValidIngredient(int itemId)
    {
        ItemDef def = ItemLoader.Instance?.itemDb.Get(itemId);
        if (def == null || !def.IsIngredient) return false;

        // [기획 반영] 중복 재료 금지
        foreach (var slot in _boardSlots)
            if (slot != null && slot.ItemId == itemId) return false;

        return true;
    }

    // --------------------------------------------------------------------------
    // 3. 상태 갱신 및 검증 (재고량 체크 포함)
    // --------------------------------------------------------------------------
    private void UpdateCookingState()
    {
        List<int> currentIds = new List<int>();
        foreach (var slot in _boardSlots)
            if (slot != null) currentIds.Add(slot.ItemId);

        // 레시피 매칭
        if (recipeLoader != null && recipeLoader.RecipeDb != null)
            MatchedRecipe = recipeLoader.RecipeDb.FindRecipe(currentIds);
        else
            MatchedRecipe = null;

        // 요리 가능 여부 체크 (재고량 포함)
        bool canCook = IsBoardValidForCooking(currentIds.Count);

        if (cookingUI != null)
            cookingUI.RefreshUI(_boardSlots, canCook, _targetCookCount);
    }

    private bool IsBoardValidForCooking(int activeItemCount)
    {
        if (activeItemCount < 2) return false; // 최소 2개

        // 중간 빈칸 체크
        bool foundEmpty = false;
        for (int i = 0; i < _boardSlots.Length; i++)
        {
            if (_boardSlots[i] == null) foundEmpty = true;
            else if (foundEmpty) return false;
        }

        // [기획 반영] 실제 보유량(A) 체크
        foreach (var slot in _boardSlots)
        {
            if (slot != null)
            {
                long totalOwned = GetAvailableItemCount(slot.ItemId);
                if (totalOwned < _targetCookCount) return false; // 재료 부족
            }
        }
        return true;
    }

    // [기획 반영] A = 인벤 + 창고 + Held 총합 계산
    public long GetAvailableItemCount(int itemId)
    {
        long count = 0;
        if (inventorySystem != null) count += inventorySystem.CountItem(itemId);
        if (storageSystem != null) count += storageSystem.CountItem(itemId);

        if (sharedHeld != null && sharedHeld.HasItem && sharedHeld.Stack.itemId == itemId)
            count += sharedHeld.Stack.amount;

        return count;
    }

    // --------------------------------------------------------------------------
    // 4. 요리 실행 및 소모 (Cook 버튼 클릭 시)
    // --------------------------------------------------------------------------
    public void StartCookingProcess()
    {
        List<int> currentIds = new List<int>();
        foreach (var slot in _boardSlots) if (slot != null) currentIds.Add(slot.ItemId);

        if (!IsBoardValidForCooking(currentIds.Count)) return;

        if (miniGame != null)
            miniGame.StartGame(currentIds.Count, OnMiniGameFinished);
        else
            OnMiniGameFinished(CookingMiniGame.CookingQuality.Normal);
    }

    private void OnMiniGameFinished(CookingMiniGame.CookingQuality quality)
    {
        // [기획 반영] 실패 여부와 상관없이 재료 소모
        ConsumeIngredients();

        // 결과 아이템 결정
        int resultItemId = (MatchedRecipe != null) ? MatchedRecipe.resultItemId : failureItemId;
        int resultAmount = _targetCookCount;

        int qualityInt = 0;
        switch (quality)
        {
            case CookingMiniGame.CookingQuality.Sloppy: qualityInt = 1; break;
            case CookingMiniGame.CookingQuality.Normal: qualityInt = 0; break;
            case CookingMiniGame.CookingQuality.Perfect: qualityInt = 2; break;
        }

        if (resultItemId == failureItemId)
        {
            qualityInt = 0;
        }

        // 결과 지급 (인벤토리 -> 창고 순)
        if (inventorySystem != null)
        {
            // [수정] qualityInt를 함께 전달!
            int remain = inventorySystem.TryAddFromExternal(resultItemId, resultAmount, qualityInt);

            if (remain > 0 && storageSystem != null)
            {
                // 창고 시스템에도 TryAddFromExternal에 quality 인자가 있다면 넣어줘야 합니다.
                // (만약 StorageSystem도 똑같이 수정했다면 아래처럼)
                storageSystem.TryAddFromExternal(resultItemId, remain, qualityInt);
            }
        }

        // 요리 후 도마 초기화
        ClearBoard();
    }

    // [기획 반영] 재료 소모 로직 (인벤 -> 창고 순, LIFO)
    private void ConsumeIngredients()
    {
        foreach (var slot in _boardSlots)
        {
            if (slot == null) continue;

            int amountNeeded = _targetCookCount;

            // 1. 인벤토리 차감
            if (inventorySystem != null)
            {
                int invCount = inventorySystem.CountItem(slot.ItemId);
                int take = Mathf.Min(invCount, amountNeeded);
                if (take > 0)
                {
                    inventorySystem.RemoveItem(slot.ItemId, take);
                    amountNeeded -= take;
                }
            }

            // 2. 창고 차감 (부족분)
            if (amountNeeded > 0 && storageSystem != null)
            {
                storageSystem.RemoveItem(slot.ItemId, amountNeeded);
            }
        }
    }

    public void OpenRecipeBook()
    {
        if (uiInteract != null)
        {
            uiInteract.OpenRecipeBook();
            Debug.Log("[CookingStation] 요리 중 레시피북을 열었습니다.");
        }
    }

    private void OnMouseDown()
    {
        Debug.Log("유니티 기본 시스템으로 조리대 클릭 감지됨!");
        Interact();
    }
}