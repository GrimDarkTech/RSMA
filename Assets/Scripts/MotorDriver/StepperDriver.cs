using UnityEngine;
using System.Linq;
using RSMA.Microcontroller.Virtual;

/// <summary>
/// Implements properties and functionality of stepper motor driver
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_motor_drivers.md")]
public class StepperDriver : MonoBehaviour
{
    /// <summary>
    /// GPIO port pin connected to driver's 1st input
    /// </summary>
    public ConnectedPin channel1Pin;
    /// <summary>
    /// GPIO port pin connected to driver's 2st input
    /// </summary>
    public ConnectedPin channel2Pin;
    /// <summary>
    /// GPIO port pin connected to driver's 3rd input
    /// </summary>
    public ConnectedPin channel3Pin;
    /// <summary>
    /// GPIO port pin connected to driver's 4th input
    /// </summary>
    public ConnectedPin channel4Pin;

    /// <summary>
    /// Microcontroller GPIO that connected to driver
    /// </summary>
    public RSMAGPIO connectMicrocontroller;

    [Header("Virtual MCU GPIO")]
    [SerializeField] private RSMAVirtualMicrocontroller virtualMicrocontroller;
    [SerializeField] private byte virtualChannel1Pin;
    [SerializeField] private byte virtualChannel2Pin;
    [SerializeField] private byte virtualChannel3Pin;
    [SerializeField] private byte virtualChannel4Pin;

    private int outputSingal;

    /// <summary>
    /// Output signal value
    /// </summary>
    public int output 
    {
        get { return outputSingal; }
    }


    private void Update()
    {
        if (virtualMicrocontroller != null)
        {
            UpdateOutput(
                virtualMicrocontroller.GetPinValue(virtualChannel1Pin),
                virtualMicrocontroller.GetPinValue(virtualChannel2Pin),
                virtualMicrocontroller.GetPinValue(virtualChannel3Pin),
                virtualMicrocontroller.GetPinValue(virtualChannel4Pin));
        }
        else if(connectMicrocontroller != null)
        {
            UpdateOutput(
                connectMicrocontroller.GetPin(channel1Pin).value,
                connectMicrocontroller.GetPin(channel2Pin).value,
                connectMicrocontroller.GetPin(channel3Pin).value,
                connectMicrocontroller.GetPin(channel4Pin).value);
        }
    }

    private void UpdateOutput(float channel1, float channel2, float channel3, float channel4)
    {
        var input = new[]
        {
            channel1 >= 0.5f ? 1 : 0,
            channel2 >= 0.5f ? 1 : 0,
            channel3 >= 0.5f ? 1 : 0,
            channel4 >= 0.5f ? 1 : 0
        };

        if (Enumerable.SequenceEqual(input, new[] { 1, 1, 0, 0 })) outputSingal = 1;
        else if (Enumerable.SequenceEqual(input, new[] { 0, 1, 1, 0 })) outputSingal = 2;
        else if (Enumerable.SequenceEqual(input, new[] { 0, 0, 1, 1 })) outputSingal = 3;
        else if (Enumerable.SequenceEqual(input, new[] { 1, 0, 0, 1 })) outputSingal = 4;
        else outputSingal = 0;
    }
}
