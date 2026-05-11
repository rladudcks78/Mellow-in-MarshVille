using UnityEngine;
using UnityEngine.EventSystems;

public class HUDBagButton : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UIInteract uiInteract;

    private void Awake()
    {
        if (uiInteract == null)
            uiInteract = FindAnyObjectByType<UIInteract>();
    }

    public void OnClickBag()
    {
        if(uiInteract == null)
        {
            Debug.LogWarning("[HUDBagButton] UIInteract not found.");
            return;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        uiInteract.ToggleInventoryFromHUD();
    }
}
    
