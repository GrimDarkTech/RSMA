using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class RobotA : MonoBehaviour, IMicrocontollerProgram
{
    public RSMAGPIO GPIO { get; set; }
    public  RSMADataTransferMaster dataBus { get; set; }
    public IEnumerator MainProgramm()
    {
        void SetMotorA(float angularVelocity)
        {
            if(angularVelocity == 0)
            {
                GPIO.WritePin("PA", "1", RSMAGPIO.High);
                GPIO.WritePin("PA", "2", RSMAGPIO.High);
                GPIO.WritePin("PA", "3", 0f);
            }
            if(angularVelocity > 0)
            {
                GPIO.WritePin("PA", "1", RSMAGPIO.High);
                GPIO.WritePin("PA", "2", RSMAGPIO.Low);
                GPIO.WritePin("PA", "3", angularVelocity);
            }
            if (angularVelocity < 0)
            {
                GPIO.WritePin("PA", "1", RSMAGPIO.Low);
                GPIO.WritePin("PA", "2", RSMAGPIO.High);
                GPIO.WritePin("PA", "3", angularVelocity);
            }
        }
        void SetMotorB(float angularVelocity)
        {
            if (angularVelocity == 0)
            {
                GPIO.WritePin("PB", "1", RSMAGPIO.High);
                GPIO.WritePin("PB", "2", RSMAGPIO.High);
                GPIO.WritePin("PB", "3", 0f);
            }
            if (angularVelocity > 0)
            {
                GPIO.WritePin("PB", "1", RSMAGPIO.High);
                GPIO.WritePin("PB", "2", RSMAGPIO.Low);
                GPIO.WritePin("PB", "3", angularVelocity);
            }
            if (angularVelocity < 0)
            {
                GPIO.WritePin("PB", "1", RSMAGPIO.Low);
                GPIO.WritePin("PB", "2", RSMAGPIO.High);
                GPIO.WritePin("PB", "3", angularVelocity);
            }
        }
        while (true)
        {
            SetMotorA(0f);
            SetMotorB(0f);
            yield return new WaitForSeconds(2f);
            SetMotorA(0.6f);
            SetMotorB(0.6f);
            yield return new WaitForSeconds(2f);
            SetMotorA(0f);
            SetMotorB(0f);
            yield return new WaitForSeconds(2f);
            SetMotorA(-0.5f);
            SetMotorB(-0.5f);
            yield return new WaitForSeconds(2f);
            SetMotorA(0.5f);
            SetMotorB(0.5f);
            yield return new WaitForSeconds(2f);
        }

        

    }
}