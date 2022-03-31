using UnityEngine;

public class LedBaseScript : MonoBehaviour
{
    protected bool mode;
    [SerializeField] private Material activeLEDMaterial;
    [SerializeField] private Material disabledLEDMaterial;
    public GameObject GPIO;
    public int portID;
    private GPIOBaseScript GPIOScript;
    private Renderer LEDRenderer;
    private void Start()
    {
        LEDRenderer = gameObject.GetComponent<Renderer>();
        GPIOScript = GPIO.GetComponent<GPIOBaseScript>();
    }
    private void Update()
    {
        mode = GPIOScript.getDigitalPort(portID);
        if (mode)
        {
            LEDRenderer.material = activeLEDMaterial;
        }
        else
        {
            LEDRenderer.material = disabledLEDMaterial;
        }
    }
    public bool getMode()
    {
        return mode;
    }
}
