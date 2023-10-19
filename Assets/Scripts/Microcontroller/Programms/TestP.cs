using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestP : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }
    public RSMADataTransferMaster dataBus {get; set;}

    public IEnumerator MainProgramm()
    {
        float timeToWait = 1f;
        string str = "Hello world";
        yield return new WaitForSeconds(timeToWait);

        while (true)
        {
            GPIO.WritePin("PC", "13", 1);
            yield return new WaitForSeconds(timeToWait);
            GPIO.WritePin("PC", "13", 0);
            yield return new WaitForSeconds(5f);
            GPIO.WritePin("PA", "0", 1);
            GPIO.WritePin("PA", "1", 1);
            GPIO.WritePin("PA", "2", 1);
            yield return new WaitForSeconds(2f);
            GPIO.WritePin("PA", "0", 0);
            GPIO.WritePin("PA", "1", 0);
            GPIO.WritePin("PA", "2", 0);
        }
    }

}
