using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSTM33 : MicrocontrollerBaseScript
{
    private float range;
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

        if (range < 4f)
        {
            if (GPIO.getDigitalPort(1) && !GPIO.getDigitalPort(2))
            {
                GPIO.setDigitalPort(3, true);
                GPIO.setPWMPort(1, 0.2f);
                GPIO.setDigitalPort(1, false);
                GPIO.setDigitalPort(2, true);
            }
            else
            {
                GPIO.setDigitalPort(3, true);
                GPIO.setPWMPort(1, 0.2f);
                GPIO.setDigitalPort(1, true);
                GPIO.setDigitalPort(2, false);
            }

        }
        else
        {
            GPIO.setDigitalPort(3, false);
            GPIO.setPWMPort(1, 0.8f);
        }
        isLoop = false;
    }
}
