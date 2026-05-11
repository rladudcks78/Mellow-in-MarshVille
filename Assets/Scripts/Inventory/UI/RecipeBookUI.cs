using System.Collections.Generic; 
using UnityEngine; 
using UnityEngine.UI; 
using TMPro; 

public class RecipeBookUI : MonoBehaviour
{
    [Header("Data System")] // 데이터 관련 참조
    [SerializeField] private PlayerData playerData; 
    [SerializeField] private RecipeLoader recipeLoader; 
    [SerializeField] private SpriteResolver spriteResolver;

    [Header("List Panel (Left)")] // 좌측 스크롤 리스트 관련 참조
    [SerializeField] private Transform contentParent; 
    [SerializeField] private GameObject slotPrefab; 

    [Header("Detail Panel (Right)")] // 우측 상세 정보창 관련 참조
    [SerializeField] private GameObject rightPanelObj; 
    [SerializeField] private Image detailIcon; 
    [SerializeField] private TMP_Text detailNameText; 
    [SerializeField] private TMP_Text detailDescText; 

    [Header("Detail Ingredients")] // 우측 하단 필요 재료 목록 참조
    [SerializeField] private Transform ingredientParent; 
    [SerializeField] private GameObject ingredientIconPrefab;

    [SerializeField] private Button closeButton;

    public bool IsOpen => gameObject.activeSelf;

    private List<RecipeSlotUI> spawnedSlots = new List<RecipeSlotUI>();

    private UIInteract uiInteract;

    public void Init(UIInteract interact)
    {
        uiInteract = interact; 

        if (closeButton != null) 
        {
            closeButton.onClick.RemoveAllListeners(); // 혹시 모를 중복 연결을 방지
            closeButton.onClick.AddListener(() => uiInteract.CloseRecipeBookUI());
        }
    }

    public void Open()
    {
        gameObject.SetActive(true); 
        RefreshList(); 

        // 처음 열었을 때는 우측 상세 정보창을 비워두기 위해 꺼둡니다.
        if (rightPanelObj != null) rightPanelObj.SetActive(false);
    }

    public void Close()
    {
        gameObject.SetActive(false); 
    }

    // 좌측 슬롯 목록을 생성하고 데이터를 갱신하는 함수
    private void RefreshList()
    {
        var allRecipes = recipeLoader.RecipeDb.GetAllRecipes();

        for (int i = 0; i < allRecipes.Count; i++)
        {
            if (i >= spawnedSlots.Count)
            {
                GameObject go = Instantiate(slotPrefab, contentParent);

                spawnedSlots.Add(go.GetComponent<RecipeSlotUI>());
            }

            // 슬롯 하나와 레시피 데이터 하나를 짝짓는다
            RecipeDef recipe = allRecipes[i];
            RecipeSlotUI slot = spawnedSlots[i];

            // 플레이어 데이터에서 이 레시피(ID)를 배웠는지 검사합니다.
            bool unlocked = playerData.HasRecipe(recipe.recipeId);
 
            Sprite icon = spriteResolver.Load(recipe.spritePath);

            slot.gameObject.SetActive(true);
            slot.Init(recipe, unlocked, icon, this);
        }

        // 만약 전체 레시피보다 생성된 슬롯이 더 많다면 (버전업 등으로 데이터가 줄어든 예외 상황)
        for (int i = allRecipes.Count; i < spawnedSlots.Count; i++)
        {
            // 남는 슬롯은 화면에서 숨깁니다.
            spawnedSlots[i].gameObject.SetActive(false);
        }
    }

    // 좌측 슬롯이 클릭되었을 때 호출되는 함수
    public void SelectRecipe(RecipeDef recipe, bool isUnlocked)
    {
        if (rightPanelObj != null) rightPanelObj.SetActive(true);

        detailIcon.sprite = spriteResolver.Load(recipe.spritePath);

        if (isUnlocked) 
        {
            detailIcon.color = Color.white; 
            detailNameText.text = recipe.recipeName; 
            detailDescText.text = recipe.description; 

            RefreshIngredients(recipe.ingredients);
        }
        else 
        {
            detailIcon.color = Color.black; 
            detailNameText.text = "???"; 
            detailDescText.text = recipe.discoveryHint; 

            RefreshIngredients(new List<int>());
        }
    }

    // 우측 하단의 필요 재료 아이콘들을 생성/갱신하는 함수
    private void RefreshIngredients(List<int> ingredientIds)
    {
        foreach (Transform child in ingredientParent)
        {
            Destroy(child.gameObject);
        }

        foreach (int itemId in ingredientIds)
        {
            ItemDef itemDef = ItemLoader.Instance?.itemDb.Get(itemId);
            if (itemDef != null) 
            {
                GameObject go = Instantiate(ingredientIconPrefab, ingredientParent);
                Image img = go.GetComponent<Image>();
                img.sprite = spriteResolver.Load(itemDef.spritePath);
            }
        }
    }
}