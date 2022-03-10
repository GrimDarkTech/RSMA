using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSTM33 : MicrocontrollerBaseScript
{
    [SerializeField] private GameObject powerLED;
    [SerializeField] private GameObject debugLED;
    [SerializeField] private Material activeLEDMaterial;
    [SerializeField] private Material greenLEDMaterial;
    [SerializeField] private Material disabledLEDMaterial;
    private Renderer powerLEDRenderer;
    private Renderer debugLEDRenderer;
    private void Start()
    {
        powerLEDRenderer = powerLED.GetComponent<Renderer>();
        debugLEDRenderer = debugLED.GetComponent<Renderer>();
        MicroStart();
    }
    private void Update()
    {
        if (isActive)
        {
            powerLEDRenderer.material = activeLEDMaterial;
            if (!isLoop)
            {
                MicroStart();
            }
        }
        else
        {
            powerLEDRenderer.material = disabledLEDMaterial;
            isLoop = false;
        }
    }

    private void MicroStart()
    {
        Debug.Log("Started");
        isLoop = true;
        StartCoroutine(MicroLoop());
    }
    private IEnumerator MicroLoop()
    {
        Debug.Log("Time is 0");
        yield return new WaitForSeconds(5);
        Debug.Log("Time is 5");
    }
}
