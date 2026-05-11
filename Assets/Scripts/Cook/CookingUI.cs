using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 필수

public class CookingUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button cookButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetBoardButton;
    [SerializeField] private Button recipeBookButton;

    [Header("Quantity Refs")]
    [SerializeField] private TMP_Text cookCountText; // "2" 표시용
    [SerializeField] private Button countUpButton;
    [SerializeField] private Button countDownButton;

    [Header("Slots")]
    [SerializeField] private Transform slotParent;
    [SerializeField] private Image slotPrefab;

    [Header("Resources")]
    [SerializeField] private SpriteResolver spriteResolver;
    [SerializeField] private Sprite emptySlotSprite;

    private CookingStation station;

    public bool isOpen => root != null && root.activeSelf;

    public void Init(CookingStation station)
    {
        this.station = station;

        // 버튼 리스너 연결
        cookButton.onClick.RemoveAllListeners();
        cookButton.onClick.AddListener(() => station.StartCookingProcess());

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => station.RequestClose());

        if (resetBoardButton != null)
        {
            resetBoardButton.onClick.RemoveAllListeners();
            resetBoardButton.onClick.AddListener(() => station.ClearBoard()); // 반환 대신 Clear
        }

        if (recipeBookButton != null)
        {
            recipeBookButton.onClick.RemoveAllListeners();
            recipeBookButton.onClick.AddListener(() => station.OpenRecipeBook());
        }

        // [기획 반영] 수량 조절 버튼 연결
        if (countUpButton != null)
        {
            countUpButton.onClick.RemoveAllListeners();
            countUpButton.onClick.AddListener(() => station.ChangeCookCount(1));
        }
        if (countDownButton != null)
        {
            countDownButton.onClick.RemoveAllListeners();
            countDownButton.onClick.AddListener(() => station.ChangeCookCount(-1));
        }

        if (root != null) root.SetActive(false);
    }

    public void Show()
    {
        if (root != null) root.SetActive(true);

        //SFX 재생
        if(SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Cook_Open_Chop);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Cook_Open_Steam);
    }

    public void Hide() { if (root != null) root.SetActive(false); }

    public void RefreshUI(BoardSlotData[] boardSlots, bool canCook, int targetCount)
    {
        if (slotParent == null || slotPrefab == null) return;

        // 수량 텍스트 갱신
        if (cookCountText != null) cookCountText.text = targetCount.ToString();

        // 슬롯 초기화
        foreach (Transform child in slotParent) Destroy(child.gameObject);

        for (int i = 0; i < boardSlots.Length; i++)
        {
            Image slotObj = Instantiate(slotPrefab, slotParent);
            Button btn = slotObj.GetComponent<Button>();

            // 배경 설정
            slotObj.sprite = emptySlotSprite;
            slotObj.color = Color.white;

            Image iconImg = null;
            TMP_Text amountInfoText = null;

            if (slotObj.transform.childCount > 0)
                iconImg = slotObj.transform.GetChild(0).GetComponent<Image>();

            if (slotObj.transform.childCount > 1)
                amountInfoText = slotObj.transform.GetChild(1).GetComponent<TMP_Text>();

            BoardSlotData data = boardSlots[i];
            int index = i;

            // Case 1: 재료가 있는 슬롯
            if (data != null)
            {
                ItemDef def = ItemLoader.Instance?.itemDb.Get(data.ItemId);

                if (iconImg != null && def != null && spriteResolver != null)
                {
                    iconImg.gameObject.SetActive(true);
                    iconImg.sprite = spriteResolver.Load(def.spritePath);

                    long owned = station.GetAvailableItemCount(data.ItemId);

                    // 아이콘: 부족하면 반투명 처리 (기존 유지)
                    if (owned < targetCount)
                        iconImg.color = new Color(1f, 1f, 1f, 0.4f);
                    else
                        iconImg.color = Color.white;

                    // [수정된 부분] 텍스트: 부족할 때만 앞부분 빨간색 태그 적용
                    if (amountInfoText != null)
                    {
                        amountInfoText.gameObject.SetActive(true);

                        // 기본 텍스트 색상은 흰색으로 고정 (뒤쪽 '/ N' 부분은 흰색이어야 하므로)
                        amountInfoText.color = Color.white;

                        if (owned < targetCount)
                        {
                            // 부족함: 앞부분(owned)만 빨간색 태그 감싸기
                            amountInfoText.text = $"<color=red>{owned}</color>/{targetCount}";
                        }
                        else
                        {
                            // 충분함: 전체 흰색
                            amountInfoText.text = $"{owned}/{targetCount}";
                        }
                    }
                }

                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => station.ClearIngredientAt(index));
                }
            }
            // Case 2: 빈 슬롯
            else
            {
                if (iconImg != null) iconImg.gameObject.SetActive(false);
                if (amountInfoText != null) amountInfoText.gameObject.SetActive(false);

                if (btn != null)
                {
                    btn.interactable = true;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => station.TrySetIngredient_FromHeld(index));
                }
            }
        }

        if (cookButton != null) cookButton.interactable = canCook;

        bool hasAnyItem = false;
        foreach (var slot in boardSlots) if (slot != null) hasAnyItem = true;

        if (resetBoardButton != null) resetBoardButton.interactable = hasAnyItem;
    }
}