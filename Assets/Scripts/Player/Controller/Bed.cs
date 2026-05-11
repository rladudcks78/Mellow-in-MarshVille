using UnityEngine;

public class Bed : MonoBehaviour
{
    [Header("--- Settings ---")]
    [Tooltip("InputReader SO 파일을 연결하세요")]
    [SerializeField] private InputReader inputReader;

    private bool _isPlayerInZone = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어가 침대 범위에 들어옴
        if (collision.CompareTag("Player"))
        {
            _isPlayerInZone = true;
            // 우클릭 이벤트 구독
            if (inputReader != null) inputReader.ContextClickEvent += OnInteract;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // 플레이어가 침대 범위에서 나감
        if (collision.CompareTag("Player"))
        {
            _isPlayerInZone = false;
            // 우클릭 이벤트 구독 해제 (필수)
            if (inputReader != null) inputReader.ContextClickEvent -= OnInteract;
        }
    }

    // 우클릭 시 실행되는 함수
    private void OnInteract()
    {
        if (!_isPlayerInZone) return;

        // 이미 자는 중이거나 시간이 멈췄으면 무시
        if (TimeManager.Instance.IsTimeStopped) return;

        Debug.Log("[Bed] 취침합니다.");

        // TimeManager의 Sleep 호출 -> 시간 정지 -> 날짜 변경
        TimeManager.Instance.Sleep();
    }
}