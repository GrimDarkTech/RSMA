using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro2 : MicrocontrollerBase
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
        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        dataBus.ReciveData(1);
        rangeR = float.Parse(dataBus.GetData());
        dataBus.ReciveData(2);
        rangeL = float.Parse(dataBus.GetData());
        dataBus.SendData(3);
        if (range < 4 || rangeL < 1.9f || rangeR < 1.9f)
        {
            GPIO.SetPWMPort(1, 0.45f);
            GPIO.SetPWMPort(2, 0.45f);
            GPIO.SetPWMPort(3, 0.45f);
            GPIO.SetPWMPort(4, 0.45f);
            if (rangeR < rangeL)
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
            else if (rangeR >= rangeL)
            {
                GPIO.SetDigitalPort(1, true);
                GPIO.SetDigitalPort(2, false);
                GPIO.SetDigitalPort(3, true);
                GPIO.SetDigitalPort(4, true);
                GPIO.SetDigitalPort(5, false);
                GPIO.SetDigitalPort(6, false);
                GPIO.SetDigitalPort(7, true);
                GPIO.SetDigitalPort(8, true);
                GPIO.SetDigitalPort(9, false);
            }

        }
        else
        {
            GPIO.SetPWMPort(1, 0.6f);
            GPIO.SetPWMPort(2, 0.6f);
            GPIO.SetPWMPort(3, 0.6f);
            GPIO.SetPWMPort(4, 0.6f);
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
