using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Was made for test new parser system
/// </summary>
public class TestP : MonoBehaviour, IMicrocontollerProgram
{
    /// <summary>
    /// Field for GPIO
    /// </summary>
    public RSMAGPIO GPIO { get; set; }
    /// <summary>
    /// Data bus stop
    /// </summary>
    public RSMADataTransferMaster dataBus {get; set;}

    /// <summary>
    /// Chislo Pi, but with more power
    /// </summary>
    public const float pi = 3.24f;

    /// <summary>
    /// Just float for cls
    /// </summary>
    public float value3 = 0f;

    /// <summary>
    /// Sum and save via value3
    /// </summary>
    /// <param name="arg1">Argum 1</param>
    /// <param name="arg2">AGRRR 2</param>
    public void Sueta(int arg1, int arg2)
    {
        value3 = arg1 + arg2;
    }

    /// <summary>
    /// Microcontroller method just for fun
    /// </summary>
    /// <remarks>Works pretty well</remarks>
    /// <returns>Returns time interupts</returns>
    public IEnumerator MainProgramm()
    {
        float timeToWait = 1f;
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
            yield return new WaitForSeconds(3f);
            GPIO.WritePin("PA", "0", 0);
            GPIO.WritePin("PA", "1", 0);
            GPIO.WritePin("PA", "2", 0);
        }
    }

}
public class SecondClassInFile
{
    public int a = 5;

    /// <summary>
    /// It's have description
    /// </summary>
    /// <remarks>sdasddas</remarks>
    public int b = 1;
}
