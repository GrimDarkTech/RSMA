using System;
using System.Collections.Generic;
using RSMA.Microcontroller.Virtual;

[Serializable]
public sealed class VirtualPinBinding
{
    public byte virtualPin;
    public ConnectedPin gpioPin;
    public PinPullModes pullMode;
    public PinOutputModes outputMode;
    public PinModes resetMode = PinModes.Input;
    public bool resetLatchLevel;
}

public sealed class RSMAGpioHalAdapter : IVirtualMcuHal, IVirtualInputSource, IVirtualButtonHost
{
    private readonly RSMAGPIO gpio;
    private readonly VirtualBoardProfile profile;
    private readonly Dictionary<byte, ConnectedPin> bindings = new Dictionary<byte, ConnectedPin>();
    private readonly Dictionary<byte, VirtualGpioPinState> states = new Dictionary<byte, VirtualGpioPinState>();
    private readonly List<ScheduledInput> scheduledInputs = new List<ScheduledInput>();
    private ulong nextInputSequence;

    private sealed class ScheduledInput
    {
        public byte Pin;
        public bool Level;
        public double Timestamp;
        public ulong Sequence;
    }

    public event Action<VirtualGpioInputEvent> InputEdge;
    public double? NextInputTimestampMilliseconds => scheduledInputs.Count == 0
        ? (double?)null : scheduledInputs[0].Timestamp;

    public RSMAGpioHalAdapter(RSMAGPIO gpio, IEnumerable<VirtualPinBinding> pinBindings)
        : this(gpio, CreateProfileFromBindings(pinBindings))
    {
    }

    public RSMAGpioHalAdapter(RSMAGPIO gpio, VirtualBoardProfile profile)
    {
        this.gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        foreach (var definition in profile.PinDefinitions)
        {
            var connectedPin = new ConnectedPin { portName = definition.PortName, pinName = definition.PinName };
            if (gpio.GetPin(connectedPin) == null)
                throw new BoardProfileException("Board '" + profile.BoardId + "' maps missing RSMA GPIO pin " +
                    definition.PortName + "." + definition.PinName);

            bindings.Add(definition.VirtualPin, connectedPin);
            states.Add(definition.VirtualPin, new VirtualGpioPinState
            {
                VirtualPin = definition.VirtualPin,
                Mode = definition.ResetMode,
                PullMode = ToVirtualPullMode(gpio.GetPin(connectedPin).pinPullMode),
                OutputType = ToVirtualOutputType(gpio.GetPin(connectedPin).pinOutputMode),
                LatchLevel = definition.ResetLatchLevel,
                LineLevel = gpio.GetPin(connectedPin).value >= 0.5f
            });
        }

        ApplyResetState();
    }

    public static RSMAGpioHalAdapter CreateStm32F103Default(RSMAGPIO gpio)
    {
        return new RSMAGpioHalAdapter(gpio, RsmaBoardProfiles.CreateStm32F103());
    }

    public void PinMode(byte pin, VirtualPinMode mode)
    {
        var state = GetState(pin);
        ConfigurePin(pin, mode, state.PullMode, state.OutputType);
    }

