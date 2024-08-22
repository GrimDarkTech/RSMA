using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blink : MonoBehaviour, IMicrocontollerProgram
{

    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    
    public IEnumerator MainProgramm()
    {
        while (true)
        {
            GPIO.WritePin("PC", "13", RSMAGPIO.High);
            yield return new WaitForSeconds(0.1f);
            GPIO.WritePin("PC", "13", RSMAGPIO.Low);
            yield return new WaitForSeconds(0.1f);
        }
    }

}
