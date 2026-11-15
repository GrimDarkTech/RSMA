using System;
using System.Collections.Generic;
using System.Linq;
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
public sealed class RSMAGpioHalAdapter : IVirtualMcuHal, IVirtualPwmHal, IVirtualPwmFrequencyHal,
    IVirtualPwmOutputSource, IVirtualInputSource,
    IVirtualPinValueSource, IVirtualButtonHost, IVirtualGpioEdgeSource, IVirtualAnalogInputSource,
    IVirtualPeripheralLifecycle, IVirtualBoardCapabilitySource, IVirtualElectricalModel
{
    private readonly RSMAGPIO gpio;
    private readonly VirtualBoardProfile profile;
    private readonly Dictionary<byte, ConnectedPin> bindings = new Dictionary<byte, ConnectedPin>();
    private readonly Dictionary<byte, VirtualGpioPinState> states = new Dictionary<byte, VirtualGpioPinState>();
    private readonly Dictionary<byte, bool?> externalDrives = new Dictionary<byte, bool?>();
    private readonly Dictionary<byte, VirtualGpioElectricalState> electricalStates =
        new Dictionary<byte, VirtualGpioElectricalState>();
    private readonly Dictionary<byte, Func<double, ushort>> analogSensors =
        new Dictionary<byte, Func<double, ushort>>();
    private readonly Dictionary<byte, int> pwmFrequencies = new Dictionary<byte, int>();
    private readonly Dictionary<byte, double> pwmUpdateTimes = new Dictionary<byte, double>();
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
    public IList<string> ElectricalDiagnostics { get; } = new List<string>();
    public IEnumerable<byte> SupportedPins => bindings.Keys;
    public IEnumerable<byte> SupportedAnalogChannels => profile.PinDefinitions
        .Where(definition => bindings.ContainsKey(definition.VirtualPin) &&
            (analogSensors.ContainsKey(definition.VirtualPin) ||
            ((IVirtualAnalogInputSource)this).SupportsAnalogChannel(definition.VirtualPin))
        )
        .Select(definition => definition.VirtualPin);
    public IEnumerable<byte> SupportedSpiBuses => Array.Empty<byte>();
    public IEnumerable<byte> SupportedI2cAddresses => Array.Empty<byte>();
    public IEnumerable<byte> SupportedDmaChannels => Array.Empty<byte>();
    public IEnumerable<byte> SupportedUartPorts => Array.Empty<byte>();
    public IEnumerable<string> Constraints => new[]
    {
        "GPIO pin capabilities are limited to the configured scene bindings",
        "SPI, I2C, DMA and EEPROM are not provided by the scene GPIO adapter"
    };
    public double? NextInputTimestampMilliseconds => scheduledInputs.Count == 0
        ? (double?)null : scheduledInputs[0].Timestamp;

    public RSMAGpioHalAdapter(RSMAGPIO gpio, IEnumerable<VirtualPinBinding> pinBindings)
        : this(gpio, CreateProfileFromBindings(pinBindings))
    {
    }

    public RSMAGpioHalAdapter(RSMAGPIO gpio, VirtualBoardProfile profile)
        : this(gpio, profile, null, null, true)
    {
    }

    public RSMAGpioHalAdapter(RSMAGPIO gpio, VirtualBoardProfile profile, IEnumerable<byte> mappedPins)
        : this(gpio, profile, mappedPins, null, true)
    {
    }

    public RSMAGpioHalAdapter(RSMAGPIO gpio, VirtualBoardProfile profile,
        IEnumerable<VirtualPinBinding> pinBindings)
        : this(gpio, profile,
            pinBindings == null ? null : pinBindings.Select(binding => binding.virtualPin),
            pinBindings == null ? null : pinBindings.ToDictionary(
                binding => binding.virtualPin, binding => binding.gpioPin), true)
    {
    }

    private RSMAGpioHalAdapter(RSMAGPIO gpio, VirtualBoardProfile profile,
        IEnumerable<byte> mappedPins, IDictionary<byte, ConnectedPin> physicalOverrides, bool initialize)
    {
        this.gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        var allowedPins = mappedPins == null ? null : new HashSet<byte>(mappedPins);

        foreach (var definition in profile.PinDefinitions)
        {
            if (allowedPins != null && !allowedPins.Contains(definition.VirtualPin))
                continue;
            var connectedPin = physicalOverrides != null && physicalOverrides.TryGetValue(definition.VirtualPin,
                out var overridePin)
                ? overridePin
                : new ConnectedPin { portName = definition.PortName, pinName = definition.PinName };
            if (!profile.GetPin(definition.VirtualPin).SupportsPhysicalRoute(connectedPin.portName, connectedPin.pinName))
                throw new BoardProfileException("Board '" + profile.BoardId + "' does not declare physical route " +
                    connectedPin.portName + "." + connectedPin.pinName + " for virtual pin " + definition.VirtualPin);
            if (gpio.GetPin(connectedPin) == null)
                continue;

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
        return new RSMAGpioHalAdapter(gpio, RsmaBoardProfiles.CreateStm32F103(),
            new byte[] { 10, 11, 13 });
    }

    public void PinMode(byte pin, VirtualPinMode mode)
    {
        var state = GetState(pin);
        ConfigurePin(pin, mode, state.PullMode, state.OutputType);
    }

    public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
        VirtualPinOutputType outputType)
    {
        var definition = profile.GetPin(pin);
        if (mode == VirtualPinMode.Input && !definition.Supports(VirtualPinCapabilities.DigitalInput))
            throw new InvalidOperationException("Pin " + pin + " does not support digital input");
        if (mode == VirtualPinMode.Output && !definition.Supports(VirtualPinCapabilities.DigitalOutput))
            throw new InvalidOperationException("Pin " + pin + " does not support digital output");
        if (pullMode == VirtualPinPullMode.PullUp && !definition.Supports(VirtualPinCapabilities.PullUp))
            throw new InvalidOperationException("Pin " + pin + " does not support pull-up");
        if (pullMode == VirtualPinPullMode.PullDown && !definition.Supports(VirtualPinCapabilities.PullDown))
            throw new InvalidOperationException("Pin " + pin + " does not support pull-down");
        if (outputType == VirtualPinOutputType.OpenDrain && !definition.Supports(VirtualPinCapabilities.OpenDrain))
            throw new InvalidOperationException("Pin " + pin + " does not support open-drain output");
        var connectedPin = Resolve(pin);
        var gpioPin = gpio.GetPin(connectedPin);
        gpioPin.pinMode = mode == VirtualPinMode.Output ? PinModes.Output : PinModes.Input;
        gpioPin.pinPullMode = ToLegacyPullMode(pullMode);
        gpioPin.pinOutputMode = ToLegacyOutputType(outputType);

        var state = GetState(pin);
        state.Mode = mode;
        state.PullMode = pullMode;
        state.OutputType = outputType;
        if (mode == VirtualPinMode.Output)
            WriteLine(pin, state.LatchLevel);
        RecomputeElectricalState(pin);
    }

    public void DigitalWrite(byte pin, bool value)
    {
        var connectedPin = Resolve(pin);
        var gpioPin = gpio.GetPin(connectedPin);
        var state = GetState(pin);
        if (state.Mode != VirtualPinMode.Output)
            throw new InvalidOperationException("DigitalWrite requires an output pin");

        state.LatchLevel = value;
        RecomputeElectricalState(pin);
    }

    public void PwmWrite(byte pin, byte dutyCycle)
    {
        profile.GetPinWithCapability(pin, VirtualPinCapabilities.Pwm);
        var connectedPin = Resolve(pin);
        var state = GetState(pin);
        if (state.Mode != VirtualPinMode.Output)
            throw new InvalidOperationException("PWM write requires an output pin");

        var value = dutyCycle / 255.0f;
        state.LatchLevel = dutyCycle >= 128;
        RecomputeElectricalState(pin, value);
        if (!pwmFrequencies.ContainsKey(pin))
            pwmFrequencies[pin] = VirtualGpioBank.DefaultPwmFrequencyHz;
    }

    public void PwmWriteFrequency(byte pin, byte dutyCycle, int frequencyHz)
    {
        if (frequencyHz < 1 || frequencyHz > 1000000)
            throw new InvalidOperationException("PWM frequency must be between 1 and 1000000 Hz");
        PwmWrite(pin, dutyCycle);
        pwmFrequencies[pin] = frequencyHz;
    }

    public VirtualPwmOutputState GetPwmOutput(byte pin)
    {
        var normalized = Math.Max(0f, Math.Min(1f, ReadPinValue(pin)));
        return new VirtualPwmOutputState(
            pin,
            (byte)Math.Round(normalized * 255f, MidpointRounding.AwayFromZero),
            pwmFrequencies.TryGetValue(pin, out var frequency)
                ? frequency : VirtualGpioBank.DefaultPwmFrequencyHz,
            normalized >= 0.5f,
            pwmUpdateTimes.TryGetValue(pin, out var timestamp) ? timestamp : 0.0);
    }

    public void SetPwmUpdateTime(byte pin, double timestampMilliseconds)
    {
        if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
            timestampMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
        Resolve(pin);
        pwmUpdateTimes[pin] = timestampMilliseconds;
    }

    public float ReadPinValue(byte pin)
    {
        return gpio.GetPin(Resolve(pin)).value;
    }

    public ushort AnalogRead(byte channel)
    {
        profile.GetPinWithCapability(channel, VirtualPinCapabilities.Analog);
        var connectedPin = Resolve(channel);
        var normalized = gpio.GetPin(connectedPin).value;
        var clamped = Math.Max(0f, Math.Min(1f, normalized));
        return (ushort)Math.Round(clamped * 4095f, MidpointRounding.AwayFromZero);
    }

    public bool SupportsAnalogChannel(byte channel)
    {
        return profile.TryGetPin(channel, out var definition) &&
            definition.Supports(VirtualPinCapabilities.Analog);
    }

    public void SetAnalogInput(byte channel, ushort value)
    {
        if (value > 4095)
            throw new ArgumentOutOfRangeException(nameof(value), "ADC sample must be between 0 and 4095");
        var pin = gpio.GetPin(Resolve(channel));
        pin.value = value / 4095f;
    }

    public void ClearAnalogInput(byte channel)
    {
        if (!SupportsAnalogChannel(channel))
            throw new InvalidOperationException("ADC channel is not mapped: " + channel);
        gpio.GetPin(Resolve(channel)).value = 0f;
    }

    public void RegisterAnalogSensor(byte channel, Func<double, ushort> sampler)
    {
        if (sampler == null)
            throw new ArgumentNullException(nameof(sampler));
        if (!SupportsAnalogChannel(channel))
            throw new InvalidOperationException("ADC channel is not mapped: " + channel);
        if (analogSensors.ContainsKey(channel))
            throw new InvalidOperationException("ADC channel already has a virtual sensor");
        analogSensors.Add(channel, sampler);
    }

    public void UnregisterAnalogSensor(byte channel, Func<double, ushort> sampler)
    {
        if (analogSensors.TryGetValue(channel, out var registered) && registered == sampler)
            analogSensors.Remove(channel);
    }

    public void AdvanceAnalogTo(double simulatedTimeMilliseconds)
    {
        if (double.IsNaN(simulatedTimeMilliseconds) || double.IsInfinity(simulatedTimeMilliseconds) ||
            simulatedTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(simulatedTimeMilliseconds));
        foreach (var sensor in analogSensors)
            SetAnalogInput(sensor.Key, sensor.Value(simulatedTimeMilliseconds));
    }

    public bool DigitalRead(byte pin)
    {
        var state = GetState(pin);
        RecomputeElectricalState(pin);
        return state.LineLevel;
    }

    public void SetExternalDrive(byte virtualPin, bool level)
    {
        GetState(virtualPin);
        externalDrives[virtualPin] = level;
        RecomputeElectricalState(virtualPin);
    }

    public void ClearExternalDrive(byte virtualPin)
    {
        GetState(virtualPin);
        externalDrives[virtualPin] = null;
        RecomputeElectricalState(virtualPin);
    }

    public VirtualGpioElectricalState GetElectricalState(byte virtualPin)
    {
        GetState(virtualPin);
        RecomputeElectricalState(virtualPin);
        return electricalStates[virtualPin];
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
            externalDrives[input.Pin] = input.Level;
            RecomputeElectricalState(input.Pin);
            if (previous == input.Level)
                continue;
            InputEdge?.Invoke(new VirtualGpioInputEvent(
                input.Pin, previous, input.Level, input.Timestamp, input.Sequence,
                input.Level ? VirtualGpioEdge.Rising : VirtualGpioEdge.Falling));
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

    public void ResetPeripheralState()
    {
        ApplyResetState();
    }

    public void ApplyResetState()
    {
        scheduledInputs.Clear();
        pwmFrequencies.Clear();
        pwmUpdateTimes.Clear();
        externalDrives.Clear();
        ElectricalDiagnostics.Clear();
        foreach (var definition in profile.PinDefinitions)
        {
            if (!bindings.ContainsKey(definition.VirtualPin))
                continue;
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
        GetState(pin).LatchLevel = value;
        RecomputeElectricalState(pin);
    }

    private void RecomputeElectricalState(byte pin, float? analogValue = null)
    {
        var state = GetState(pin);
        externalDrives.TryGetValue(pin, out var external);
        var driverActive = state.Mode == VirtualPinMode.Output &&
            (state.OutputType == VirtualPinOutputType.PushPull || !state.LatchLevel);
        var driverLevel = analogValue.HasValue ? analogValue.Value >= 0.5f : state.LatchLevel;
        var highImpedance = !driverActive;
        var pullLevel = state.PullMode == VirtualPinPullMode.PullUp ? (bool?)true :
            state.PullMode == VirtualPinPullMode.PullDown ? (bool?)false : null;
        var line = highImpedance ? (external ?? pullLevel ?? false) : driverLevel;
        var conflict = driverActive && external.HasValue && external.Value != driverLevel;
        var connectedPin = Resolve(pin);
        var physicalPin = gpio.GetPin(connectedPin);
        if (physicalPin != null)
            physicalPin.value = analogValue.HasValue && driverActive && !conflict
                ? analogValue.Value : (line ? RSMAGPIO.High : RSMAGPIO.Low);
        state.LineLevel = line;
        var electrical = electricalStates.TryGetValue(pin, out var existing)
            ? existing : new VirtualGpioElectricalState { VirtualPin = pin };
        var previousDiagnostic = existing == null ? null : existing.Diagnostic;
        electrical.OutputDriverActive = driverActive;
        electrical.HighImpedance = highImpedance;
        electrical.ExternalDriveLevel = external;
        electrical.LineLevel = line;
        electrical.Conflict = conflict;
        electrical.Diagnostic = conflict
            ? "External drive conflicts with push-pull output on virtual pin " + pin : null;
        electricalStates[pin] = electrical;
        if (conflict && !ElectricalDiagnostics.Contains(electrical.Diagnostic))
            ElectricalDiagnostics.Add(electrical.Diagnostic);
        else if (!conflict && !string.IsNullOrEmpty(previousDiagnostic))
            ElectricalDiagnostics.Remove(previousDiagnostic);
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
