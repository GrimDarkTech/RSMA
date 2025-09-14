using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TestA : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        yield return new WaitForSeconds(0.1f);
        while (true)
        {

        }
        GPIO.WritePin("PA", "0", 0.5f);
    }
}