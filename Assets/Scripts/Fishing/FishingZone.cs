using UnityEngine;

public class FishingZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("이 구역이 강(River)인지 바다(Ocean)인지 설정")]
    public FishArea areaType; 

    [Tooltip("이 구역의 계절/바이옴 ID (1:봄/마을, 2:여름, 4:가을, 8:겨울 등)")]
    public int biomeID = 1;

    [Header("Debug")]
    [SerializeField] private Color debugColor = new Color(0, 1, 1, 0.3f);

    private void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            // 콜라이더 크기만큼 반투명 큐브를 그립니다.
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
            Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
        }
    }
}