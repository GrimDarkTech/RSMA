using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LedBaseScript : MonoBehaviour
{
    public bool mode;
    [SerializeField] private Material activeLEDMaterial;
    [SerializeField] private Material disabledLEDMaterial;
    private Renderer LEDRenderer;
    private void Start()
    {
        LEDRenderer = gameObject.GetComponent<Renderer>();
    }
    private void Update()
    {
        if (mode)
        {
            LEDRenderer.material = activeLEDMaterial;
        }
        else
        {
            LEDRenderer.material = disabledLEDMaterial;
        }
    }
    private void getModeRaw(GameObject device, bool mode)
    {
        //device 
    }
}
