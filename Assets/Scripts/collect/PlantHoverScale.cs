using UnityEngine;

public class PlantHoverScale : MonoBehaviour
{
    [SerializeField] private float hoverScale = 1.08f;

    Vector3 baseScale;
    bool isHover;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    public void SetHover(bool on)
    {
        if (isHover == on) return;
        isHover = on;
        transform.localScale = on ? baseScale * hoverScale : baseScale;
    }
}

