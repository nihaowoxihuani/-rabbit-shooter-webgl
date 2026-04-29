using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;

    public Vector2 InputDirection { get; private set; }

    private Vector2 origin;
    private float radius;
    private bool isDragging;

    void Start()
    {
        origin = background.position;
        radius = background.sizeDelta.x / 2.5f;
        InputDirection = Vector2.zero;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Vector2 direction = eventData.position - origin;
        float magnitude = Mathf.Clamp(direction.magnitude, 0, radius);
        InputDirection = direction.normalized * (magnitude / radius);
        handle.position = origin + direction.normalized * magnitude;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        InputDirection = Vector2.zero;
        handle.position = origin;
    }
}
