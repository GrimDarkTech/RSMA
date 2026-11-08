using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RSMAVirtualButtonInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RSMAVirtualMicrocontroller microcontroller;
    [SerializeField] private LimitSwitch limitSwitch;
    [SerializeField] private byte virtualPin;

    private bool previousPressed;
    private bool initialized;

    private void Update()
    {
        if (microcontroller == null || limitSwitch == null)
            return;

        var pressed = limitSwitch.SendData() == "1";
        if (!initialized)
        {
            previousPressed = pressed;
            initialized = true;
            microcontroller.SetButtonPressed(virtualPin, pressed);
            return;
        }

        if (pressed == previousPressed)
            return;

        previousPressed = pressed;
        microcontroller.SetButtonPressed(virtualPin, pressed);
    }

    public void SetPressed(bool pressed)
    {
        if (microcontroller == null)
            return;
        previousPressed = pressed;
        initialized = true;
        microcontroller.SetButtonPressed(virtualPin, pressed);
    }

    public void Press()
    {
        SetPressed(true);
    }

    public void Release()
    {
        SetPressed(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Press();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        if (initialized && previousPressed)
            SetPressed(false);
    }
}