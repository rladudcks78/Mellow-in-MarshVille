using UnityEngine;
using UnityEngine.UI;
using TMPro; // 👈 텍스트 제어를 위해 추가

public class RecipeSlotUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText; 
    [SerializeField] private Button slotButton;

    private RecipeDef myRecipe;
    private bool isUnlocked;
    private RecipeBookUI manager;

    public void Init(RecipeDef recipe, bool unlocked, Sprite iconSprite, RecipeBookUI bookManager)
    {
        myRecipe = recipe;
        isUnlocked = unlocked;
        manager = bookManager;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => manager.SelectRecipe(myRecipe, isUnlocked));

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;

            iconImage.color = isUnlocked ? Color.white : Color.black;
        }

        if (nameText != null)
        {
            nameText.text = isUnlocked ? recipe.recipeName : "???";

            nameText.color = isUnlocked ? Color.white : Color.gray;
        }
    }
}