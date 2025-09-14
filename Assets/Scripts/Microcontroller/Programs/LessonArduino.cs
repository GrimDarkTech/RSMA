using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class LessonArduino : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        yield return new WaitForSeconds(0.1f);
        dataBus.data = "Hello RSMA!";

        dataBus.SendData(0);

        yield return new WaitForSeconds(3f);

        dataBus.data = "012345";

        dataBus.SendData(0);

        while (true) 
        {
            GPIO.WritePin("PB", "1", 1);
            yield return new WaitForSeconds(0.5f);
            GPIO.WritePin("PB", "1", 0);
            yield return new WaitForSeconds(0.5f);
        }

    }
}