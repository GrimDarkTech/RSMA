using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ManualProgram : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        yield return new WaitForSeconds(0.1f);
    }
}