using UnityEngine;
using UnityEngine.EventSystems; 

public class PressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool _isPressed;
    public bool IsPressed => _isPressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
    }

    private void OnDisable()
    {
        _isPressed = false;
    }
}