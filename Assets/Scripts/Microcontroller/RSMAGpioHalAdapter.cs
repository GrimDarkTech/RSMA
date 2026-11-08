using System;
using System.Collections.Generic;
using RSMA.Microcontroller.Virtual;

[Serializable]
public sealed class VirtualPinBinding
{
    public byte virtualPin;
    public ConnectedPin gpioPin;
}

public sealed class RSMAGpioHalAdapter : IVirtualMcuHal
{
    private readonly RSMAGPIO gpio;
    private readonly Dictionary<byte, ConnectedPin> bindings = new Dictionary<byte, ConnectedPin>();

    public RSMAGpioHalAdapter(RSMAGPIO gpio, IEnumerable<VirtualPinBinding> pinBindings)
    {
        this.gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        if (pinBindings == null)
            throw new ArgumentNullException(nameof(pinBindings));

        foreach (var binding in pinBindings)
        {
            if (binding == null)
                throw new ArgumentException("Pin binding cannot be null", nameof(pinBindings));
            if (bindings.ContainsKey(binding.virtualPin))
                throw new ArgumentException("Duplicate virtual GPIO pin " + binding.virtualPin, nameof(pinBindings));
            if (gpio.GetPin(binding.gpioPin) == null)
                throw new ArgumentException("Mapped RSMA GPIO pin does not exist: " +
                    binding.gpioPin.portName + "." + binding.gpioPin.pinName, nameof(pinBindings));

            bindings.Add(binding.virtualPin, binding.gpioPin);
        }
    }

    public static RSMAGpioHalAdapter CreateStm32F103Default(RSMAGPIO gpio)
    {
        return new RSMAGpioHalAdapter(gpio, new[]
        {
            new VirtualPinBinding
            {
                virtualPin = 13,
                gpioPin = new ConnectedPin { portName = "PC", pinName = "13" }
            }
        });
    }

    public void PinMode(byte pin, VirtualPinMode mode)
    {
        var gpioPin = gpio.GetPin(Resolve(pin));
        gpioPin.pinMode = mode == VirtualPinMode.Output ? PinModes.Output : PinModes.Input;
        if (mode == VirtualPinMode.Output)
            gpioPin.value = gpioPin.outputDefaulValue;
    }

    public void DigitalWrite(byte pin, bool value)
    {
        var connectedPin = Resolve(pin);
        var gpioPin = gpio.GetPin(connectedPin);
        if (gpioPin.pinMode != PinModes.Output)
            throw new InvalidOperationException("DigitalWrite requires an output pin");

        gpio.WritePin(connectedPin.portName, connectedPin.pinName, value ? RSMAGPIO.High : RSMAGPIO.Low);
    }

    public bool DigitalRead(byte pin)
    {
        return gpio.GetPin(Resolve(pin)).value >= 0.5f;
    }

    public void ApplySafeState()
    {
        foreach (var connectedPin in bindings.Values)
        {
            var pin = gpio.GetPin(connectedPin);
            if (pin != null && pin.pinMode == PinModes.Output)
                gpio.WritePin(connectedPin.portName, connectedPin.pinName, RSMAGPIO.Low);
        }
    }

    private ConnectedPin Resolve(byte pin)
    {
        if (!bindings.TryGetValue(pin, out var connectedPin))
            throw new InvalidOperationException("Virtual GPIO pin is not mapped: " + pin);
        return connectedPin;
    }
}
