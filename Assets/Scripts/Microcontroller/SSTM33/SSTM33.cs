using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSTM33 : MicrocontrollerBase
{
    private float range;
    private string keyBoardCode;
    private string avAT;
    private string avHM;
    private float time = 0;
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(.1f);
        dataBus.SetData("CustomMotor; HingeJointMotor; Time");
        dataBus.SendData(3);
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
        yield return new WaitForSeconds(0.2f);
        time = time + 0.2f;
        dataBus.ReciveData(5);
        avAT = dataBus.GetData();
        dataBus.ReciveData(4);
        avHM = dataBus.GetData();
        dataBus.SetData(avAT + ";"+ avHM + ";" + time);
        dataBus.SendData(3);

        dataBus.ReciveData(0);
        range = float.Parse(dataBus.GetData());
        dataBus.SendData(1);
        dataBus.ReciveData(2);
        keyBoardCode = dataBus.GetData();
        if(keyBoardCode == "W")
        {
            GPIO.SetDigitalPort(4, true);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, true);
        }
        else if(keyBoardCode == "S")
        {
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, true);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
        }
        else if (keyBoardCode == "A")
        {
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, true);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, true);
        }
        else if (keyBoardCode == "D")
        {
            GPIO.SetDigitalPort(4, true);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, true);
            GPIO.SetDigitalPort(7, false);
        }
        else if (keyBoardCode == "Space")
        {
            GPIO.SetDigitalPort(4, false);
            GPIO.SetDigitalPort(5, false);
            GPIO.SetDigitalPort(6, false);
            GPIO.SetDigitalPort(7, false);
        }
        else if (keyBoardCode == "LeftArrow")
        {
            GPIO.SetDigitalPort(1, true);
            GPIO.SetDigitalPort(2, false);
        }
        else if (keyBoardCode == "RightArrow")
        {
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, true);
        }
        else if (keyBoardCode == "UpArrow")
        {
            GPIO.SetDigitalPort(1, false);
            GPIO.SetDigitalPort(2, false);
        }

        if (range < 4f)
            GPIO.SetDigitalPort(3, true);
        else
            GPIO.SetDigitalPort(3, false);

        isLoop = false;
    }
}
