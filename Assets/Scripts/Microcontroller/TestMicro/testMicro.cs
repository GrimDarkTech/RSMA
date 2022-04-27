using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro : MicrocontrollerBaseScript
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
        I2CBus.reciveData(0);
        range = float.Parse(I2CBus.getData());
        I2CBus.sendData(1);
        if(range < 8)
        {
            GPIO.setDigitalPort(1, true);
            GPIO.setDigitalPort(2, true);
            GPIO.setDigitalPort(3, false);
            GPIO.setDigitalPort(4, false);
            GPIO.setDigitalPort(5, true);
            GPIO.setDigitalPort(6, true);
            GPIO.setDigitalPort(7, false);
            GPIO.setDigitalPort(8, false);
            GPIO.setDigitalPort(9, true);
        }
        else
        {
            GPIO.setDigitalPort(1, false);
            GPIO.setDigitalPort(2, true);
            GPIO.setDigitalPort(3, false);
            GPIO.setDigitalPort(4, true);
            GPIO.setDigitalPort(5, false);
            GPIO.setDigitalPort(6, true);
            GPIO.setDigitalPort(7, false);
            GPIO.setDigitalPort(8, true);
            GPIO.setDigitalPort(9, false);
        }
        isLoop = false;
    }
}
