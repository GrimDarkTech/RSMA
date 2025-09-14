using UnityEngine;

/// <summary>
/// Implements properties and functionality of electric motor driver
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_motor_drivers.md")]
public class RSMAMotorDriver : MonoBehaviour
{
    /// <summary>
    /// GPIO port pin connected to driver's 1st input
    /// </summary>
    public ConnectedPin connectedPin1;
    /// <summary>
    /// GPIO port pin connected to driver's 2st input
    /// </summary>
    public ConnectedPin connectedPin2;
    /// <summary>
    /// GPIO port pin connected to driver's PWM input
    /// </summary>
    public ConnectedPin connectedPinPWM;

    /// <summary>
    /// Microcontroller GPIO that connected to driver
    /// </summary>
    public RSMAGPIO connectMicrocontroller;

    private float portIn1;
    private float portIn2;
    private float portPWM;
    private float output = 0;

    /// <summary>
    /// Returns output signal from driver
    /// </summary>
    /// <returns>float value of output signal from driver</returns>
    public float GetOutput()
    {
        return output;
    }
    private int boolToInt(bool boolValue)
    {
        if (boolValue)
            return 1;
        else
            return 0;
    }

    private void Update()
    {
        if(connectMicrocontroller != null)
        {
            portIn1 = connectMicrocontroller.GetPin(connectedPin1).value;
            portIn2 = connectMicrocontroller.GetPin(connectedPin2).value;
            portPWM = connectMicrocontroller.GetPin(connectedPinPWM).value;
            output = portPWM * (portIn1 - portIn2);
        }
    }
}
