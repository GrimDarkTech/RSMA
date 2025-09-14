using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Stepper : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        yield return new WaitForSeconds(2f);

        dataBus.ReciveData(0);

        float angle = float.Parse(dataBus.data) * 6;

        Debug.Log("Encoder rotation: " + angle);

        int steps = (int)(angle * 0.1388f);


        yield return new WaitForSeconds(0.1f);

        if(steps > 0) 
        {
            for (int i = 0; i < steps; i++)
            {
                GPIO.WritePin("PB", "13", 1);
                GPIO.WritePin("PB", "14", 1);
                GPIO.WritePin("PA", "10", 0);
                GPIO.WritePin("PA", "11", 0);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 1);
                GPIO.WritePin("PB", "14", 0);
                GPIO.WritePin("PA", "10", 0);
                GPIO.WritePin("PA", "11", 1);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 0);
                GPIO.WritePin("PB", "14", 0);
                GPIO.WritePin("PA", "10", 1);
                GPIO.WritePin("PA", "11", 1);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 0);
                GPIO.WritePin("PB", "14", 1);
                GPIO.WritePin("PA", "10", 1);
                GPIO.WritePin("PA", "11", 0);
                yield return new WaitForSeconds(0.05f);
            }
        }
        else
        {
            steps *= -1; 
            for (int i = 0; i < steps; i++)
            {
                GPIO.WritePin("PB", "13", 1);
                GPIO.WritePin("PB", "14", 1);
                GPIO.WritePin("PA", "10", 0);
                GPIO.WritePin("PA", "11", 0);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 0);
                GPIO.WritePin("PB", "14", 1);
                GPIO.WritePin("PA", "10", 1);
                GPIO.WritePin("PA", "11", 0);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 0);
                GPIO.WritePin("PB", "14", 0);
                GPIO.WritePin("PA", "10", 1);
                GPIO.WritePin("PA", "11", 1);
                yield return new WaitForSeconds(0.05f);
                GPIO.WritePin("PB", "13", 1);
                GPIO.WritePin("PB", "14", 0);
                GPIO.WritePin("PA", "10", 0);
                GPIO.WritePin("PA", "11", 1);
                yield return new WaitForSeconds(0.05f);
            }
        }


        GPIO.WritePin("PB", "13", 0);
        GPIO.WritePin("PB", "14", 0);
        GPIO.WritePin("PA", "10", 0);
        GPIO.WritePin("PA", "11", 0);
    }
}