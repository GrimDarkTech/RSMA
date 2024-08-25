using UnityEngine;
using System.Linq;

/// <summary>
/// Implements properties and functionality of stepper motor driver
/// </summary>
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
        if(connectMicrocontroller != null)
        {
            int[] input = new int[4];

            input[0] = (int)connectMicrocontroller.GetPin(channel1Pin).value;
            input[1] = (int)connectMicrocontroller.GetPin(channel2Pin).value;
            input[2] = (int)connectMicrocontroller.GetPin(channel3Pin).value;
            input[3] = (int)connectMicrocontroller.GetPin(channel4Pin).value;

            if (Enumerable.SequenceEqual(input, new int[] { 1, 1, 0, 0 }))
            {
                outputSingal = 1;
            }
            else if (Enumerable.SequenceEqual(input, new int[] { 0, 1, 1, 0 }))
            {
                outputSingal = 2;
            }
            else if (Enumerable.SequenceEqual(input, new int[] { 0, 0, 1, 1 }))
            {
                outputSingal = 3;
            }
            else if (Enumerable.SequenceEqual(input, new int[] { 1, 0, 0, 1 }))
            {
                outputSingal = 4;
            }
            else
            {
                outputSingal = 0;
            }
        }
    }
}
