using UnityEngine;
using UnityEngine.EventSystems;

public class HUDRecipeButton : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UIInteract uiInteract;

    private void Awake()
    {
        if (uiInteract == null)
            uiInteract = FindAnyObjectByType<UIInteract>();
    }

    public void OnClickRecipeBook()
    {
        if (uiInteract == null)
        {
            Debug.LogWarning("[HUDRecipeButton] UIInteract를 찾을 수 없습니다.");
            return;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        uiInteract.ToggleRecipeBookFromHUD();
    }
}