    public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
        VirtualPinOutputType outputType)
    {
        var connectedPin = Resolve(pin);
        var gpioPin = gpio.GetPin(connectedPin);
        gpioPin.pinMode = mode == VirtualPinMode.Output ? PinModes.Output : PinModes.Input;
        gpioPin.pinPullMode = ToLegacyPullMode(pullMode);
        gpioPin.pinOutputMode = ToLegacyOutputType(outputType);

        var state = GetState(pin);
        state.Mode = mode;
        state.PullMode = pullMode;
        state.OutputType = outputType;
        if (mode == VirtualPinMode.Input && pullMode == VirtualPinPullMode.PullUp && !state.LineLevel)
        {
            gpio.WritePin(connectedPin.portName, connectedPin.pinName, RSMAGPIO.High);
            state.LineLevel = true;
        }
        if (mode == VirtualPinMode.Output)
            WriteLine(pin, state.LatchLevel);
    }

    public void DigitalWrite(byte pin, bool value)
    {
        var connectedPin = Resolve(pin);
        var gpioPin = gpio.GetPin(connectedPin);
        var state = GetState(pin);
        if (state.Mode != VirtualPinMode.Output)
            throw new InvalidOperationException("DigitalWrite requires an output pin");

        state.LatchLevel = value;
        gpio.WritePin(connectedPin.portName, connectedPin.pinName, value ? RSMAGPIO.High : RSMAGPIO.Low);
        state.LineLevel = gpioPin.value >= 0.5f;
    }

    public bool DigitalRead(byte pin)
    {
        var state = GetState(pin);
        state.LineLevel = gpio.GetPin(Resolve(pin)).value >= 0.5f;
        return state.LineLevel;
    }

    public void ScheduleInputLevel(byte pin, double timestampMilliseconds, bool level)
    {
        if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) || timestampMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
        if (GetState(pin).Mode != VirtualPinMode.Input)
            throw new InvalidOperationException("Input events require input mode");

        scheduledInputs.Add(new ScheduledInput
        {
            Pin = pin,
            Level = level,
            Timestamp = timestampMilliseconds,
            Sequence = nextInputSequence++
        });
        scheduledInputs.Sort((left, right) =>
        {
            var timestamp = left.Timestamp.CompareTo(right.Timestamp);
            return timestamp != 0 ? timestamp : left.Sequence.CompareTo(right.Sequence);
        });
    }

    public void AdvanceInputTo(double timestampMilliseconds)
    {
        if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
        while (scheduledInputs.Count != 0 && scheduledInputs[0].Timestamp <= timestampMilliseconds)
        {
            var input = scheduledInputs[0];
            scheduledInputs.RemoveAt(0);
            var state = GetState(input.Pin);
            var previous = state.LineLevel;
            state.LineLevel = input.Level;
            gpio.WritePin(Resolve(input.Pin).portName, Resolve(input.Pin).pinName,
                input.Level ? RSMAGPIO.High : RSMAGPIO.Low);
            if (previous == input.Level)
                continue;
            InputEdge?.Invoke(new VirtualGpioInputEvent
            {
                VirtualPin = input.Pin,
                PreviousLevel = previous,
                Level = input.Level,
                TimestampMilliseconds = input.Timestamp,
                Sequence = input.Sequence,
                Edge = input.Level ? VirtualGpioEdge.Rising : VirtualGpioEdge.Falling
            });
        }
    }

    public void ApplySafeState()
    {
        foreach (var pin in bindings.Keys)
        {
            var state = GetState(pin);
            if (state.Mode == VirtualPinMode.Output)
                WriteLine(pin, false);
        }
    }

    public void ApplyResetState()
    {
        foreach (var definition in profile.PinDefinitions)
        {
            var state = GetState(definition.VirtualPin);
            state.Mode = definition.ResetMode;
            state.PullMode = definition.ResetPullMode;
            state.OutputType = definition.OutputType;
            state.LatchLevel = definition.ResetLatchLevel;
            ConfigurePin(definition.VirtualPin, state.Mode, state.PullMode, state.OutputType);
            if (state.Mode == VirtualPinMode.Output)
                WriteLine(definition.VirtualPin, state.LatchLevel);
        }
    }

    public VirtualGpioPinState GetState(byte pin)
    {
        if (!states.TryGetValue(pin, out var state))
            throw new InvalidOperationException("Virtual GPIO pin is not mapped: " + pin);
        return state;
    }

    public VirtualPinMode GetMode(byte pin)
    {
        return GetState(pin).Mode;
    }

    private void WriteLine(byte pin, bool value)
    {
        var connectedPin = Resolve(pin);
        gpio.WritePin(connectedPin.portName, connectedPin.pinName, value ? RSMAGPIO.High : RSMAGPIO.Low);
        GetState(pin).LineLevel = gpio.GetPin(connectedPin).value >= 0.5f;
    }

    private static VirtualBoardProfile CreateProfileFromBindings(IEnumerable<VirtualPinBinding> pinBindings)
    {
        if (pinBindings == null)
            throw new ArgumentNullException(nameof(pinBindings));
        var data = new List<VirtualPinBindingData>();
        foreach (var binding in pinBindings)
        {
            if (binding == null)
                throw new ArgumentException("Pin binding cannot be null", nameof(pinBindings));
            data.Add(new VirtualPinBindingData(
                binding.virtualPin,
                binding.gpioPin.portName,
                binding.gpioPin.pinName,
                binding.resetMode == PinModes.Output ? VirtualPinMode.Output : VirtualPinMode.Input,
                ToVirtualPullMode(binding.pullMode),
                binding.outputMode == PinOutputModes.OpenDrain ? VirtualPinOutputType.OpenDrain : VirtualPinOutputType.PushPull,
                binding.resetLatchLevel));
        }
        return VirtualBoardProfile.CreateFromBindings("custom", data);
    }

    private static VirtualPinPullMode ToVirtualPullMode(PinPullModes mode)
    {
        return mode == PinPullModes.PullUp ? VirtualPinPullMode.PullUp :
            mode == PinPullModes.PullDown ? VirtualPinPullMode.PullDown : VirtualPinPullMode.None;
    }

    private static PinPullModes ToLegacyPullMode(VirtualPinPullMode mode)
    {
        return mode == VirtualPinPullMode.PullUp ? PinPullModes.PullUp :
            mode == VirtualPinPullMode.PullDown ? PinPullModes.PullDown : PinPullModes.NoPullUpDown;
    }

    private static VirtualPinOutputType ToVirtualOutputType(PinOutputModes mode)
    {
        return mode == PinOutputModes.OpenDrain ? VirtualPinOutputType.OpenDrain : VirtualPinOutputType.PushPull;
    }

    private static PinOutputModes ToLegacyOutputType(VirtualPinOutputType mode)
    {
        return mode == VirtualPinOutputType.OpenDrain ? PinOutputModes.OpenDrain : PinOutputModes.PushPull;
    }

    private ConnectedPin Resolve(byte pin)
    {
        if (!bindings.TryGetValue(pin, out var connectedPin))
            throw new InvalidOperationException("Virtual GPIO pin is not mapped: " + pin);
        return connectedPin;
    }
}
