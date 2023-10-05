using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

public class Agrobot : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }

    public RSMADataTransferMaster dataBus { get; set; }

    public IEnumerator MainProgramm()
    {
        while (true)
        {
            MoveForward();
            yield return new WaitForSeconds(3f);
            Stop();
            yield return new WaitForSeconds(0.5f);
            TurnRight();
            yield return new WaitForSeconds(6f);
            Stop();
            yield return new WaitForSeconds(0.5f);
            MoveBackward();
            yield return new WaitForSeconds(3f);
            Stop();
            yield return new WaitForSeconds(0.5f);
            TurnLeft();
            yield return new WaitForSeconds(6f);
            Stop();
            yield return new WaitForSeconds(0.5f);
        }
        void Stop()
        {
            GPIO.WritePin("PA", 3, 0);
            GPIO.WritePin("PA", 6, 0);
            GPIO.WritePin("PB", 3, 0);
            GPIO.WritePin("PB", 6, 0);
            GPIO.WritePin("PC", 3, 0);
            GPIO.WritePin("PC", 6, 0);
        }
        void MoveForward()
        {
            GPIO.WritePin("PA", 1, RSMAGPIO.High);
            GPIO.WritePin("PA", 2, RSMAGPIO.Low);
            GPIO.WritePin("PA", 3, 1f);
            GPIO.WritePin("PA", 4, RSMAGPIO.High);
            GPIO.WritePin("PA", 5, RSMAGPIO.Low);
            GPIO.WritePin("PA", 6, 1f);

            GPIO.WritePin("PB", 1, RSMAGPIO.High);
            GPIO.WritePin("PB", 2, RSMAGPIO.Low);
            GPIO.WritePin("PB", 3, 1f);
            GPIO.WritePin("PB", 4, RSMAGPIO.High);
            GPIO.WritePin("PB", 5, RSMAGPIO.Low);
            GPIO.WritePin("PB", 6, 1f);

            GPIO.WritePin("PC", 1, RSMAGPIO.High);
            GPIO.WritePin("PC", 2, RSMAGPIO.Low);
            GPIO.WritePin("PC", 3, 1f);
            GPIO.WritePin("PC", 4, RSMAGPIO.High);
            GPIO.WritePin("PC", 5, RSMAGPIO.Low);
            GPIO.WritePin("PC", 6, 1f);
        }
        void MoveBackward()
        {
            GPIO.WritePin("PA", 1, RSMAGPIO.Low);
            GPIO.WritePin("PA", 2, RSMAGPIO.High);
            GPIO.WritePin("PA", 3, 1f);
            GPIO.WritePin("PA", 4, RSMAGPIO.Low);
            GPIO.WritePin("PA", 5, RSMAGPIO.High);
            GPIO.WritePin("PA", 6, 1f);

            GPIO.WritePin("PB", 1, RSMAGPIO.Low);
            GPIO.WritePin("PB", 2, RSMAGPIO.High);
            GPIO.WritePin("PB", 3, 1f);
            GPIO.WritePin("PB", 4, RSMAGPIO.Low);
            GPIO.WritePin("PB", 5, RSMAGPIO.High);
            GPIO.WritePin("PB", 6, 1f);

            GPIO.WritePin("PC", 1, RSMAGPIO.Low);
            GPIO.WritePin("PC", 2, RSMAGPIO.High);
            GPIO.WritePin("PC", 3, 1f);
            GPIO.WritePin("PC", 4, RSMAGPIO.Low);
            GPIO.WritePin("PC", 5, RSMAGPIO.High);
            GPIO.WritePin("PC", 6, 1f);
        }
        void TurnRight()
        {
            GPIO.WritePin("PA", 1, RSMAGPIO.High);
            GPIO.WritePin("PA", 2, RSMAGPIO.Low);
            GPIO.WritePin("PA", 3, 1f);
            GPIO.WritePin("PA", 4, RSMAGPIO.Low);
            GPIO.WritePin("PA", 5, RSMAGPIO.High);
            GPIO.WritePin("PA", 6, 1f);

            GPIO.WritePin("PB", 1, RSMAGPIO.High);
            GPIO.WritePin("PB", 2, RSMAGPIO.Low);
            GPIO.WritePin("PB", 3, 1f);
            GPIO.WritePin("PB", 4, RSMAGPIO.Low);
            GPIO.WritePin("PB", 5, RSMAGPIO.High);
            GPIO.WritePin("PB", 6, 1f);

            GPIO.WritePin("PC", 1, RSMAGPIO.High);
            GPIO.WritePin("PC", 2, RSMAGPIO.Low);
            GPIO.WritePin("PC", 3, 1f);
            GPIO.WritePin("PC", 4, RSMAGPIO.Low);
            GPIO.WritePin("PC", 5, RSMAGPIO.High);
            GPIO.WritePin("PC", 6, 1f);
        }
        void TurnLeft()
        {
            GPIO.WritePin("PA", 1, RSMAGPIO.Low);
            GPIO.WritePin("PA", 2, RSMAGPIO.High);
            GPIO.WritePin("PA", 3, 1f);
            GPIO.WritePin("PA", 4, RSMAGPIO.High);
            GPIO.WritePin("PA", 5, RSMAGPIO.Low);
            GPIO.WritePin("PA", 6, 1f);

            GPIO.WritePin("PB", 1, RSMAGPIO.Low);
            GPIO.WritePin("PB", 2, RSMAGPIO.High);
            GPIO.WritePin("PB", 3, 1f);
            GPIO.WritePin("PB", 4, RSMAGPIO.High);
            GPIO.WritePin("PB", 5, RSMAGPIO.Low);
            GPIO.WritePin("PB", 6, 1f);

            GPIO.WritePin("PC", 1, RSMAGPIO.Low);
            GPIO.WritePin("PC", 2, RSMAGPIO.High);
            GPIO.WritePin("PC", 3, 1f);
            GPIO.WritePin("PC", 4, RSMAGPIO.High);
            GPIO.WritePin("PC", 5, RSMAGPIO.Low);
            GPIO.WritePin("PC", 6, 1f);
        }
    }
}
