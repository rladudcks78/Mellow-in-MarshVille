using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIInteract : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GamePauseManager gpm;
    [SerializeField] private InputReader inputReader;

    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private InventorySystem inventorySystem;

    [SerializeField] private StorageUI storageUI;
    [SerializeField] private StorageSystem storageSystem;

    [SerializeField] private ConfirmPopupUI confirmPopupUI;
    [SerializeField] private ContextMenuUI contextMenuUI;

    [SerializeField] private CookingUI cookingUI;

    [SerializeField] private OptionsMenuUI optionsMenuUI;

    [SerializeField] private RecipeBookUI recipeBookUI;

    [SerializeField] private ShopUIController shopUIController;

    [Header("Canvas")]
    [SerializeField] private Canvas rootCanvas;

    private Vector2 lastPointerPos;

    private SharedHeldSystem sharedHeld;

    private bool rightDragActive;

    private enum HoverArea { None, Inventory, Storage }
    private HoverArea lastDragArea = HoverArea.None;
    private int lastDragIndex = -1;

    private CookingStation currentStation;
    //private bool cookingHeld;
    //private int cookingHeldItemId = -1;
    //private SlotRef cookingHeldOrigin;

    private Camera UICamera
    {
        get
        {
            if (rootCanvas == null) return null;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return rootCanvas.worldCamera;
        }
    }

    private void Awake()
    {
        gpm = GamePauseManager.Instance;
        if (gpm == null)
            gpm = FindAnyObjectByType<GamePauseManager>();

        sharedHeld = SharedHeldSystem.Instance;
        if (sharedHeld == null)
            sharedHeld = FindAnyObjectByType<SharedHeldSystem>();
    }

    private void OnEnable()
    {
        if (inputReader == null) return;

        inputReader.PointEvent += OnPoint;
        inputReader.LeftClickEvent += OnLeftClick;
        inputReader.RightClickStartedEvent += OnRightClickStarted;
        inputReader.RightClickCanceledEvent += OnRightClickCanceled;
        inputReader.NoteEvent += OnNote;

        inputReader.InventoryEvent += OnInventoryToggle;
        inputReader.CloseEvent += OnClose;
    }

    private void OnDisable()
    {
        if (inputReader == null) return;

        inputReader.PointEvent -= OnPoint;
        inputReader.LeftClickEvent -= OnLeftClick;
        inputReader.RightClickStartedEvent -= OnRightClickStarted;
        inputReader.RightClickCanceledEvent -= OnRightClickCanceled;
        inputReader.NoteEvent -= OnNote;

        inputReader.InventoryEvent -= OnInventoryToggle;
        inputReader.CloseEvent -= OnClose;
    }

    private void Start()
    {
        if (recipeBookUI != null)
        {
            recipeBookUI.Init(this);
        }

        // (참고) 다른 UI들도 이런 식으로 Start에서 초기화해주면 결합도가 낮아집니다.
    }

    private void OnPoint(Vector2 screenPos)
    {
        lastPointerPos = screenPos;

        if (inventoryUI != null && inventoryUI.isOpen)
            inventoryUI.SetPointerPos(screenPos);

        if (storageUI != null && storageUI.isOpen)
            storageUI.SetPointerPos(screenPos);

        if(shopUIController != null && shopUIController.isOpen)
            shopUIController.SetPointerPos(screenPos);

        // 우클릭 드래그 중이면 "슬롯 진입"마다 1개씩 놓기
        if (rightDragActive)
            TickRightDrag();
    }

    private void OnClose()
    {
        if (inputReader == null) return;

        //1. 팝업
        if (confirmPopupUI != null && confirmPopupUI.isOpen)
        {
            confirmPopupUI.Cancel();
            return;
        }

        //2.컨텍스트 메뉴 닫기
        if (contextMenuUI != null && contextMenuUI.isOpen)
        {
            contextMenuUI.Hide();
            return;
        }

        if (recipeBookUI != null && recipeBookUI.IsOpen)
        {
            recipeBookUI.Close();
            gpm?.Exit(GamePauseManager.Modal.Note);
            return; // 여기서 멈춰야 요리 모드가 통째로 꺼지지 않습니다.
        }

        // 3. 상점 열려있으면 상점 먼저 닫기 (핵심)
        if (shopUIController != null && shopUIController.isOpen)
        {
            CloseShop();
            return;
        }

        //4.옵션창 열려있으면 옵션 닫기
        if (optionsMenuUI != null && optionsMenuUI.IsOpen)
        {
            optionsMenuUI.Close();
            return;
        }

        //5. 요리 열려있으면 요리+창고+인벤 같이 닫기
        if (cookingUI != null && cookingUI.isOpen)
        {
            CloseCooking();
            return;
        }

        //6.창고 열려있으면 창고+인벤 같이 닫기
        if (storageUI != null && storageUI.isOpen)
        {
            CloseStorage();
            return;
        }

        //7. 인벤토리만 열려있으면 인벤토리 닫기
        if (inventoryUI != null && inventoryUI.isOpen)
        {
            inventoryUI.Close();
            gpm?.Exit(GamePauseManager.Modal.Inventory);
            return;
        }

        if (optionsMenuUI != null)
        {
            optionsMenuUI.Open();
            return;
        }
    }

    private void OnInventoryToggle()
    {
        if (inventoryUI == null || inputReader == null) return;

        if (confirmPopupUI != null && confirmPopupUI.isOpen) return;
        if (contextMenuUI != null && contextMenuUI.isOpen) contextMenuUI.Hide();

        // 상점 열려있는 동안 I키로 인벤 단독 토글 막기 (핵심)
        if (shopUIController != null && shopUIController.isOpen) return;

        //요리모드에서는 I키 막기
        if (cookingUI != null && cookingUI.isOpen) return;

        //창고 열려있는 상태에서 인벤 키 누르면 둘다 닫기
        if (storageUI != null && storageUI.isOpen)
        {
            CloseStorage();
            return;
        }

        if (!inventoryUI.isOpen)
        {
            inventoryUI.Open();
            gpm?.Enter(GamePauseManager.Modal.Inventory);
        }
        else
        {
            inventoryUI.Close();
            gpm?.Exit(GamePauseManager.Modal.Inventory);
        }
    }

    private void OnLeftClick()
    {
        if (inventoryUI == null) return;
        if (confirmPopupUI != null && confirmPopupUI.isOpen) return;

        // 컨텍스트 메뉴 열려있을 때: 버튼 처리 후 닫기
        if (contextMenuUI != null && contextMenuUI.isOpen)
        {
            bool hitUse = HitRect(contextMenuUI.UseButtonRect, lastPointerPos);
            if (hitUse)
                TryUseOrEquipFromContext();

            contextMenuUI.Hide();
            return;
        }

        // 우선순위: Shop -> Storage -> Inventory

        if(shopUIController != null && shopUIController.isOpen)
        {
            //왼쪽 상점 슬롯
            if(shopUIController.hoveredShopIndex >= 0)
            {
                shopUIController.OnShopSlotLeftClick(shopUIController.hoveredShopIndex);
                return;
            }

            //오른쪽 인벤 슬롯
            if(inventoryUI != null && inventoryUI.isOpen && inventoryUI.hoveredIndex >= 0)
            {
                shopUIController.OnInventorySlotLeftClickFromUIInteract(inventoryUI.hoveredIndex);
                return;
            }

            // 상점 UI 열려있을 때 빈 공간 클릭은 기존 인벤토리/창고 로직으로 안내려가게 막기
            return;            
        }

        if (storageUI != null && storageUI.isOpen && storageUI.hoveredIndex >= 0)
        {
            storageUI.OnSlotLeftClick(storageUI.hoveredIndex);
            return;
        }

        if (inventoryUI != null && inventoryUI.isOpen && inventoryUI.hoveredIndex >= 0)
        {
            inventoryUI.OnSlotLeftClick(inventoryUI.hoveredIndex);
            return;
        }
    }

    private void OnRightClickStarted()
    {
        bool invOpen = inventoryUI != null && inventoryUI.isOpen;
        bool storOpen = storageUI != null && storageUI.isOpen;
        bool cookOpen = cookingUI != null && cookingUI.isOpen;
        bool shopOpen = shopUIController != null && shopUIController.isOpen;

        if (confirmPopupUI != null && confirmPopupUI.isOpen) return;

        // 컨텍스트 메뉴는 우클릭 시작 시 항상 닫기
        if (contextMenuUI != null && contextMenuUI.isOpen)
            contextMenuUI.Hide();

        // 상점 열려있으면 상점 우선 처리 (핵심)
        if (shopOpen)
        {
            // 왼쪽 상점 우클릭 -> 1개 즉시 구매
            if (shopUIController.hoveredShopIndex >= 0)
            {
                shopUIController.OnShopSlotRightClick(shopUIController.hoveredShopIndex);
                return;
            }

            // 오른쪽 인벤토리 슬롯 우클릭 -> 1개 즉시 판매
            if (inventoryUI != null && inventoryUI.isOpen && inventoryUI.hoveredIndex >= 0)
            {
                shopUIController.OnInventorySlotRightClickFromUIInteract(inventoryUI.hoveredIndex);
                return;
            }

            // 상점 UI 열려있을 때 빈 공간 우클릭도 밑 로직으로 안 내려가게 막기
            return;
        }

        // 둘 다 닫혀있으면 우클릭 무시 (상점 분기 뒤로 내려옴)
        if (!invOpen && !storOpen) return;

        // 손에 아이템 들고 있으면: "우클릭 드래그 1개씩 놓기" 시작 (인벤/창고 공통)
        if (sharedHeld != null && sharedHeld.HasItem)
        {
            BeginRightDrag();
            return;
        }

        // 요리 모드 중에는 우클릭 시 해당 아이템 도마 위에 올리기
        if (cookOpen && currentStation != null)
        {
            if (TryPutOneIngredientToBoard_UnderPointer()) return;
        }

        // 손이 비어있고, 창고+인벤이 동시에 열려있으면: 컨텍스트 메뉴 금지, 대신 "퀵 전송"
        if (invOpen && storOpen)
        {
            TryQuickTransferUnderPointer();
            return;
        }

        // 인벤만 열려있을 때
        if (!invOpen) return;

        int hovered = inventoryUI.hoveredIndex;
        if (hovered < 0) return;

        if (inventorySystem == null || !inventorySystem.IsReady) return;

        var stack = inventorySystem.Inventory.Get(hovered);
        if (stack.IsEmpty) return;

        if (!inventorySystem.TryGetDef(stack.itemId, out var def)) return;

        // 사용 가능한 아이템인지 체크
        if (def.isUsable)
        {
            // Case A: 음식/소모품 (장비가 아님) -> 즉시 섭취
            if (!def.IsEquipment)
            {
                if (BuffManager.Instance != null)
                {
                    // 1. 버프 매니저에게 섭취 요청 (ID + 품질)
                    BuffManager.Instance.ConsumeFood(def.itemId, stack.quality);

                    // 2. 아이템 1개 감소
                    inventorySystem.ConsumeAt(hovered, 1);

                    Debug.Log($"[UIInteract] 냠냠! {def.name}을(를) 우클릭으로 바로 먹었습니다.");
                }
                // 메뉴를 띄우지 않고 여기서 함수 종료
                return;
            }
            // Case B: 장비 (도구, 무기 등) -> 컨텍스트 메뉴 표시 (장착/버리기 등 선택)
            else
            {
                contextMenuUI?.Show(lastPointerPos, hovered);
            }
        }
    }

    private void OnRightClickCanceled()
    {
        if (inventoryUI == null || !inventoryUI.isOpen) return;
        EndRightDrag();
    }

    private void BeginRightDrag()
    {
        rightDragActive = true;
        lastDragArea = HoverArea.None;
        lastDragIndex = -1;

        // 시작하자마자 현재 슬롯에도 1회 놓기 시도(정지 클릭해도 1개 놓이게)
        TickRightDrag();
    }

    private void EndRightDrag()
    {
        rightDragActive = false;
        lastDragArea = HoverArea.None;
        lastDragIndex = -1;
    }

    private void TickRightDrag()
    {
        if (!rightDragActive) return;
        if (sharedHeld == null || !sharedHeld.HasItem)
        {
            EndRightDrag();
            return;
        }

        // 현재 마우스가 올라간 슬롯 결정 (우선순위: Storage -> Inventory)
        if (!TryGetHoveredSlot(out var area, out var index))
        {
            // 슬롯 밖으로 나가면 "마지막 슬롯" 리셋 -> 다시 들어오면 1개 놓기 가능
            lastDragArea = HoverArea.None;
            lastDragIndex = -1;
            return;
        }

        // 같은 슬롯에서 계속 떨리는 입력이면 중복 방지
        if (area == lastDragArea && index == lastDragIndex)
            return;

        // 1개 놓기 시도 (각 시스템의 OnSlotRightClick이 "빈칸/같은아이템"만 처리)
        if (area == HoverArea.Storage)
        {
            storageSystem?.OnSlotRightClick(index);
        }
        else if (area == HoverArea.Inventory)
        {
            inventorySystem?.OnSlotRightClick(index);
        }

        lastDragArea = area;
        lastDragIndex = index;

        // 놓다가 다 떨어지면 드래그 종료
        if (!sharedHeld.HasItem)
            EndRightDrag();
    }

    private bool TryGetHoveredSlot(out HoverArea area, out int index)
    {
        area = HoverArea.None;
        index = -1;

        // Storage 우선
        if (storageUI != null && storageUI.isOpen && storageUI.hoveredIndex >= 0)
        {
            area = HoverArea.Storage;
            index = storageUI.hoveredIndex;
            return true;
        }

        if (inventoryUI != null && inventoryUI.isOpen && inventoryUI.hoveredIndex >= 0)
        {
            area = HoverArea.Inventory;
            index = inventoryUI.hoveredIndex;
            return true;
        }

        return false;
    }

    private void TryQuickTransferUnderPointer()
    {
        if (inventorySystem == null || storageSystem == null) return;
        if (!inventorySystem.IsReady || !storageSystem.IsReady) return;

        if (!TryGetHoveredSlot(out var area, out var index)) return;

        // Storage -> Inventory
        if (area == HoverArea.Storage)
        {
            var s = storageSystem.Storage.Get(index);
            if (s.IsEmpty) return;

            int remain = inventorySystem.TryAddFromExternal(s.itemId, s.amount);
            int moved = s.amount - remain;
            if (moved <= 0) return; // 인벤에 넣을 수 없음

            if (remain <= 0) storageSystem.Storage.Set(index, default);
            else storageSystem.Storage.Set(index, new ItemStack(s.itemId, remain));

            return;
        }

        // Inventory -> Storage
        if (area == HoverArea.Inventory)
        {
            var s = inventorySystem.Inventory.Get(index);
            if (s.IsEmpty) return;

            int remain = storageSystem.TryAddFromExternal(s.itemId, s.amount);
            int moved = s.amount - remain;
            if (moved <= 0) return; // 창고에 넣을 수 없음

            if (remain <= 0) inventorySystem.Inventory.Set(index, default);
            else inventorySystem.Inventory.Set(index, new ItemStack(s.itemId, remain));

            return;
        }
    }

    private void TryUseOrEquipFromContext()
    {
        if (contextMenuUI == null || inventorySystem == null) return;

        int slotIndex = contextMenuUI.SlotIndex;
        inventorySystem.SetActiveSlot(slotIndex);
    }

    //private bool TrySelectCookingHeld_UnderPointer()
    //{
    //    if (!TryGetHoveredSlot(out var area, out var index)) return false;

    //    ItemStack stack = default;

    //    if(area == HoverArea.Inventory)
    //    {
    //        if (inventorySystem == null || !inventorySystem.IsReady) return false;
    //        stack = inventorySystem.Inventory.Get(index);
    //        if (stack.IsEmpty) return false;

    //        if (!inventorySystem.TryGetDef(stack.itemId, out var def)) return false;
    //        if (!def.IsIngredient) return false;

    //        cookingHeld = true;
    //        cookingHeldItemId = stack.itemId;
    //        cookingHeldOrigin = new SlotRef(ContainerKind.Inventory, index);
    //        return true;
    //    }
    //    else if(area == HoverArea.Storage)
    //    {
    //        if (storageSystem == null || !storageSystem.IsReady) return false;
    //        stack = storageSystem.Storage.Get(index);
    //        if(stack.IsEmpty) return false;

    //        //재료 여부는 InventorySystem DB로 통일해서 판단 ( 둘이 같은 ItemDB를 쓰는 구조라 가능 )
    //        if (inventorySystem == null || !inventorySystem.IsReady) return false;
    //        if (!inventorySystem.TryGetDef(stack.itemId, out var def)) return false;
    //        if (!def.IsIngredient) return false;

    //        cookingHeld = true;
    //        cookingHeldItemId = stack.itemId;
    //        cookingHeldOrigin = new SlotRef(ContainerKind.Storage, index);
    //        return true;
    //    }

    //    return false;
    //}

    private bool TryPutOneIngredientToBoard_UnderPointer()
    {
        if (currentStation == null) return false;
        if (!TryGetHoveredSlot(out var area, out var index)) return false;

        //슬롯에서 스택 가져오기
        ItemStack stack = default;

        if(area == HoverArea.Inventory)
        {
            if (inventorySystem == null || !inventorySystem.IsReady) return false;
            stack = inventorySystem.Inventory.Get(index);
        }
        else if(area == HoverArea.Storage)
        {
            if (storageSystem == null || !storageSystem.IsReady) return false;
            stack = storageSystem.Storage.Get(index);
        }
        else return false;

        if(stack.IsEmpty) return false;

        //재료인지 체크
        if (inventorySystem == null || !inventorySystem.IsReady) return false;
        if (!inventorySystem.TryGetDef(stack.itemId, out var def)) return false;
        if (!def.IsIngredient) return false;

        //보드에 추가 시도
        bool added = currentStation.TryAddIngredient_FromRightClick(stack.itemId);
        if (!added) return false;

        return true;
    }

    private bool HitRect(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, UICamera);
    }

    public void OpenStorage(StorageUI.LayoutMode mode)
    {
        if (inputReader == null) return;

        confirmPopupUI?.Hide();
        if (contextMenuUI != null && contextMenuUI.isOpen) contextMenuUI.Hide();

        storageUI?.Open(mode);
        inventoryUI?.Open(InventoryUI.LayoutMode.WithStorage);

        gpm?.Enter(GamePauseManager.Modal.Storage);
    }

    public void CloseStorage()
    {
        if (inputReader == null) return;

        confirmPopupUI?.Hide();
        if (confirmPopupUI != null && confirmPopupUI.isOpen) confirmPopupUI.Hide();

        storageUI?.Close();
        inventoryUI?.Close();

        gpm?.Exit(GamePauseManager.Modal.Storage);
    }

    public void OpenCooking(CookingStation station)
    {
        if (inputReader == null) return;

        // 다른 UI 정리
        confirmPopupUI?.Hide();
        if (contextMenuUI != null && contextMenuUI.isOpen) contextMenuUI.Hide();
        if (storageUI != null && storageUI.isOpen) CloseStorage();

        //가상 Held 초기화
        //ClearCookingHeld();

        currentStation = station;

        if (cookingUI != null) cookingUI.Show();
        if (inventoryUI != null) inventoryUI.Open(InventoryUI.LayoutMode.Cooking);

        storageUI.Open(StorageUI.LayoutMode.Cooking);

        currentStation.OpenCookingModeInternal();

        gpm?.Enter(GamePauseManager.Modal.Cooking);
    }

    public void CloseCooking()
    {
        CloseStorage();

        if (currentStation != null)
        {
            currentStation.CloseCooking(); 
                                           
            currentStation = null;
        }

        // UI 닫기
        if (cookingUI != null) cookingUI.Hide();

        // 모달 해제
        gpm?.Exit(GamePauseManager.Modal.Cooking);
    }

    public void ToggleInventoryFromHUD()
    {
        OnInventoryToggle();
    }

    public void ToggleRecipeBookFromHUD()
    {
        OnNote(); 
    }

    private void OnNote()
    {
        if (inputReader == null) return;
        if (recipeBookUI == null) return;

        // 다른 UI가 열려있을 때의 우선순위 처리 (예: 팝업이 켜져있으면 무시)
        if (confirmPopupUI != null && confirmPopupUI.isOpen) return;

        if (recipeBookUI.IsOpen)
        {
            recipeBookUI.Close();
            gpm?.Exit(GamePauseManager.Modal.Note);
        }
        else
        {
            // 인벤토리 등 다른 창이 열려있다면 닫아줍니다.
            if (inventoryUI != null && inventoryUI.isOpen) inventoryUI.Close();
            if (storageUI != null && storageUI.isOpen) storageUI.Close();

            recipeBookUI.Open();
            gpm?.Enter(GamePauseManager.Modal.Note);
        }
    }

    // 요리 모드 중 레시피 버튼을 마우스로 클릭했을 때 호출할 함수
    public void OpenRecipeBook()
    {
        if (recipeBookUI != null && !recipeBookUI.IsOpen)
        {
            recipeBookUI.Open();
            // 요리 모드 중에는 이미 Modal.Cooking 상태이므로 별도의 gpm(일시정지) 조작은 생략
        }
    }

    public void CloseRecipeBookUI()
    {
        if (recipeBookUI != null && recipeBookUI.IsOpen) // 창이 열려있을 때만 작동
        {
            recipeBookUI.Close(); 

            gpm?.Exit(GamePauseManager.Modal.Note);

            Debug.Log("[UIInteract] 레시피북이 정식으로 닫혔습니다.");
        }
    }

    //private void ClearCookingHeld()
    //{
    //    cookingHeld = false;
    //    cookingHeldItemId = -1;
    //    cookingHeldOrigin = default;
    //}

    public bool OpenShopForNpc(int npcId)
    {
        if (shopUIController == null)
            shopUIController = FindAnyObjectByType<ShopUIController>();

        if (shopUIController == null)
        {
            Debug.LogWarning("[UIInteract] ShopUIController 참조가 없습니다.");
            return false;
        }

        // 충돌 가능 UI 정리
        confirmPopupUI?.Hide();
        if (contextMenuUI != null && contextMenuUI.isOpen)
            contextMenuUI.Hide();

        // 상점과 동시 오픈하면 꼬이는 UI들 정리
        if (cookingUI != null && cookingUI.isOpen)
            CloseCooking();

        if (storageUI != null && storageUI.isOpen)
            CloseStorage();

        // 먼저 상점 데이터/오픈 가능 여부 체크
        bool opened = shopUIController.OpenForNpc(npcId);
        if (!opened)
            return false;

        // 상점 우측 판매 패널용 인벤토리 UI 보장
        if (inventoryUI != null && !inventoryUI.isOpen)
        {
            bool wasOpen = inventoryUI.isOpen;

            // 이미 열려있어도 Shop 레이아웃으로 강제 적용되게 호출
            inventoryUI.Open(InventoryUI.LayoutMode.Shop);

            // 모달 진입은 처음 열 때
            if (!wasOpen)
                gpm?.Enter(GamePauseManager.Modal.Inventory);
        }

        return true;
    }

    public void CloseShop()
    {
        // 상점 열려있을 때 컨텍스트 메뉴 같이 정리
        if (contextMenuUI != null && contextMenuUI.isOpen)
            contextMenuUI.Hide();

        if (shopUIController != null && shopUIController.isOpen)
            shopUIController.CloseShop();

        // 상점 세션 종료 시 인벤토리도 함께 닫기 (현재 구조 기준)
        if (inventoryUI != null && inventoryUI.isOpen)
        {
            inventoryUI.Close();
            gpm?.Exit(GamePauseManager.Modal.Inventory);
        }
    }
}
