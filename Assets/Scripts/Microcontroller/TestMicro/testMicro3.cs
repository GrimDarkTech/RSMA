using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro3 : MicrocontrollerBaseScript
{
    private void Start()
    {
        isLoop = false;
    }
    private void Update()
    {
        if (!isLoop)
        {
            StartCoroutine(MicroLoop());
        }
    }
    private IEnumerator MicroLoop()
    {
        isLoop = true;
        yield return new WaitForSeconds(0.1f);

        isLoop = false;
    }
}
