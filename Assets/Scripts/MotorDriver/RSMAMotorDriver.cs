using UnityEngine;
using RSMA.Microcontroller.Virtual;

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

    [Header("Virtual MCU PWM")]
    [SerializeField] private RSMAVirtualMicrocontroller virtualMicrocontroller;
    [SerializeField] private byte virtualPin1;
    [SerializeField] private byte virtualPin2;
    [SerializeField] private byte virtualPwmPin;

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
        if (virtualMicrocontroller != null)
        {
            portIn1 = virtualMicrocontroller.GetPinValue(virtualPin1) >= 0.5f ? 1f : 0f;
            portIn2 = virtualMicrocontroller.GetPinValue(virtualPin2) >= 0.5f ? 1f : 0f;
            portPWM = Mathf.Clamp01(virtualMicrocontroller.GetPinValue(virtualPwmPin));
            output = portPWM * (portIn1 - portIn2);
        }
        else if(connectMicrocontroller != null)
        {
            portIn1 = connectMicrocontroller.GetPin(connectedPin1).value;
            portIn2 = connectMicrocontroller.GetPin(connectedPin2).value;
            portPWM = connectMicrocontroller.GetPin(connectedPinPWM).value;
            output = portPWM * (portIn1 - portIn2);
        }
    }
}
