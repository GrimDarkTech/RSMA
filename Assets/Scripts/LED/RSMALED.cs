using UnityEngine;

public class RSMALED : MonoBehaviour
{
    protected bool mode;
    [SerializeField] private Material activeLEDMaterial;
    [SerializeField] private Material disabledLEDMaterial;

    public int portID;
    public RSMAGPIO GPIOScript;
    private Renderer LEDRenderer;
    private void Start()
    {
        LEDRenderer = gameObject.GetComponent<Renderer>();
    }
    private void Update()
    {
        mode = GPIOScript.GetDigitalPort(portID);
        if (mode)
        {
            LEDRenderer.material = activeLEDMaterial;
        }
        else
        {
            LEDRenderer.material = disabledLEDMaterial;
        }
    }
    public bool GetMode()
    {
        return mode;
    }
}
