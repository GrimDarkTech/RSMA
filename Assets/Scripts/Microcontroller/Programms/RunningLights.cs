using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class RunningLights : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public RSMADataTransferMaster dataBus { get; set; }

    public IEnumerator MainProgramm()
    {
        string port = "PA";
        while (true)
        {
            for(int i =0; i < 9; i++)
            {
                GPIO.WritePin(port, i, RSMAGPIO.High);
                yield return new WaitForSeconds(0.5f);
                GPIO.WritePin(port, i, RSMAGPIO.Low);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
