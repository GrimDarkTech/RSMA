using UnityEngine;

[RequireComponent(typeof(Light))]
public class RSMALED : MonoBehaviour
{
    protected ushort mode;
    public Renderer colorBody;
    public Color color;
    public ConnectedPin connectedPin;
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

    public void SetMode(ushort newMode)
    {
        mode = newMode;
    }
}
