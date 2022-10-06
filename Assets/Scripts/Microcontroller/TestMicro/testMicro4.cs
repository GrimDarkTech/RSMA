using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro4 : MicrocontrollerBase
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
        GPIO.SetPWMPort(0, 0.1f);
        GPIO.SetPWMPort(1, 0.1f);
        GPIO.SetPWMPort(2, 0.1f);
        GPIO.SetPWMPort(3, 0.1f);
        GPIO.SetDigitalPort(0, false);
        GPIO.SetDigitalPort(1, false);
        GPIO.SetDigitalPort(4, false);
        GPIO.SetDigitalPort(5, false);
        GPIO.SetDigitalPort(2, false);
        GPIO.SetDigitalPort(3, false);
        GPIO.SetDigitalPort(6, false);
        GPIO.SetDigitalPort(7, false);
        yield return new WaitForSeconds(0.3f);
        GPIO.SetPWMPort(9, 0f);
        yield return new WaitForSeconds(0.8f);
        dataBus.ReciveData(0);
        rangeR = float.Parse(dataBus.GetData());
        GPIO.SetPWMPort(9, 0.5f);
        yield return new WaitForSeconds(0.8f);
        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        GPIO.SetPWMPort(9, 1f);
        yield return new WaitForSeconds(0.8f);
        dataBus.ReciveData(0);
        rangeL = float.Parse(dataBus.GetData());
        GPIO.SetPWMPort(9, 0.5f);


        if (range <= 3.3f || rangeR <= 1.8f || rangeL <= 1.8f)
        {
            GPIO.SetPWMPort(0, 1f);
            GPIO.SetPWMPort(1, 1f);
            GPIO.SetPWMPort(2, 1f);
            GPIO.SetPWMPort(3, 1f);
            if (rangeR < rangeL)
            {
                TurnLeft();
                dataBus.SetData("Turn left");
                dataBus.SendData(1);
                yield return new WaitForSeconds(1.7f);
            }
            else if (rangeR >= rangeL)
            {
                TurnRight();
                dataBus.SetData("Turn right");
                dataBus.SendData(1);
                yield return new WaitForSeconds(1.7f);
            }

        }
        else
        {
            yield return new WaitForSeconds(0.6f);
            GPIO.SetPWMPort(9, 0.5f);
            for (int i =0; i < 25; i++)
            {
                dataBus.ReciveData(0);
                range = float.Parse(dataBus.GetData());
                if (range >= 2.2f)
                {
                    MoveForward();
                    dataBus.SetData("Forvard");
                    dataBus.SendData(1);
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
        isLoop = false;
    }

    void TurnLeft()
    {
        GPIO.SetDigitalPort(0, true);
        GPIO.SetDigitalPort(1, false);
        GPIO.SetDigitalPort(4, true);
        GPIO.SetDigitalPort(5, false);
        GPIO.SetDigitalPort(2, false);
        GPIO.SetDigitalPort(3, true);
        GPIO.SetDigitalPort(6, false);
        GPIO.SetDigitalPort(7, true);

    }
    void TurnRight()
    {
        GPIO.SetDigitalPort(0, false);
        GPIO.SetDigitalPort(1, true);
        GPIO.SetDigitalPort(4, false);
        GPIO.SetDigitalPort(5, true);
        GPIO.SetDigitalPort(2, true);
        GPIO.SetDigitalPort(3, false);
        GPIO.SetDigitalPort(6, true);
        GPIO.SetDigitalPort(7, false);
    }
    void MoveForward()
    {
        GPIO.SetPWMPort(0, 1.0f);
        GPIO.SetPWMPort(1, 1.0f);
        GPIO.SetPWMPort(2, 1.0f);
        GPIO.SetPWMPort(3, 1.0f);
        GPIO.SetDigitalPort(0, true);
        GPIO.SetDigitalPort(1, false);
        GPIO.SetDigitalPort(4, true);
        GPIO.SetDigitalPort(5, false);
        GPIO.SetDigitalPort(2, true);
        GPIO.SetDigitalPort(3, false);
        GPIO.SetDigitalPort(6, true);
        GPIO.SetDigitalPort(7, false);
    }
}
