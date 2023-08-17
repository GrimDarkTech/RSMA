using Microsoft.Cci;
using System.Collections.Generic;
using UnityEngine;


public class RSMAGPIO : MonoBehaviour
{
    public const float High = 1;
    public const float Low = 2;
    [SerializeField] private List<GPIOPort> ports = new List<GPIOPort>();
    
    public void WritePin(string portName, string pinName, float value)
    {
        GPIOPin pin2Write = null;
        foreach (var port in ports)
        {
            if(port.name == portName)
            {
                foreach (var pin in port.pins)
                {
                    if (pin.name == pinName)
                    {
                        pin2Write = pin;
                        break;
                    }
                }
                break;
            }
        }
        if(pin2Write != null)
        {
            pin2Write.value = value;
        }
    }
    public void WritePin(int portIndex, int pinIndex, float value)
    {
        if(portIndex < ports.Count)
        {
            if (pinIndex < ports[portIndex].pins.Count)
            {
                GPIOPin pin2Write = ports[portIndex].pins[pinIndex];
                if (pin2Write != null)
                {
                    pin2Write.value = value;
                }
            }
        }
    }
    public void WritePin(int portIndex, string pinName, float value)
    {
        GPIOPin pin2Write = null;
        if (portIndex < ports.Count)
        {
            foreach (var pin in ports[portIndex].pins)
            {
                if (pin.name == pinName)
                {
                    pin2Write = pin;
                    break;
                }
            }
        }
        if (pin2Write != null)
        {
            pin2Write.value = value;
        }
    }
    public void WritePin(string portName, int pinIndex, float value)
    {
        GPIOPin pin2Write = null;
        foreach (var port in ports)
        {
            if (port.name == portName)
            {
                pin2Write = port.pins[pinIndex];
                break;
            }
        }
        if (pin2Write != null)
        {
            pin2Write.value = value;
        }
    }
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

    public void ResetAll()
    {
        foreach (var port in ports)
        {
            foreach (var pin in port.pins)
            {
                pin.value = 0;
            }
        }
    }
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
