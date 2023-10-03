using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// </summary>
public class Blink : MonoBehaviour, IMicrocontollerProgram
{
    /// <summary>
    /// 
    /// </summary>
    public RSMAGPIO GPIO { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public  RSMADataTransferMaster dataBus { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
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
