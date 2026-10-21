using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] float hoverScale = 1.12f;
    [SerializeField] float speed = 12f;

    Vector3 normalScale;
    bool isHovered;

    void Awake()
    {
        normalScale = transform.localScale;
    }

    void Update()
    {
        Vector3 targetScale = isHovered
            ? normalScale * hoverScale
            : normalScale;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.unscaledDeltaTime * speed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }
}