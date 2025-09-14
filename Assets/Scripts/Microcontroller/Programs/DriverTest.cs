using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class DriverTest : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        yield return new WaitForSeconds(2.1f);
        for (int i = 0; i < 100; i++)
        {
            GPIO.WritePin("PA", "1", 1);
            GPIO.WritePin("PA", "2", 1);
            GPIO.WritePin("PA", "3", 0);
            GPIO.WritePin("PA", "4", 0);
            yield return new WaitForSeconds(0.2f);
            GPIO.WritePin("PA", "1", 1);
            GPIO.WritePin("PA", "2", 0);
            GPIO.WritePin("PA", "3", 0);
            GPIO.WritePin("PA", "4", 1);
            yield return new WaitForSeconds(0.2f);
            GPIO.WritePin("PA", "1", 0);
            GPIO.WritePin("PA", "2", 0);
            GPIO.WritePin("PA", "3", 1);
            GPIO.WritePin("PA", "4", 1);
            yield return new WaitForSeconds(0.2f);
            GPIO.WritePin("PA", "1", 0);
            GPIO.WritePin("PA", "2", 1);
            GPIO.WritePin("PA", "3", 1);
            GPIO.WritePin("PA", "4", 0);
            yield return new WaitForSeconds(0.2f);
        }
    }
}