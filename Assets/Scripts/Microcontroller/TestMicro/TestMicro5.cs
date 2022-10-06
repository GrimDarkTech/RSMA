using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMicro5 : MicrocontrollerBase
{
    private float range;
    private float vertical;
    private float horizontal;
    private string[] axes = { "0", "0" };
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

        dataBus.ReciveData(2);
        axes = dataBus.GetData().Split(' ');
        horizontal = float.Parse(axes[0]);
        vertical = float.Parse(axes[1]);
        if (horizontal == 0 && vertical == 0)
        {
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
        }
        else
        {
            if (vertical > 0)
            {
                MoveForward(Mathf.Abs(vertical), horizontal);
            }
            else if (vertical < 0)
            {
                MoveBackward(Mathf.Abs(vertical), horizontal);
            }
            else if (horizontal < 0)
            {
                TurnLeft(Mathf.Abs(horizontal));
            }
            else if (horizontal > 0)
            {
                TurnRight(Mathf.Abs(horizontal)); 
            }

        }
        yield return new WaitForSeconds(0.5f);
        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        dataBus.SetData("Range: " + range);
        dataBus.SendData(1);

        isLoop = false;
    }

    void TurnLeft(float speed)
    {
        GPIO.SetPWMPort(0, speed);
        GPIO.SetPWMPort(1, speed);
        GPIO.SetPWMPort(2, speed);
        GPIO.SetPWMPort(3, speed);
        GPIO.SetDigitalPort(0, true);
        GPIO.SetDigitalPort(1, false);
        GPIO.SetDigitalPort(4, true);
        GPIO.SetDigitalPort(5, false);
        GPIO.SetDigitalPort(2, false);
        GPIO.SetDigitalPort(3, true);
        GPIO.SetDigitalPort(6, false);
        GPIO.SetDigitalPort(7, true);

    }
    void TurnRight(float speed)
    {
        GPIO.SetPWMPort(0, speed);
        GPIO.SetPWMPort(1, speed);
        GPIO.SetPWMPort(2, speed);
        GPIO.SetPWMPort(3, speed);
        GPIO.SetDigitalPort(0, false);
        GPIO.SetDigitalPort(1, true);
        GPIO.SetDigitalPort(4, false);
        GPIO.SetDigitalPort(5, true);
        GPIO.SetDigitalPort(2, true);
        GPIO.SetDigitalPort(3, false);
        GPIO.SetDigitalPort(6, true);
        GPIO.SetDigitalPort(7, false);
    }
    void MoveForward(float speed, float horSpeed)
    {
        GPIO.SetPWMPort(0, speed - horSpeed);
        GPIO.SetPWMPort(1, speed + horSpeed);
        GPIO.SetPWMPort(2, speed - horSpeed);
        GPIO.SetPWMPort(3, speed + horSpeed);
        GPIO.SetDigitalPort(0, true);
        GPIO.SetDigitalPort(1, false);
        GPIO.SetDigitalPort(4, true);
        GPIO.SetDigitalPort(5, false);
        GPIO.SetDigitalPort(2, true);
        GPIO.SetDigitalPort(3, false);
        GPIO.SetDigitalPort(6, true);
        GPIO.SetDigitalPort(7, false);
    }
    void MoveBackward(float speed, float horSpeed)
    {
        GPIO.SetPWMPort(0, speed - horSpeed);
        GPIO.SetPWMPort(1, speed + horSpeed);
        GPIO.SetPWMPort(2, speed - horSpeed);
        GPIO.SetPWMPort(3, speed + horSpeed);
        GPIO.SetDigitalPort(0, false);
        GPIO.SetDigitalPort(1, true);
        GPIO.SetDigitalPort(4, false);
        GPIO.SetDigitalPort(5, true);
        GPIO.SetDigitalPort(2, false);
        GPIO.SetDigitalPort(3, true);
        GPIO.SetDigitalPort(6, false);
        GPIO.SetDigitalPort(7, true);
    }
}
