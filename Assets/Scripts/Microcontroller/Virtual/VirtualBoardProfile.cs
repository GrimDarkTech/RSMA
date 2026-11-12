using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
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

        public VirtualPinDefinition(
            byte virtualPin,
            string portName,
            string pinName,
            VirtualPinMode resetMode = VirtualPinMode.Input,
            VirtualPinPullMode resetPullMode = VirtualPinPullMode.None,
            VirtualPinOutputType outputType = VirtualPinOutputType.PushPull,
            bool resetLatchLevel = false)
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
                    binding.ResetLatchLevel));
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

        public VirtualPinBindingData(
            byte virtualPin,
            string portName,
            string pinName,
            VirtualPinMode resetMode = VirtualPinMode.Input,
            VirtualPinPullMode resetPullMode = VirtualPinPullMode.None,
            VirtualPinOutputType outputType = VirtualPinOutputType.PushPull,
            bool resetLatchLevel = false)
        {
            VirtualPin = virtualPin;
            PortName = portName;
            PinName = pinName;
            ResetMode = resetMode;
            ResetPullMode = resetPullMode;
            OutputType = outputType;
            ResetLatchLevel = resetLatchLevel;
        }
    }

    public static class RsmaBoardProfiles
    {
        public const string Stm32F103BoardId = "RSMA-STM32F103";

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
                    false),
                new VirtualPinDefinition(
                    11,
                    "PC",
                    "11",
                    VirtualPinMode.Input,
                    VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull,
                    false),
                new VirtualPinDefinition(
                    13,
                    "PC",
                    "13",
                    VirtualPinMode.Input,
                    VirtualPinPullMode.None,
                    VirtualPinOutputType.PushPull,
                    false)
            });
        }

        public static VirtualBoardProfile Create(string boardId)
        {
            if (string.Equals(boardId, Stm32F103BoardId, StringComparison.Ordinal))
                return CreateStm32F103();
            throw new BoardProfileException("Unknown RSMA board profile '" + boardId + "'");
        }
    }
}
