using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro : MicrocontrollerBase
{
    private float range;
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
        yield return new WaitForSeconds(0.3f);
        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        dataBus.SendData(1);
        if(range < 8)
        {
            GPIO.SetDigitalPort(1, true);
            GPIO.SetDigitalPort(2, true);
            GPIO.SetDigitalPort(3, false);
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, true);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
            GPIO.SetDigitalPort(8, false);
            GPIO.SetDigitalPort(9, true);
        }
        else
        {
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, true);
            GPIO.SetDigitalPort(3, false);
            GPIO.SetDigitalPort(4, true);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
            GPIO.SetDigitalPort(8, true);
            GPIO.SetDigitalPort(9, false);
        }
        isLoop = false;
    }
}
