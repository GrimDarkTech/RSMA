using UnityEngine;

/// <summary>
/// Implements properties and functionality of Light Emitting Diode 
/// </summary>
[RequireComponent(typeof(Light))]
public class RSMALED : MonoBehaviour
{
    protected ushort mode;
    /// <summary>
    /// LED body 
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
    private Color defaultColor;
    private Material ledMaterial;
    [SerializeField] private Light ledLight;

    private void Start()
    {
        
        if(colorBody != null)
        {
            ledMaterial = colorBody.material;
        }
        else
        {
            ledMaterial = gameObject.GetComponent<Renderer>().material;
        }
        ledLight = gameObject.GetComponent<Light>();
        ledLight.color = color;
        defaultColor = ledMaterial.color;
    }
    private void Update()
    {
        if(connectMicrocontroller != null)
        {
            mode = (ushort)connectMicrocontroller.GetPin(connectedPin).value;
        }
        if (mode == 1)
        {
            ledLight.enabled = true;
            ledMaterial.color = color;
        }
        else
        {
            ledLight.enabled = false;
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
    }
}
