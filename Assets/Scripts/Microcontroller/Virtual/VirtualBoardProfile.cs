using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    [Flags]
    public enum VirtualPinCapabilities : ushort
    {
        None = 0,
        DigitalInput = 1 << 0,
        DigitalOutput = 1 << 1,
        PullUp = 1 << 2,
        PullDown = 1 << 3,
        OpenDrain = 1 << 4,
        Analog = 1 << 5,
        Pwm = 1 << 6,
        ExternalInterrupt = 1 << 7,
        BasicGpio = DigitalInput | DigitalOutput | PullUp | PullDown,
        LegacyUnconstrained = BasicGpio | OpenDrain | Analog | Pwm | ExternalInterrupt
    }

    public sealed class VirtualPinAlternateFunction
    {
        public string Name { get; }
        public string Route { get; }

        public VirtualPinAlternateFunction(string name, string route)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new BoardProfileException("GPIO alternate function name is empty");
            if (string.IsNullOrWhiteSpace(route))
                throw new BoardProfileException("GPIO alternate function route is empty");
            Name = name;
            Route = route;
        }
    }

    public enum VirtualPinPullMode : byte
    {
        None = 0,
        PullUp = 1,
        PullDown = 2
    }

    public enum VirtualPinOutputType : byte
    {
        PushPull = 0,
        OpenDrain = 1
    }

    public sealed class VirtualGpioPinState
    {
        public byte VirtualPin { get; set; }
        public VirtualPinMode Mode { get; set; }
        public VirtualPinPullMode PullMode { get; set; }
        public VirtualPinOutputType OutputType { get; set; }
        public bool LatchLevel { get; set; }
        public bool LineLevel { get; set; }
    }

    public sealed class VirtualPinDefinition
    {
        public byte VirtualPin { get; }
        public string PortName { get; }
        public string PinName { get; }
        public VirtualPinMode ResetMode { get; }
        public VirtualPinPullMode ResetPullMode { get; }
        public VirtualPinOutputType OutputType { get; }
        public bool ResetLatchLevel { get; }
        public VirtualPinCapabilities Capabilities { get; }
        public IList<VirtualPinAlternateFunction> AlternateFunctions { get; }

        public VirtualPinDefinition(
            byte virtualPin,
            string portName,
            string pinName,
            VirtualPinMode resetMode = VirtualPinMode.Input,
            VirtualPinPullMode resetPullMode = VirtualPinPullMode.None,
            VirtualPinOutputType outputType = VirtualPinOutputType.PushPull,
            bool resetLatchLevel = false,
            VirtualPinCapabilities capabilities = VirtualPinCapabilities.LegacyUnconstrained,
            IEnumerable<VirtualPinAlternateFunction> alternateFunctions = null)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new BoardProfileException("GPIO profile port name is empty");
            if (string.IsNullOrWhiteSpace(pinName))
                throw new BoardProfileException("GPIO profile pin name is empty");

            VirtualPin = virtualPin;
            PortName = portName;
            PinName = pinName;
            ResetMode = resetMode;
            ResetPullMode = resetPullMode;
            OutputType = outputType;
            ResetLatchLevel = resetLatchLevel;
            Capabilities = capabilities;
            AlternateFunctions = new List<VirtualPinAlternateFunction>(alternateFunctions ??
                Array.Empty<VirtualPinAlternateFunction>()).AsReadOnly();
            ValidateResetConfiguration();
        }

        public bool Supports(VirtualPinCapabilities capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool SupportsAlternateFunction(string name, string route = null)
        {
            foreach (var alternate in AlternateFunctions)
            {
                if (!string.Equals(alternate.Name, name, StringComparison.Ordinal))
                    continue;
                if (route == null || string.Equals(alternate.Route, route, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void ValidateResetConfiguration()
        {
            if (ResetMode == VirtualPinMode.Input && !Supports(VirtualPinCapabilities.DigitalInput))
                throw new BoardProfileException("GPIO profile reset mode requires digital input capability for pin " + VirtualPin);
            if (ResetMode == VirtualPinMode.Output && !Supports(VirtualPinCapabilities.DigitalOutput))
                throw new BoardProfileException("GPIO profile reset mode requires digital output capability for pin " + VirtualPin);
            if (ResetPullMode == VirtualPinPullMode.PullUp && !Supports(VirtualPinCapabilities.PullUp))
                throw new BoardProfileException("GPIO profile reset pull-up is unsupported for pin " + VirtualPin);
            if (ResetPullMode == VirtualPinPullMode.PullDown && !Supports(VirtualPinCapabilities.PullDown))
                throw new BoardProfileException("GPIO profile reset pull-down is unsupported for pin " + VirtualPin);
            if (OutputType == VirtualPinOutputType.OpenDrain && !Supports(VirtualPinCapabilities.OpenDrain))
                throw new BoardProfileException("GPIO profile open-drain output is unsupported for pin " + VirtualPin);
        }
    }

    public sealed class BoardProfileException : Exception
    {
        public BoardProfileException(string message) : base(message) { }
    }

    public sealed class VirtualBoardProfile
    {
        private readonly Dictionary<byte, VirtualPinDefinition> definitions =
            new Dictionary<byte, VirtualPinDefinition>();

        public VirtualBoardProfile(string boardId, IEnumerable<VirtualPinDefinition> pinDefinitions)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                throw new BoardProfileException("GPIO profile board ID is empty");
            if (pinDefinitions == null)
                throw new ArgumentNullException(nameof(pinDefinitions));

            BoardId = boardId;
            var alternateRoutes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in pinDefinitions)
            {
                if (definition == null)
                    throw new BoardProfileException("GPIO profile contains a null pin definition");
                if (definitions.ContainsKey(definition.VirtualPin))
                    throw new BoardProfileException("GPIO profile contains duplicate virtual pin " + definition.VirtualPin);

                foreach (var existing in definitions.Values)
                {
                    if (string.Equals(existing.PortName, definition.PortName, StringComparison.Ordinal) &&
                        string.Equals(existing.PinName, definition.PinName, StringComparison.Ordinal))
                        throw new BoardProfileException("GPIO profile maps physical pin " + definition.PortName + "." +
                            definition.PinName + " more than once");
                }
                ValidateAlternateFunctions(definition, alternateRoutes);
                definitions.Add(definition.VirtualPin, definition);
            }

            if (definitions.Count == 0)
                throw new BoardProfileException("GPIO profile contains no pin definitions");
        }

        public string BoardId { get; }
        public IEnumerable<VirtualPinDefinition> PinDefinitions => definitions.Values;

        public VirtualPinDefinition GetPin(byte virtualPin)
        {
            if (!definitions.TryGetValue(virtualPin, out var definition))
                throw new BoardProfileException("Board '" + BoardId + "' has no mapping for virtual GPIO pin " + virtualPin);
            return definition;
        }

        public bool TryGetPin(byte virtualPin, out VirtualPinDefinition definition)
        {
            return definitions.TryGetValue(virtualPin, out definition);
        }

        public VirtualPinDefinition GetPinWithCapability(byte virtualPin, VirtualPinCapabilities capability)
        {
            var definition = GetPin(virtualPin);
            if (!definition.Supports(capability))
                throw new BoardProfileException("Board '" + BoardId + "' pin " + virtualPin +
                    " does not support capability " + capability);
            return definition;
        }

        private static void ValidateAlternateFunctions(VirtualPinDefinition definition,
            HashSet<string> alternateRoutes)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var alternate in definition.AlternateFunctions)
            {
                if (alternate == null)
                    throw new BoardProfileException("GPIO profile contains a null alternate function on virtual pin " +
                        definition.VirtualPin);
                var key = alternate.Name + "@" + alternate.Route;
                if (!keys.Add(key) || !alternateRoutes.Add(key))
                    throw new BoardProfileException("GPIO profile contains duplicate alternate function " + key +
                        " on virtual pin " + definition.VirtualPin);
            }
        }

        public static VirtualBoardProfile CreateFromBindings(string boardId, IEnumerable<VirtualPinBindingData> bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            var definitions = new List<VirtualPinDefinition>();
            foreach (var binding in bindings)
            {
                definitions.Add(new VirtualPinDefinition(
                    binding.VirtualPin,
                    binding.PortName,
                    binding.PinName,
                    binding.ResetMode,
                    binding.ResetPullMode,
                    binding.OutputType,
                    binding.ResetLatchLevel,
                    binding.Capabilities,
                    binding.AlternateFunctions));
            }
            return new VirtualBoardProfile(boardId, definitions);
        }
    }

    public sealed class VirtualPinBindingData
    {
        public byte VirtualPin { get; }
        public string PortName { get; }
        public string PinName { get; }
        public VirtualPinMode ResetMode { get; }
        public VirtualPinPullMode ResetPullMode { get; }
        public VirtualPinOutputType OutputType { get; }
        public bool ResetLatchLevel { get; }
        public VirtualPinCapabilities Capabilities { get; }
        public IEnumerable<VirtualPinAlternateFunction> AlternateFunctions { get; }

        public VirtualPinBindingData(
            byte virtualPin,
            string portName,
            string pinName,
            VirtualPinMode resetMode = VirtualPinMode.Input,
            VirtualPinPullMode resetPullMode = VirtualPinPullMode.None,
            VirtualPinOutputType outputType = VirtualPinOutputType.PushPull,
            bool resetLatchLevel = false,
            VirtualPinCapabilities capabilities = VirtualPinCapabilities.LegacyUnconstrained,
            IEnumerable<VirtualPinAlternateFunction> alternateFunctions = null)
        {
            VirtualPin = virtualPin;
            PortName = portName;
            PinName = pinName;
            ResetMode = resetMode;
            ResetPullMode = resetPullMode;
            OutputType = outputType;
            ResetLatchLevel = resetLatchLevel;
            Capabilities = capabilities;
            AlternateFunctions = alternateFunctions ?? Array.Empty<VirtualPinAlternateFunction>();
        }
    }

    public static class RsmaBoardProfiles
    {
        public const string Stm32F103BoardId = "RSMA-STM32F103";
        public const string AvrAtmega328PBoardId = "RSMA-AVR-ATmega328P";

        public static VirtualBoardProfile CreateStm32F103()
        {
            return new VirtualBoardProfile(Stm32F103BoardId, new[]
            {
                new VirtualPinDefinition(
                    10,
                    "PC",
                    "10",
                    VirtualPinMode.Input,
                    VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull,
                    false,
                    VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.Analog |
                        VirtualPinCapabilities.ExternalInterrupt,
                    new[]
                    {
                        new VirtualPinAlternateFunction("ADC", "ADC10"),
                        new VirtualPinAlternateFunction("EXTI", "EXTI10")
                    }),
                new VirtualPinDefinition(
                    11,
                    "PC",
                    "11",
                    VirtualPinMode.Input,
                    VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull,
                    false,
                    VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.ExternalInterrupt,
                    new[] { new VirtualPinAlternateFunction("EXTI", "EXTI11") }),
                new VirtualPinDefinition(
                    13,
                    "PC",
                    "13",
                    VirtualPinMode.Input,
                    VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull,
                    false,
                    VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.Pwm,
                    new[] { new VirtualPinAlternateFunction("PWM", "TIM1_CH1") })
            });
        }

        public static VirtualBoardProfile CreateAvrAtmega328P()
        {
            return new VirtualBoardProfile(AvrAtmega328PBoardId, new[]
            {
                new VirtualPinDefinition(11, "PB", "3", VirtualPinMode.Input, VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull, false, VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.Pwm,
                    new[] { new VirtualPinAlternateFunction("PWM", "TIM2_CH2") }),
                new VirtualPinDefinition(12, "PB", "4", VirtualPinMode.Input, VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull, false, VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.ExternalInterrupt,
                    new[] { new VirtualPinAlternateFunction("EXTI", "EXTI4") }),
                new VirtualPinDefinition(13, "PB", "5", VirtualPinMode.Input, VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull, false, VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.Analog,
                    new[] { new VirtualPinAlternateFunction("ADC", "ADC0") })
            });
        }

        public static VirtualBoardProfile Create(string boardId)
        {
            if (string.Equals(boardId, Stm32F103BoardId, StringComparison.Ordinal))
                return CreateStm32F103();
            if (string.Equals(boardId, AvrAtmega328PBoardId, StringComparison.Ordinal))
                return CreateAvrAtmega328P();
            throw new BoardProfileException("Unknown RSMA board profile '" + boardId + "'");
        }
    }
}
