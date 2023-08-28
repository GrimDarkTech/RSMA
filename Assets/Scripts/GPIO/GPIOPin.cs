using System;

/// <summary>
/// Implements properties and functionality of GPIO port pin
/// </summary>
[Serializable]
public class GPIOPin
{
    /// <summary>
    /// GPIO port pin name
    /// </summary>
    public string name;

    /// <summary>
    /// GPIO port pin state
    /// </summary>
    public float value;

    /// <summary>
    /// GPIO port pin mode
    /// </summary>
    /// <remarks>Output is default</remarks>
    public PinModes pinMode;

    /// <summary>
    /// GPIO port pin pull-up/pull-down resistor mode
    /// </summary>
    /// <remarks>No pull-up/down is default</remarks>
    public PinPullModes pinPullMode;

    /// <summary>
    /// GPIO port pin output default state         
    /// </summary>
    /// <remarks>Works in output mode only</remarks>
    public float outputDefaulValue;

    /// <summary>
    /// GPIO port pin output mode       
    /// </summary>
    /// <remarks>Push-pull is default. Works in output mode only</remarks>
    public PinOutputModes pinOutputMode;
}
public enum PinModes
{
    Output,
    Input
}
public enum PinPullModes
{
    NoPullUpDown,
    PullUp,
    PullDown
}

public enum PinOutputModes
{
    PushPull,
    OpenDrain
}