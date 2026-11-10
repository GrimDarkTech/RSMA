using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Implements properties and functionality of GPIO
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_microcontrollers.md")]
public class RSMAGPIO : MonoBehaviour
{
    /// <summary>
    /// Constant value of GPIO port pin in active state
    /// </summary>
    public const float High = 1;
    /// <summary>
    /// Constant value of GPIO port pin in inactive state
    /// </summary>
    public const float Low = 0;
    
    [SerializeField] private List<GPIOPort> ports = new List<GPIOPort>();

    /// <summary>
    /// Returns GPIO ports list
    /// </summary>
    /// <returns>List of ports</returns>
    public List<GPIOPort> GetPortList()
    { 
        return ports; 
    }

    /// <summary>
    /// Toggles output value of GPIO port pin 
    /// </summary>
    /// <param name="portName">GPIO port name</param>
    /// <param name="pinName">GPIO pin name</param>
    /// <param name="value">GPIO port pin value</param>
    /// <remarks>Works with pins in output mode only</remarks>
    public void WritePin(string portName, string pinName, float value)
    {
        WritePin(GetPin(portName, pinName), value);
    }
    /// <summary>
    /// Toggles output value of GPIO port pin 
    /// </summary>
    /// <param name="portIndex">GPIO port index</param>
    /// <param name="pinIndex">GPIO pin index</param>
    /// <param name="value">GPIO port pin value</param>
    /// <remarks>Works with pins in output mode only</remarks>
    public void WritePin(int portIndex, int pinIndex, float value)
    {
        if (portIndex < 0 || portIndex >= ports.Count ||
            pinIndex < 0 || pinIndex >= ports[portIndex].pins.Count)
            return;

        WritePin(ports[portIndex].pins[pinIndex], value);
    }
    /// <summary>
    /// Toggles output value of GPIO port pin 
    /// </summary>
    /// <param name="portIndex">GPIO port index</param>
    /// <param name="pinName">GPIO pin name</param>
    /// <param name="value">GPIO port pin value</param>
    /// <remarks>Works with pins in output mode only</remarks>
    public void WritePin(int portIndex, string pinName, float value)
    {
        if (portIndex < 0 || portIndex >= ports.Count)
            return;

        WritePin(ports[portIndex].pins.Find(pin => pin.name == pinName), value);
    }
    /// <summary>
    /// Toggles output value of GPIO port pin
    /// </summary>
    /// <param name="portName">GPIO port name</param>
    /// <param name="pinIndex">GPIO pin index</param>
    /// <param name="value">GPIO port pin value</param>
    /// <remarks>Works with pins in output mode only</remarks>
    public void WritePin(string portName, int pinIndex, float value)
    {
        GPIOPin pin2Write = null;
        foreach (var port in ports)
        {
            if (port.name == portName)
            {
                if (pinIndex < 0 || pinIndex >= port.pins.Count)
                    return;
                pin2Write = port.pins[pinIndex];
                break;
            }
        }
        WritePin(pin2Write, value);
    }

    private void WritePin(GPIOPin pin, float value)
    {
        if (pin == null)
            return;
        if (pin.pinMode != PinModes.Output)
        {
            Debug.LogWarning($"GPIO {gameObject.name} can't set pin state, {pin.name} mode is Input");
            return;
        }

        pin.value = value;
    }
    /// <summary>
    /// Gets GPIO port pin object by port name and pin name
    /// </summary>
    /// <param name="portName">GPIO port name</param>
    /// <param name="pinName">GPIO pin name</param>
    /// <returns>Returns GPIOPin ref if it exists, else returns null</returns>
    public GPIOPin GetPin(string portName, string pinName)
    {
        GPIOPin pin2Get = null;
        foreach (var port in ports)
        {
            if (port.name == portName)
            {
                foreach (var pin in port.pins)
                {
                    if (pin.name == pinName)
                    {
                        pin2Get = pin;
                        break;
                    }
                }
                break;
            }
        }
        return pin2Get;
    }
    /// <summary>
    /// Gets GPIO port pin object by ConnectedPin enum
    /// </summary>
    /// <param name="connectedPin">Connected GPIO port pin description</param>
    /// <returns>Returns GPIOPin ref if it exists, else returns null</returns>
    public GPIOPin GetPin(ConnectedPin connectedPin)
    {
        GPIOPin pin2Get = null;
        foreach (var port in ports)
        {
            if (port.name == connectedPin.portName)
            {
                foreach (var pin in port.pins)
                {
                    if (pin.name == connectedPin.pinName)
                    {
                        pin2Get = pin;
                        break;
                    }
                }
                break;
            }
        }
        return pin2Get;
    }
    /// <summary>
    /// Sets default GPIO ports pins values
    /// </summary>
    public void ResetAll()
    {
        foreach (var port in ports)
        {
            foreach (var pin in port.pins)
            {
                if(pin.pinMode == PinModes.Output)
                {
                    pin.value = pin.outputDefaulValue;
                }
                else
                {
                    pin.value = 0f;
                }
            }
        }
    }
    /// <summary>
    /// Sets GPIO ports pins values to 0
    /// </summary>
    public void TurnOffAll()
    {
        foreach (var port in ports)
        {
            foreach (var pin in port.pins)
            {
                    pin.value = 0f;
            }
        }
    }
    /// <summary>
    /// Read input value of GPIO port pin 
    /// </summary>
    /// <param name="portName">GPIO port name</param>
    /// <param name="pinName">GPIO pin name</param>
    /// <returns>Returns float GPIO port pin value</returns>
    public float ReadPin(string portName, string pinName)
    {
        GPIOPin pin2Read = null;
        foreach (var port in ports)
        {
            if (port.name == portName)
            {
                foreach (var pin in port.pins)
                {
                    if (pin.name == pinName)
                    {
                        pin2Read = pin;
                        break;
                    }
                }
                break;
            }
        }
        return pin2Read.value;
    }
    /// <summary>
    /// Read input value of GPIO port pin 
    /// </summary>
    /// <param name="portIndex">GPIO port name</param>
    /// <param name="pinIndex">GPIO pin name</param>
    /// <returns>Returns float GPIO port pin value</returns>
    public float ReadPin(int portIndex, int pinIndex)
    {
        GPIOPin pin2Read = null;
        if (portIndex < ports.Count)
        {
            if (pinIndex < ports[portIndex].pins.Count)
            {
                pin2Read = ports[portIndex].pins[pinIndex];
            }
        }
        return pin2Read.value;
    }
    /// <summary>
    /// Read input value of GPIO port pin 
    /// </summary>
    /// <param name="portIndex">GPIO port name</param>
    /// <param name="pinName">GPIO pin name</param>
    /// <returns>Returns float GPIO port pin value</returns>
    public float ReadPin(int portIndex, string pinName)
    {
        GPIOPin pin2Read = null;
        if (portIndex < ports.Count)
        {
            foreach (var pin in ports[portIndex].pins)
            {
                if (pin.name == pinName)
                {
                    pin2Read = pin;
                    break;
                }
            }
        }
        return pin2Read.value;
    }
    /// <summary>
    /// Read input value of GPIO port pin 
    /// </summary>
    /// <param name="portName">GPIO port name</param>
    /// <param name="pinIndex">GPIO pin name</param>
    /// <returns>Returns float GPIO port pin value</returns>
    public float ReadPin(string portName, int pinIndex)
    {
        GPIOPin pin2Read = null;
        foreach (var port in ports)
        {
            if (port.name == portName)
            {
                pin2Read = port.pins[pinIndex];
                break;
            }
        }
        return pin2Read.value;
    }
}
