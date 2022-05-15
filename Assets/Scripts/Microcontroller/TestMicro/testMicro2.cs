using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro2 : MicrocontrollerBaseScript
{
    private float range;
    private float rangeR;
    private float rangeL;
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
        yield return new WaitForSeconds(0.25f);
        I2CBus.reciveData(0);
        range = float.Parse(I2CBus.getData());
        I2CBus.reciveData(1);
        rangeR = float.Parse(I2CBus.getData());
        I2CBus.reciveData(2);
        rangeL = float.Parse(I2CBus.getData());
        I2CBus.sendData(3);
        if (range < 4 || rangeL < 1.9f || rangeR < 1.9f)
        {
            GPIO.setPWMPort(1, 0.45f);
            GPIO.setPWMPort(2, 0.45f);
            GPIO.setPWMPort(3, 0.45f);
            GPIO.setPWMPort(4, 0.45f);
            if (rangeR < rangeL)
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
            else if (rangeR >= rangeL)
            {
                GPIO.setDigitalPort(1, true);
                GPIO.setDigitalPort(2, false);
                GPIO.setDigitalPort(3, true);
                GPIO.setDigitalPort(4, true);
                GPIO.setDigitalPort(5, false);
                GPIO.setDigitalPort(6, false);
                GPIO.setDigitalPort(7, true);
                GPIO.setDigitalPort(8, true);
                GPIO.setDigitalPort(9, false);
            }

        }
        else
        {
            GPIO.setPWMPort(1, 0.6f);
            GPIO.setPWMPort(2, 0.6f);
            GPIO.setPWMPort(3, 0.6f);
            GPIO.setPWMPort(4, 0.6f);
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
