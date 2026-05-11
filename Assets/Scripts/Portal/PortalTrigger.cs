using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PortalTrigger : MonoBehaviour
{
    [Tooltip("이 문을 통과했을 때 이동할 목적지의 ID입니다.")]
    [SerializeField] private string targetLocationID;

    private int playerLayer = 3;
    

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == playerLayer)
        {
            TeleportManager.Instance.RequestTeleport(targetLocationID);
        }
    }
}