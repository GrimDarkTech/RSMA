using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSTM33 : MicrocontrollerBaseScript
{
    private float range;
    private string keyBoardCode;
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
        I2CBus.reciveData(0);
        range = float.Parse(I2CBus.getData());
        I2CBus.sendData(1);
        I2CBus.reciveData(2);
        keyBoardCode = I2CBus.getData();
        if(keyBoardCode == "W")
        {
            GPIO.setDigitalPort(4, true);
            GPIO.setDigitalPort(5, false);
            GPIO.setDigitalPort(6, false);
            GPIO.setDigitalPort(7, true);
        }
        else if(keyBoardCode == "S")
        {
            GPIO.setDigitalPort(4, false);
            GPIO.setDigitalPort(5, true);
            GPIO.setDigitalPort(6, true);
            GPIO.setDigitalPort(7, false);
        }
        else if (keyBoardCode == "A")
        {
            GPIO.setDigitalPort(4, false);
            GPIO.setDigitalPort(5, true);
            GPIO.setDigitalPort(6, false);
            GPIO.setDigitalPort(7, true);
        }
        else if (keyBoardCode == "D")
        {
            GPIO.setDigitalPort(4, true);
            GPIO.setDigitalPort(5, false);
            GPIO.setDigitalPort(6, true);
            GPIO.setDigitalPort(7, false);
        }
        else if (keyBoardCode == "Space")
        {
            GPIO.setDigitalPort(4, false);
            GPIO.setDigitalPort(5, false);
            GPIO.setDigitalPort(6, false);
            GPIO.setDigitalPort(7, false);
        }
        if (range < 4f)
            GPIO.setDigitalPort(3, true);
        else
            GPIO.setDigitalPort(3, false);

        isLoop = false;
    }
}
