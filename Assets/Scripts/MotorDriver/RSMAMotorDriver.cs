using UnityEngine;

/// <summary>
/// Implements properties and functionality of electric motor driver
/// </summary>
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
    private float portIn1;
    private float portIn2;
    private float portSpeed;
    private float output = 0;
    /// <summary>
    /// Microcontroller GPIO that connected to driver
    /// </summary>
    public RSMAGPIO connectMicrocontroller;
    /// <summary>
    /// Returns output signal from driver
    /// </summary>
    /// <returns></returns>
    public float getOutput()
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
    void Update()
    {
        portIn1 = connectMicrocontroller.GetPin(connectedPin1).value;
        portIn2 = connectMicrocontroller.GetPin(connectedPin2).value;
        portSpeed = connectMicrocontroller.GetPin(connectedPinPWM).value;
        output = portSpeed * (portIn1 - portIn2);
    }
}
