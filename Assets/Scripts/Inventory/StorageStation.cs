using UnityEngine;

public class StorageStation : MonoBehaviour
{
    [Header("Interact")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private string playerTag = "Player";

    private UIInteract uiInteract;

    private void Awake()
    {
        uiInteract = FindFirstObjectByType<UIInteract>();

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }

    public void Interact()
    {
        Debug.Log("[StorageStation] 창고 Interact 호출됨");

        if (uiInteract == null)
        {
            uiInteract = FindFirstObjectByType<UIInteract>();
            if (uiInteract == null)
            {
                Debug.LogError("[StorageStation] UIInteract를 찾을 수 없습니다.");
                return;
            }
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist > interactDistance)
            {
                Debug.Log($"[StorageStation] 너무 멈 (거리={dist:F2}, 허용={interactDistance:F2})");
                return;
            }
        }

        // 핵심: 창고는 Normal 모드로 열기
        uiInteract.OpenStorage(StorageUI.LayoutMode.Normal);
        Debug.Log("[StorageStation] 창고 UI 열기 요청 완료 (Normal)");
    }

    // (선택) 유니티 기본 클릭 디버그용
    // CookingStation처럼 비교 테스트할 때만 잠깐 쓰고, 최종엔 지워도 됨.
    private void OnMouseDown()
    {
        Debug.Log("[StorageStation] OnMouseDown 감지됨");
        Interact();
    }
}