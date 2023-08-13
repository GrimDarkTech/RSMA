using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class testMecanum : IMicrocontollerProgramm
{
    private float range;

    public RSMAGPIO GPIO { get; set; }
    public RSMADataTransferMaster dataBus { get; set; }

    public IEnumerator MicroLoop()
    {
        yield return new WaitForSeconds(0.3f);
        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        dataBus.SendData(1);
        if(Input.GetKey(KeyCode.W))
        {
            speed2Driver(2, 3, 1, 1);
            speed2Driver(4, 5, 2, 1);
            speed2Driver(6, 7, 3, 1);
            speed2Driver(8, 9, 4, 1);
        }
        if (Input.GetKey(KeyCode.S))
        {
            speed2Driver(2, 3, 1, -1);
            speed2Driver(4, 5, 2, -1);
            speed2Driver(6, 7, 3, -1);
            speed2Driver(8, 9, 4, -1);
        }
        if (Input.GetKey(KeyCode.A))
        {
            speed2Driver(2, 3, 1, -1);
            speed2Driver(4, 5, 2, 1);
            speed2Driver(6, 7, 3, 1);
            speed2Driver(8, 9, 4, -1);
        }
        if (Input.GetKey(KeyCode.D))
        {
            speed2Driver(2, 3, 1, 1);
            speed2Driver(4, 5, 2, -1);
            speed2Driver(6, 7, 3, -1);
            speed2Driver(8, 9, 4, 1);
        }
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
