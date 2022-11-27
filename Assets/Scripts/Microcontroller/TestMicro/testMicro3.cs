using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testMicro3 : MicrocontrollerBase
{
    Vector2 imputValue;
    string[] axes = { "0", "0" };
    float vheel1Velocity;
    float vheel2Velocity;
    float vheel3Velocity;
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
        dataBus.ReciveData(0);
        axes = dataBus.GetData().Split(' ');
        imputValue.x = float.Parse(axes[0]);
        imputValue.y = float.Parse(axes[1]);

        vheel1Velocity = imputValue.x - (imputValue.y / 1.73205f);
        vheel2Velocity = imputValue.x + (imputValue.y / 1.73205f);
        
        speed2Driver(2, 3, 1, vheel1Velocity);
        speed2Driver(4, 5, 2, vheel2Velocity);
        speed2Driver(6, 7, 3, vheel3Velocity);

        isLoop = false;
    }

    void speed2Driver(int firstDigital, int secondDigital, int PWM, float speed)
    {
        if (speed > 0)
        {
            GPIO.SetDigitalPort(firstDigital, true);
            GPIO.SetDigitalPort(secondDigital, false);
            GPIO.SetPWMPort(PWM, Mathf.Abs(speed));
        }
        else if (speed < 0)
        {
            GPIO.SetDigitalPort(firstDigital, false);
            GPIO.SetDigitalPort(secondDigital, true);
            GPIO.SetPWMPort(PWM, Mathf.Abs(speed));
        }
        else if (speed == 0)
        {
            GPIO.SetDigitalPort(firstDigital, false);
            GPIO.SetDigitalPort(secondDigital, false);
            GPIO.SetPWMPort(PWM, Mathf.Abs(speed));
        }
    }
}


