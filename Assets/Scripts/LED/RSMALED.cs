using UnityEngine;
using RSMA.Microcontroller.Virtual;

/// <summary>
/// Implements properties and functionality of Light Emitting Diode 
/// </summary>
[RequireComponent(typeof(Light))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_leds.md")]
public class RSMALED : MonoBehaviour
{
    protected ushort mode;
    /// <summary>
    /// LED body. Object which model will change color when the LED is turned on/off. If the value is not set, the component will try to find it automatically on the object to which it is attached
    /// </summary>
    public Renderer colorBody;
    /// <summary>
    /// LED color 
    /// </summary>
    public Color color;
    /// <summary>
    /// GPIO port pin which LED is connected
    /// </summary>
    public ConnectedPin connectedPin;
    /// <summary>
    /// GPIO which LED is connected
    /// </summary>
    public RSMAGPIO connectMicrocontroller;

    [Header("Virtual MCU PWM")]
    [SerializeField] private RSMAVirtualMicrocontroller virtualMicrocontroller;
    [SerializeField] private byte virtualPin;
    [SerializeField] private bool usePwmBrightness = true;

    private Color defaultColor;
    private float brightness;
    private Material ledMaterial;
    [SerializeField] private Light ledLight;

    private void Start()
    {
        
        if(colorBody == null)
        {
            colorBody = gameObject.GetComponent<Renderer>();
        }
        if (colorBody != null)
            ledMaterial = colorBody.material;
        ledLight = gameObject.GetComponent<Light>();
        ledLight.color = color;
        ledLight.intensity = 1f;
        if (ledMaterial != null)
            defaultColor = ledMaterial.color;
    }
    private void Update()
    {
        if (virtualMicrocontroller != null)
        {
            brightness = Mathf.Clamp01(virtualMicrocontroller.GetPinValue(virtualPin));
            mode = (ushort)(brightness > 0f ? 1 : 0);
        }
        else if(connectMicrocontroller != null)
        {
            mode = (ushort)connectMicrocontroller.GetPin(connectedPin).value;
            brightness = Mathf.Clamp01(connectMicrocontroller.GetPin(connectedPin).value);
        }
        if (brightness > 0f)
        {
            ledLight.enabled = true;
            if (usePwmBrightness)
            {
                ledLight.intensity = brightness;
                if (ledMaterial != null)
                    ledMaterial.color = Color.Lerp(defaultColor, color, brightness);
            }
            else if (ledMaterial != null)
                ledMaterial.color = color;
        }
        else
        {
            ledLight.enabled = false;
            if (ledMaterial != null)
                ledMaterial.color = defaultColor;
        }
    }
    /// <summary>
    /// Sets LED mode
    /// </summary>
    /// <param name="mode">LED mode (1 - active, else - inactive) </param>
    public void SetMode(ushort mode)
    {
        this.mode = mode;
        brightness = mode == 0 ? 0f : 1f;
    }

    public void SetPwm(byte dutyCycle)
    {
        brightness = dutyCycle / 255f;
        mode = dutyCycle == 0 ? (ushort)0 : (ushort)1;
    }
}
