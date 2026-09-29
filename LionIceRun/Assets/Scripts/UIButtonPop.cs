using UnityEngine;
using UnityEngine.EventSystems;

// 버튼에 마우스를 올리면 살짝 커지고, 누르면 쏙 작아지는 폴가이즈풍 반응
public class UIButtonPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Min(1f)] public float hoverScale = 1.06f;
    [Range(0.5f, 1f)] public float pressScale = 0.94f;
    [Min(1f)] public float speed = 18f;

    float target = 1f;
    bool hovered;

    void OnDisable()
    {
        target = 1f;
        hovered = false;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        float current = transform.localScale.x;
        float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = Vector3.one * next;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; target = hoverScale; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; target = 1f; }
    public void OnPointerDown(PointerEventData eventData) => target = pressScale;
    public void OnPointerUp(PointerEventData eventData) => target = hovered ? hoverScale : 1f;
}
