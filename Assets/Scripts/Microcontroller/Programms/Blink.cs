using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Blink : MonoBehaviour, IMicrocontollerProgramm
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }

    public IEnumerator MainProgramm()
    {
        while (true)
        {
            GPIO.SetDigitalPort(1, true);
            yield return new WaitForSeconds(0.1f);
            GPIO.SetDigitalPort(1, false);
            yield return new WaitForSeconds(0.1f);
        }
    }

}
