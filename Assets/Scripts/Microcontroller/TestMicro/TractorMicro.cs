using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TractorMicro : MicrocontrollerBase
{
    private string key;
    private int gear;
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
        yield return new WaitForSeconds(0.05f);
        dataBus.ReciveData(0);
        key = dataBus.GetData();
        Debug.Log(key);
        if (key == "W" && gear >= 0 && gear < 100)
        {
            gear = gear + 1;
            GPIO.SetPWMPort(4, gear / 100f);
            GPIO.SetPWMPort(1, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(3, Mathf.Abs(gear / 100f));
            GPIO.SetDigitalPort(1, true);
            GPIO.SetDigitalPort(2, false);
            GPIO.SetDigitalPort(3, true);
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, true);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, true);
            GPIO.SetDigitalPort(8, false);
        }
        else if (key == "S" && gear > 0 && gear <= 100)
        {
            gear = gear - 1;
            GPIO.SetPWMPort(4, gear / 100f);
            GPIO.SetPWMPort(1, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(3, Mathf.Abs(gear / 100f));
            GPIO.SetDigitalPort(1, true);
            GPIO.SetDigitalPort(2, false);
            GPIO.SetDigitalPort(3, true);
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, true);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, true);
            GPIO.SetDigitalPort(8, false);
        }
        else if (key == "S" && gear <= 0 && gear > -100)
        {
            gear = gear - 1;
            GPIO.SetPWMPort(4, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(1, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(3, Mathf.Abs(gear / 100f));
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, true);
            GPIO.SetDigitalPort(3, false);
            GPIO.SetDigitalPort(4, true);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
            GPIO.SetDigitalPort(8, true);
        }
        else if (key == "W" && gear <= 0 && gear >= -100)
        {
            gear = gear + 1;
            GPIO.SetPWMPort(4, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(1, Mathf.Abs(gear / 100f));
            GPIO.SetPWMPort(3, Mathf.Abs(gear / 100f));
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, true);
            GPIO.SetDigitalPort(3, false);
            GPIO.SetDigitalPort(4, true);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
            GPIO.SetDigitalPort(8, true);
        }
        else if (key == "Space")
        {
            gear = 0;
            GPIO.SetPWMPort(4, 0);
            GPIO.SetPWMPort(1, 0);
            GPIO.SetPWMPort(3, 0);
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, false);
            GPIO.SetDigitalPort(3, false);
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, false);
            GPIO.SetDigitalPort(8, false);
        }
        dataBus.SetData("Throttle: " + gear + " %");
        dataBus.SendData(1);
        dataBus.SetData(gear.ToString());
        dataBus.SendData(2);
        isLoop = false;
    }
}
