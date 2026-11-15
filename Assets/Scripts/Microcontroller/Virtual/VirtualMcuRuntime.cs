using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualPinMode : byte
    {
        Input = 0,
        Output = 1
    }

    public enum VirtualExecutionState
    {
        Ready,
        Running,
        Waiting,
        Yielded,
        Halted,
        Faulted
    }

    public interface IVirtualMcuHal
    {
        void PinMode(byte pin, VirtualPinMode mode);
        void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
            VirtualPinOutputType outputType);
        void DigitalWrite(byte pin, bool value);
        bool DigitalRead(byte pin);
    }

    public interface IVirtualPinValueSource
    {
        float ReadPinValue(byte pin);
    }

    public interface IVirtualPwmHal
    {
        void PwmWrite(byte pin, byte dutyCycle);
    }

    public interface IVirtualAdcHal
    {
        ushort AnalogRead(byte channel);
    }

    public interface IVirtualAnalogInputSource : IVirtualAdcHal
    {
        bool SupportsAnalogChannel(byte channel);
        void SetAnalogInput(byte channel, ushort value);
        void ClearAnalogInput(byte channel);
        void RegisterAnalogSensor(byte channel, Func<double, ushort> sampler);
        void UnregisterAnalogSensor(byte channel, Func<double, ushort> sampler);
        void AdvanceAnalogTo(double simulatedTimeMilliseconds);
    }

    public interface IVirtualSpiHal
    {
        byte SpiTransfer(byte bus, byte value);
    }

    public interface IVirtualI2cHal
    {
        byte I2cTransfer(byte address, byte value);
    }

    public interface IVirtualDmaHal
    {
        byte DmaTransfer(byte channel, byte value);
    }

    public interface IVirtualAsyncDmaHal
    {
        void DmaBufferWrite(byte buffer, byte offset, byte value);
        byte DmaBufferRead(byte buffer, byte offset);
        int DmaSubmit(byte channel, byte buffer, int length);
        int DmaStatus(int ticket);
        byte DmaReadByte(int ticket, byte offset);
        bool DmaRelease(int ticket);
        void AdvanceDmaTo(double simulatedTimeMilliseconds);
        double? NextDmaCompletionTimeMilliseconds { get; }
        void ResetDma();
    }

    public enum VirtualDmaStatus
    {
        Invalid = -1,
        Queued = 0,
        Active = 1,
        Complete = 2
    }

    public interface IVirtualEepromHal
    {
        byte EepromRead(byte address);
        void EepromWrite(byte address, byte value);
    }

    public interface IVirtualUartHal
    {
        void Begin(byte port, byte baudRatePreset);
        int Available(byte port);
        byte Read(byte port);
        void Write(byte port, byte value);
        int PacketAvailable(byte port);
        byte PacketRead(byte port);
        byte PacketType(byte port);
        bool HasPacket(byte port);
        void CompletePacket(byte port);
    }

    public interface IVirtualPeripheralLifecycle
    {
        void ResetPeripheralState();
        void ApplySafeState();
    }

    public sealed class VirtualUartBank : IVirtualUartHal, IVirtualUartInputSource,
        IVirtualUartOutputSource, IVirtualUartTransportInput,
        IVirtualPeripheralLifecycle
    {
        public const byte PortCount = 4;
        public const byte BaudRateCount = 5;
        public const int ReceiveBufferCapacity = 256;
        public const int MaxScheduledReceiveEvents = 4096;
        public const int MaxPendingHitlPackets = 256;

        private static readonly int[] BaudRates = { 9600, 19200, 38400, 57600, 115200 };
        private readonly PortState[] ports = { new PortState(), new PortState(), new PortState(), new PortState() };

        private sealed class PortState
        {
            public bool Initialized;
            public int BaudRate;
            public readonly Queue<byte> ReceiveBuffer = new Queue<byte>();
            public readonly HitlFrameParser HitlParser = new HitlFrameParser();
            public readonly Queue<HitlFrame> PendingHitlPackets = new Queue<HitlFrame>();
        }

        private sealed class ScheduledReceive
        {
            public byte Port;
            public byte[] Data;
            public double Timestamp;
            public ulong Sequence;
        }

        public event Action<byte, byte> Transmitted;
        public event Action<byte, byte> Received;
        public event Action<byte, HitlFrame> PacketReceived;
        private readonly List<ScheduledReceive> scheduledReceives = new List<ScheduledReceive>();
        private ulong nextReceiveSequence;
        public double? NextInputTimestampMilliseconds => scheduledReceives.Count == 0
            ? (double?)null : scheduledReceives[0].Timestamp;

        public void Begin(byte port, byte baudRatePreset)
        {
            var state = GetPort(port);
            if (baudRatePreset >= BaudRates.Length)
                throw new InvalidOperationException("UART baud rate preset is unsupported");
            state.BaudRate = BaudRates[baudRatePreset];
            state.Initialized = true;
        }

        public int Available(byte port)
        {
            return GetInitializedPort(port).ReceiveBuffer.Count;
        }

        public byte Read(byte port)
        {
            var buffer = GetInitializedPort(port).ReceiveBuffer;
            if (buffer.Count == 0)
                throw new InvalidOperationException("UART receive buffer is empty");
            return buffer.Dequeue();
        }

        public void Write(byte port, byte value)
        {
            GetInitializedPort(port);
            Transmitted?.Invoke(port, value);
        }

        public int PacketAvailable(byte port)
        {
            var packet = GetPendingPacket(port);
            return packet.Payload.Length;
        }

        public byte PacketRead(byte port)
        {
            var state = GetInitializedPort(port);
            var packet = GetPendingPacket(port);
            if (packet.Payload.Length == 0)
                throw new InvalidOperationException("HITL packet payload is empty");
            var value = packet.Payload[0];
            var remaining = new byte[packet.Payload.Length - 1];
            Array.Copy(packet.Payload, 1, remaining, 0, remaining.Length);
            packet.Payload = remaining;
            return value;
        }

        public byte PacketType(byte port)
        {
            return (byte)GetPendingPacket(port).Type;
        }

        public bool HasPacket(byte port)
        {
            return GetInitializedPort(port).PendingHitlPackets.Count != 0;
        }

        public int GetHitlRejectedFrameCount(byte port)
        {
            return GetInitializedPort(port).HitlParser.RejectedFrameCount;
        }

        public int GetPendingHitlPacketCount(byte port)
        {
            return GetInitializedPort(port).PendingHitlPackets.Count;
        }

        public void ResetPeripheralState()
        {
            foreach (var port in ports)
            {
                port.Initialized = false;
                port.BaudRate = 0;
                port.ReceiveBuffer.Clear();
                port.HitlParser.ClearBufferedData();
                port.PendingHitlPackets.Clear();
            }
        }

        public void ApplySafeState()
        {
        }

        public void CompletePacket(byte port)
        {
            var state = GetInitializedPort(port);
            if (state.PendingHitlPackets.Count != 0)
                state.PendingHitlPackets.Dequeue();
        }

        // Host/backend transport ingress. The input is a raw byte chunk and
        // may contain a fragment, one frame, or concatenated frames.
        public void InjectTransportBytes(byte port, byte[] transportBytes)
        {
            if (transportBytes == null)
                throw new ArgumentNullException(nameof(transportBytes));
            var state = GetInitializedPort(port);
            var frames = state.HitlParser.Feed(transportBytes);
            if (frames.Count > MaxPendingHitlPackets - state.PendingHitlPackets.Count)
                throw new InvalidOperationException("HITL packet queue capacity exceeded");
            foreach (var frame in frames)
            {
                state.PendingHitlPackets.Enqueue(frame);
                PacketReceived?.Invoke(port, frame);
            }
        }

        // Compatibility name for callers that already provide transport bytes.
        public void InjectHitlFrame(byte port, byte[] frameBytes)
        {
            InjectTransportBytes(port, frameBytes);
        }

        public void InjectReceived(byte port, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var buffer = GetInitializedPort(port).ReceiveBuffer;
            if (data.Length > ReceiveBufferCapacity - buffer.Count)
                throw new InvalidOperationException("UART receive buffer capacity exceeded");
            foreach (var value in data)
            {
                buffer.Enqueue(value);
                Received?.Invoke(port, value);
            }
        }

        public void ScheduleReceived(byte port, double timestampMilliseconds, byte[] data)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
                timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            GetInitializedPort(port);
            if (data.Length == 0)
                throw new ArgumentException("UART scheduled receive data must not be empty", nameof(data));
            if (data.Length > ReceiveBufferCapacity)
                throw new InvalidOperationException("UART receive buffer capacity exceeded");
            if (scheduledReceives.Count >= MaxScheduledReceiveEvents)
                throw new InvalidOperationException("UART scheduled receive queue capacity exceeded");

            scheduledReceives.Add(new ScheduledReceive
            {
                Port = port,
                Data = (byte[])data.Clone(),
                Timestamp = timestampMilliseconds,
                Sequence = nextReceiveSequence++
            });
            scheduledReceives.Sort((left, right) =>
            {
                var timestamp = left.Timestamp.CompareTo(right.Timestamp);
                return timestamp != 0 ? timestamp : left.Sequence.CompareTo(right.Sequence);
            });
        }

        public void AdvanceInputTo(double timestampMilliseconds)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
                timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            while (scheduledReceives.Count != 0 && scheduledReceives[0].Timestamp <= timestampMilliseconds)
            {
                var receive = scheduledReceives[0];
                scheduledReceives.RemoveAt(0);
                InjectReceived(receive.Port, receive.Data);
            }
        }

        public int GetBaudRate(byte port)
        {
            return GetInitializedPort(port).BaudRate;
        }

        private PortState GetInitializedPort(byte port)
        {
            var state = GetPort(port);
            if (!state.Initialized)
                throw new InvalidOperationException("UART port has not been initialized");
            return state;
        }

        private HitlFrame GetPendingPacket(byte port)
        {
            var packet = GetInitializedPort(port).PendingHitlPackets;
            if (packet.Count == 0)
                throw new InvalidOperationException("HITL packet queue is empty");
            return packet.Peek();
        }

        private PortState GetPort(byte port)
        {
            if (port >= ports.Length)
                throw new InvalidOperationException("UART port is outside the supported range");
            return ports[port];
        }
    }

    public sealed class VirtualGpioBank : IVirtualMcuHal, IVirtualPinValueSource, IVirtualInputSource, IVirtualButtonHost,
        IVirtualGpioEdgeSource, IVirtualPwmHal, IVirtualAdcHal, IVirtualSpiHal, IVirtualI2cHal,
        IVirtualDmaHal, IVirtualAsyncDmaHal, IVirtualEepromHal, IVirtualAnalogInputSource,
        IVirtualPeripheralLifecycle, IVirtualElectricalModel, IVirtualBoardProfileConsumer
    {
        public const int MaxScheduledInputEvents = 4096;
        public const int EepromSize = 256;
        public const int DmaBufferCount = 4;
        public const int DmaBufferCapacity = 256;
        public const int DmaQueueCapacity = 16;
        public const int DmaMaximumTicket = int.MaxValue;
        public const int DmaQueueFull = -2;

        private readonly Dictionary<byte, VirtualPinMode> modes = new Dictionary<byte, VirtualPinMode>();
        private readonly Dictionary<byte, bool> values = new Dictionary<byte, bool>();
        private readonly Dictionary<byte, float> pwmValues = new Dictionary<byte, float>();
        private readonly Dictionary<byte, ushort> analogValues = new Dictionary<byte, ushort>();
        private readonly Dictionary<byte, Func<double, ushort>> analogSensors =
            new Dictionary<byte, Func<double, ushort>>();
        private readonly Dictionary<byte, byte> spiResponses = new Dictionary<byte, byte>();
        private readonly Dictionary<byte, byte> i2cResponses = new Dictionary<byte, byte>();
        private readonly Dictionary<byte, byte> dmaResponses = new Dictionary<byte, byte>();
        private readonly byte[][] dmaBuffers =
        {
            new byte[DmaBufferCapacity], new byte[DmaBufferCapacity],
            new byte[DmaBufferCapacity], new byte[DmaBufferCapacity]
        };
        private readonly List<VirtualDmaTransfer> dmaTransfers = new List<VirtualDmaTransfer>();
        private readonly byte[] eeprom = new byte[EepromSize];
        private readonly Dictionary<byte, VirtualGpioPinState> states = new Dictionary<byte, VirtualGpioPinState>();
        private readonly Dictionary<byte, bool?> externalDrives = new Dictionary<byte, bool?>();
        private readonly Dictionary<byte, VirtualGpioElectricalState> electricalStates =
            new Dictionary<byte, VirtualGpioElectricalState>();
        private VirtualBoardProfile boardProfile;
        private readonly List<ScheduledInput> scheduledInputs = new List<ScheduledInput>();
        private ulong nextInputSequence;
        private int nextDmaTicket = 1;
        private double dmaTimeMilliseconds;

        private sealed class VirtualDmaTransfer
        {
            public int Ticket;
            public byte Channel;
            public byte[] ReceiveData;
            public VirtualDmaStatus Status;
            public double CompletionTimeMilliseconds;
        }

        private double dmaByteTimeMilliseconds = 0.01;

        public double DmaByteTimeMilliseconds
        {
            get { return dmaByteTimeMilliseconds; }
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "DMA byte time must be a finite positive value");
                dmaByteTimeMilliseconds = value;
            }
        }

        private sealed class ScheduledInput
        {
            public byte Pin;
            public bool Level;
            public double Timestamp;
            public ulong Sequence;
        }

        public event Action<byte, bool> OutputChanged;
        public event Action<VirtualGpioInputEvent> InputEdge;
        public IList<string> ElectricalDiagnostics { get; } = new List<string>();
        public double? NextInputTimestampMilliseconds => scheduledInputs.Count == 0
            ? (double?)null : scheduledInputs[0].Timestamp;

        public void PinMode(byte pin, VirtualPinMode mode)
        {
            ConfigurePin(pin, mode, VirtualPinPullMode.None, VirtualPinOutputType.PushPull);
        }

        public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
            VirtualPinOutputType outputType)
        {
            ValidatePinConfiguration(pin, mode, pullMode, outputType);
            modes[pin] = mode;
            if (!values.ContainsKey(pin))
                values[pin] = mode == VirtualPinMode.Input && pullMode == VirtualPinPullMode.PullUp;
            if (!states.TryGetValue(pin, out var state))
            {
                state = new VirtualGpioPinState { VirtualPin = pin };
                states.Add(pin, state);
            }
            state.Mode = mode;
            state.PullMode = pullMode;
            state.OutputType = outputType;
            state.LatchLevel = values[pin];
            if (!pwmValues.ContainsKey(pin))
                pwmValues[pin] = values[pin] ? 1f : 0f;
            if (mode == VirtualPinMode.Input && !state.LineLevel && pullMode == VirtualPinPullMode.PullUp)
                values[pin] = true;
            state.LineLevel = values[pin];
            RecomputeElectricalState(pin);
        }

        public void DigitalWrite(byte pin, bool value)
        {
            if (!modes.TryGetValue(pin, out var mode))
                throw new InvalidOperationException("GPIO pin has not been configured");
            if (mode != VirtualPinMode.Output)
                throw new InvalidOperationException("GPIO write requires output mode");

            values.TryGetValue(pin, out var previous);
            values[pin] = value;
            states[pin].LatchLevel = value;
            pwmValues[pin] = value ? 1f : 0f;
            RecomputeElectricalState(pin);
            if (previous != values[pin])
                OutputChanged?.Invoke(pin, values[pin]);
        }

        public bool DigitalRead(byte pin)
        {
            EnsureConfiguredPin(pin);
            RecomputeElectricalState(pin);
            return states[pin].LineLevel;
        }

        public float GetOutputValue(byte pin)
        {
            if (!modes.ContainsKey(pin))
                throw new InvalidOperationException("GPIO pin has not been configured");
            return pwmValues.TryGetValue(pin, out var value) ? value : (values[pin] ? 1f : 0f);
        }

        public float ReadPinValue(byte pin)
        {
            EnsureConfiguredPin(pin);
            RecomputeElectricalState(pin);
            var state = states[pin];
            if (state.Mode == VirtualPinMode.Output &&
                state.OutputType == VirtualPinOutputType.PushPull &&
                pwmValues.TryGetValue(pin, out var dutyCycle))
                return dutyCycle;
            return state.LineLevel ? 1f : 0f;
        }

        public void SetAnalogInput(byte channel, ushort value)
        {
            if (value > 4095)
                throw new ArgumentOutOfRangeException(nameof(value), "ADC sample must be between 0 and 4095");
            analogValues[channel] = value;
        }

        public bool SupportsAnalogChannel(byte channel)
        {
            return boardProfile == null ||
                (boardProfile.TryGetPin(channel, out var definition) &&
                    definition.Supports(VirtualPinCapabilities.Analog));
        }

        public void ClearAnalogInput(byte channel)
        {
            analogValues.Remove(channel);
        }

        public void RegisterAnalogSensor(byte channel, Func<double, ushort> sampler)
        {
            if (sampler == null)
                throw new ArgumentNullException(nameof(sampler));
            if (!SupportsAnalogChannel(channel))
                throw new InvalidOperationException("ADC channel is not supported");
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
                analogValues[sensor.Key] = sensor.Value(simulatedTimeMilliseconds);
        }

        public ushort AnalogRead(byte channel)
        {
            if (!SupportsAnalogChannel(channel))
                throw new InvalidOperationException("ADC channel is not supported by the board profile");
            return analogValues.TryGetValue(channel, out var value) ? value : (ushort)0;
        }

        public void SetExternalDrive(byte virtualPin, bool level)
        {
            EnsureConfiguredPin(virtualPin);
            externalDrives[virtualPin] = level;
            RecomputeElectricalState(virtualPin);
        }

        public void ClearExternalDrive(byte virtualPin)
        {
            EnsureConfiguredPin(virtualPin);
            externalDrives[virtualPin] = null;
            RecomputeElectricalState(virtualPin);
        }

        public VirtualGpioElectricalState GetElectricalState(byte virtualPin)
        {
            EnsureConfiguredPin(virtualPin);
            RecomputeElectricalState(virtualPin);
            return electricalStates[virtualPin];
        }

        public void ApplyBoardProfile(VirtualBoardProfile profile)
        {
            boardProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            foreach (var definition in profile.PinDefinitions)
            {
                if (modes.ContainsKey(definition.VirtualPin))
                    ValidatePinConfiguration(definition.VirtualPin, definition.ResetMode,
                        definition.ResetPullMode, definition.OutputType);
            }
        }

        public void SetSpiResponse(byte bus, byte value)
        {
            spiResponses[bus] = value;
        }

        public byte SpiTransfer(byte bus, byte value)
        {
            return spiResponses.TryGetValue(bus, out var response) ? response : (byte)0;
        }

        public void SetI2cResponse(byte address, byte value)
        {
            if (address > 0x7F)
                throw new ArgumentOutOfRangeException(nameof(address), "I2C address must be 7-bit");
            i2cResponses[address] = value;
        }

        public byte I2cTransfer(byte address, byte value)
        {
            if (address > 0x7F)
                throw new InvalidOperationException("I2C address must be 7-bit");
            return i2cResponses.TryGetValue(address, out var response) ? response : (byte)0;
        }

        public void SetDmaResponse(byte channel, byte value)
        {
            if (channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel), "DMA channel must be between 0 and 3");
            dmaResponses[channel] = value;
        }

        public byte DmaTransfer(byte channel, byte value)
        {
            if (channel > 3)
                throw new InvalidOperationException("DMA channel must be between 0 and 3");
            return dmaResponses.TryGetValue(channel, out var response) ? response : (byte)0;
        }

        public void DmaBufferWrite(byte buffer, byte offset, byte value)
        {
            GetDmaBuffer(buffer)[offset] = value;
        }

        public byte DmaBufferRead(byte buffer, byte offset)
        {
            return GetDmaBuffer(buffer)[offset];
        }

        public int DmaSubmit(byte channel, byte buffer, int length)
        {
            if (channel > 3)
                throw new InvalidOperationException("DMA channel must be between 0 and 3");
            if (length < 1 || length > DmaBufferCapacity)
                throw new InvalidOperationException("DMA transfer length must be between 1 and 256 bytes");
            if (double.IsNaN(DmaByteTimeMilliseconds) || double.IsInfinity(DmaByteTimeMilliseconds) ||
                DmaByteTimeMilliseconds <= 0)
                throw new InvalidOperationException("DMA byte time must be a finite positive value");

            if (dmaTransfers.Count >= DmaQueueCapacity)
                return DmaQueueFull;

            var ticket = AllocateDmaTicket();
            var transmitData = new byte[length];
            Array.Copy(GetDmaBuffer(buffer), transmitData, length);
            dmaTransfers.Add(new VirtualDmaTransfer
            {
                Ticket = ticket,
                Channel = channel,
                ReceiveData = transmitData,
                Status = VirtualDmaStatus.Queued
            });
            StartNextDmaTransfer(channel, dmaTimeMilliseconds);
            return ticket;
        }

        public int DmaStatus(int ticket)
        {
            var transfer = FindDmaTransfer(ticket);
            return transfer == null ? (int)VirtualDmaStatus.Invalid : (int)transfer.Status;
        }

        public byte DmaReadByte(int ticket, byte offset)
        {
            var transfer = FindDmaTransfer(ticket);
            if (transfer == null || transfer.Status != VirtualDmaStatus.Complete)
                throw new InvalidOperationException("DMA result is not complete or ticket is invalid");
            if (offset >= transfer.ReceiveData.Length)
                throw new InvalidOperationException("DMA result offset is outside the transfer length");
            return transfer.ReceiveData[offset];
        }

        public bool DmaRelease(int ticket)
        {
            var transfer = FindDmaTransfer(ticket);
            if (transfer == null || transfer.Status != VirtualDmaStatus.Complete)
                return false;
            dmaTransfers.Remove(transfer);
            return true;
        }

        public void AdvanceDmaTo(double simulatedTimeMilliseconds)
        {
            if (double.IsNaN(simulatedTimeMilliseconds) || double.IsInfinity(simulatedTimeMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(simulatedTimeMilliseconds));
            if (simulatedTimeMilliseconds <= dmaTimeMilliseconds)
                return;

            // A transfer completion can start at most one already queued transfer.
            // Therefore one AdvanceDmaTo call can process no more completions than
            // there are transfers at its start. Keep the bound so corrupted timing
            // state cannot turn this scheduler into an unbounded loop.
            var completionBudget = dmaTransfers.Count;
            while (completionBudget-- > 0)
            {
                VirtualDmaTransfer nextCompletion = null;
                for (var i = 0; i < dmaTransfers.Count; i++)
                {
                    var candidate = dmaTransfers[i];
                    if (candidate.Status == VirtualDmaStatus.Active &&
                        (nextCompletion == null || candidate.CompletionTimeMilliseconds < nextCompletion.CompletionTimeMilliseconds))
                        nextCompletion = candidate;
                }
                if (nextCompletion == null || nextCompletion.CompletionTimeMilliseconds > simulatedTimeMilliseconds)
                    break;

                var completedAt = nextCompletion.CompletionTimeMilliseconds;
                var completedChannel = nextCompletion.Channel;
                dmaTimeMilliseconds = completedAt;
                nextCompletion.Status = VirtualDmaStatus.Complete;
                StartNextDmaTransfer(completedChannel, completedAt);
            }

            for (var i = 0; i < dmaTransfers.Count; i++)
            {
                var transfer = dmaTransfers[i];
                if (transfer.Status == VirtualDmaStatus.Active &&
                    transfer.CompletionTimeMilliseconds <= simulatedTimeMilliseconds)
                    throw new InvalidOperationException("DMA completion scheduler made no progress");
            }

            dmaTimeMilliseconds = simulatedTimeMilliseconds;
            for (byte channel = 0; channel < 4; channel++)
                StartNextDmaTransfer(channel, simulatedTimeMilliseconds);
        }

        public double? NextDmaCompletionTimeMilliseconds
        {
            get
            {
                double? next = null;
                for (var i = 0; i < dmaTransfers.Count; i++)
                {
                    var transfer = dmaTransfers[i];
                    if (transfer.Status != VirtualDmaStatus.Active)
                        continue;
                    if (!next.HasValue || transfer.CompletionTimeMilliseconds < next.Value)
                        next = transfer.CompletionTimeMilliseconds;
                }
                return next;
            }
        }

        public void ResetDma()
        {
            dmaTransfers.Clear();
            dmaTimeMilliseconds = 0.0;
            for (var i = 0; i < dmaBuffers.Length; i++)
                Array.Clear(dmaBuffers[i], 0, dmaBuffers[i].Length);
        }

        private byte[] GetDmaBuffer(byte buffer)
        {
            if (buffer >= dmaBuffers.Length)
                throw new InvalidOperationException("DMA buffer index must be between 0 and 3");
            return dmaBuffers[buffer];
        }

        private int AllocateDmaTicket()
        {
            for (var attempt = 0; attempt < DmaQueueCapacity + 1; attempt++)
            {
                var candidate = nextDmaTicket;
                nextDmaTicket = nextDmaTicket == int.MaxValue ? 1 : nextDmaTicket + 1;
                if (FindDmaTransfer(candidate) == null)
                    return candidate;
            }
            return -1;
        }

        private VirtualDmaTransfer FindDmaTransfer(int ticket)
        {
            for (var i = 0; i < dmaTransfers.Count; i++)
            {
                if (dmaTransfers[i].Ticket == ticket)
                    return dmaTransfers[i];
            }
            return null;
        }

        private bool HasActiveDmaTransfer(byte channel)
        {
            for (var i = 0; i < dmaTransfers.Count; i++)
            {
                if (dmaTransfers[i].Status == VirtualDmaStatus.Active && dmaTransfers[i].Channel == channel)
                    return true;
            }
            return false;
        }

        private void StartNextDmaTransfer(byte channel, double simulatedTimeMilliseconds)
        {
            if (HasActiveDmaTransfer(channel))
                return;
            for (var i = 0; i < dmaTransfers.Count; i++)
            {
                var transfer = dmaTransfers[i];
                if (transfer.Status != VirtualDmaStatus.Queued || transfer.Channel != channel)
                    continue;
                transfer.Status = VirtualDmaStatus.Active;
                transfer.CompletionTimeMilliseconds = simulatedTimeMilliseconds +
                    transfer.ReceiveData.Length * DmaByteTimeMilliseconds;
                return;
            }
        }

        public byte EepromRead(byte address)
        {
            return eeprom[address];
        }

        public void EepromWrite(byte address, byte value)
        {
            eeprom[address] = value;
        }

        public void ClearStorage()
        {
            Array.Clear(eeprom, 0, eeprom.Length);
        }

        public void ResetPeripheralState()
        {
            scheduledInputs.Clear();
            ElectricalDiagnostics.Clear();
            externalDrives.Clear();
            var configuredPins = new List<byte>(states.Keys);
            foreach (var pin in configuredPins)
            {
                var state = states[pin];
                var wasOutput = modes[pin] == VirtualPinMode.Output;
                if (wasOutput && values[pin])
                    OutputChanged?.Invoke(pin, false);
                modes[pin] = VirtualPinMode.Input;
                if (wasOutput)
                    values[pin] = false;
                state.Mode = VirtualPinMode.Input;
                state.PullMode = VirtualPinPullMode.None;
                state.OutputType = VirtualPinOutputType.PushPull;
                state.LatchLevel = false;
                state.LineLevel = values[pin];
                pwmValues[pin] = 0f;
                RecomputeElectricalState(pin);
            }
            ResetDma();
        }

        public void ApplySafeState()
        {
            var configuredPins = new List<byte>(modes.Keys);
            foreach (var pin in configuredPins)
            {
                if (modes[pin] == VirtualPinMode.Output)
                    DigitalWrite(pin, false);
            }
        }

        public void PwmWrite(byte pin, byte dutyCycle)
        {
            if (boardProfile != null)
                boardProfile.GetPinWithCapability(pin, VirtualPinCapabilities.Pwm);
            if (!modes.TryGetValue(pin, out var mode))
                throw new InvalidOperationException("GPIO pin has not been configured");
            if (mode != VirtualPinMode.Output)
                throw new InvalidOperationException("PWM write requires output mode");

            var value = dutyCycle / 255f;
            pwmValues[pin] = value;
            var digitalValue = dutyCycle >= 128;
            var previous = values[pin];
            values[pin] = digitalValue;
            states[pin].LatchLevel = digitalValue;
            RecomputeElectricalState(pin);
            if (previous != values[pin])
                OutputChanged?.Invoke(pin, values[pin]);
        }

        private void EnsureConfiguredPin(byte pin)
        {
            if (!modes.ContainsKey(pin))
                throw new InvalidOperationException("GPIO pin has not been configured");
        }

        private void ValidatePinConfiguration(byte pin, VirtualPinMode mode,
            VirtualPinPullMode pullMode, VirtualPinOutputType outputType)
        {
            if (boardProfile == null)
                return;
            var definition = boardProfile.GetPin(pin);
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
        }

        private void RecomputeElectricalState(byte pin)
        {
            EnsureConfiguredPin(pin);
            var state = states[pin];
            externalDrives.TryGetValue(pin, out var external);
            var driverActive = state.Mode == VirtualPinMode.Output &&
                (state.OutputType == VirtualPinOutputType.PushPull || !state.LatchLevel);
            var driverLevel = state.LatchLevel;
            var highImpedance = !driverActive;
            var pullLevel = state.PullMode == VirtualPinPullMode.PullUp ? (bool?)true :
                state.PullMode == VirtualPinPullMode.PullDown ? (bool?)false : null;
            var line = highImpedance ? (external ?? pullLevel ?? false) : driverLevel;
            var conflict = driverActive && external.HasValue && external.Value != driverLevel;
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
            if (conflict)
            {
                var message = electrical.Diagnostic;
                if (!ElectricalDiagnostics.Contains(message))
                    ElectricalDiagnostics.Add(message);
            }
            else if (!string.IsNullOrEmpty(previousDiagnostic))
            {
                ElectricalDiagnostics.Remove(previousDiagnostic);
            }
        }

        public void ScheduleInputLevel(byte pin, double timestampMilliseconds, bool level)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) || timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            if (!modes.TryGetValue(pin, out var mode))
                throw new InvalidOperationException("GPIO pin has not been configured");
            if (mode != VirtualPinMode.Input)
                throw new InvalidOperationException("Input events require input mode");
            if (scheduledInputs.Count >= MaxScheduledInputEvents)
                throw new InvalidOperationException("Virtual GPIO input event capacity exceeded");

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
                var previous = states[input.Pin].LineLevel;
                externalDrives[input.Pin] = input.Level;
                RecomputeElectricalState(input.Pin);
                if (previous == input.Level)
                    continue;
                InputEdge?.Invoke(new VirtualGpioInputEvent(
                    input.Pin, previous, input.Level, input.Timestamp, input.Sequence,
                    input.Level ? VirtualGpioEdge.Rising : VirtualGpioEdge.Falling));
            }
        }

        public VirtualPinMode GetMode(byte pin)
        {
            return modes.TryGetValue(pin, out var mode) ? mode : throw new InvalidOperationException("GPIO pin has not been configured");
        }

        public VirtualGpioPinState GetState(byte pin)
        {
            if (!states.TryGetValue(pin, out var state))
                throw new InvalidOperationException("GPIO pin has not been configured");
            return state;
        }
    }

    public sealed class VirtualMachineFaultException : Exception
    {
        public VirtualMachineFaultException(string message) : base(message) { }
        public VirtualMachineFaultException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class VirtualMachineProfiler
    {
        public long InstructionsExecuted { get; private set; }
        public long HalCallsExecuted { get; private set; }
        public int MaximumValueStackDepth { get; private set; }
        public int MaximumCallStackDepth { get; private set; }
        public int MaximumInterruptStackDepth { get; private set; }

        public void Reset()
        {
            InstructionsExecuted = 0;
            HalCallsExecuted = 0;
            MaximumValueStackDepth = 0;
            MaximumCallStackDepth = 0;
            MaximumInterruptStackDepth = 0;
        }

        internal void RecordInstruction() => InstructionsExecuted++;
        internal void RecordHalCall() => HalCallsExecuted++;

        internal void RecordValueStackDepth(int depth)
        {
            if (depth > MaximumValueStackDepth)
                MaximumValueStackDepth = depth;
        }

        internal void RecordCallStackDepth(int depth)
        {
            if (depth > MaximumCallStackDepth)
                MaximumCallStackDepth = depth;
        }

        internal void RecordInterruptStackDepth(int depth)
        {
            if (depth > MaximumInterruptStackDepth)
                MaximumInterruptStackDepth = depth;
        }
    }

    public sealed class VirtualMachine
    {
        private const int MaxInstructionsPerAdvance = 100000;
        private const int MaxCallDepth = 256;
        public const int MaxValueStackDepth = 1024;

        private readonly RsfwPackage package;
        private readonly IVirtualMcuHal hal;
        private readonly IVirtualInputSource inputSource;
        private readonly IVirtualUartInputSource uartInputSource;
        private readonly IVirtualInterruptController interruptController;
        private readonly IVirtualUartHal uart;
        private readonly VirtualMachineProfiler profiler;
        private readonly VirtualMcuBackend backend;
        private readonly Stack<CallFrame> callStack = new Stack<CallFrame>();
        private readonly Stack<IrqReturnState> irqReturnStack = new Stack<IrqReturnState>();
        private readonly Queue<UartCallbackRequest> pendingUartCallbacks = new Queue<UartCallbackRequest>();
        private readonly UartCallbackReturnState uartCallbackReturnState = new UartCallbackReturnState();
        private readonly uint[] uartCallbackTargets = { uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue };
        private readonly bool[] uartCallbackPending = new bool[4];
        private readonly Queue<UartPacketCallbackRequest> pendingUartPacketCallbacks =
            new Queue<UartPacketCallbackRequest>();
        private readonly UartPacketCallbackReturnState uartPacketCallbackReturnState =
            new UartPacketCallbackReturnState();
        private readonly uint[] uartPacketCallbackTargets = { uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue };
        private readonly bool[] uartPacketCallbackPending = new bool[4];
        private readonly Stack<int> valueStack = new Stack<int>();
        private readonly int[] slots = new int[256];
        private readonly int[] globals = new int[256];
        private uint programCounter;
        private double wakeTimeMilliseconds;
        private VirtualExecutionState state;

        private sealed class CallFrame
        {
            public uint ReturnProgramCounter;
            public int[] SavedSlots;
        }

        private sealed class IrqReturnState
        {
            public uint ProgramCounter;
            public bool WasWaiting;
            public double WakeTimeMilliseconds;
        }

        private sealed class UartCallbackRequest
        {
            public byte Port;
            public ulong Sequence;
        }

        private sealed class UartCallbackReturnState
        {
            public byte Port;
            public uint ProgramCounter;
            public bool WasWaiting;
            public double WakeTimeMilliseconds;
            public bool Active;
        }

        private sealed class UartPacketCallbackRequest
        {
            public byte Port;
        }

        private sealed class UartPacketCallbackReturnState
        {
            public byte Port;
            public uint ProgramCounter;
            public bool WasWaiting;
            public double WakeTimeMilliseconds;
            public bool Active;
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal)
            : this(package, hal, null, null)
        {
        }

        public static VirtualMachine CreateWithBackend(RsfwPackage package,
            VirtualMcuBackend backend, VirtualMachineProfiler profiler = null)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));
            return new VirtualMachine(package, backend, profiler);
        }

        public VirtualMachine(RsfwPackage package, VirtualMcuBackend backend)
            : this(package, backend, null)
        {
        }

        private VirtualMachine(RsfwPackage package, VirtualMcuBackend backend,
            VirtualMachineProfiler profiler)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.hal = backend;
            this.backend = backend;
            backend.Configure(package);
            backend.Reset();
            inputSource = backend;
            uartInputSource = backend;
            interruptController = new VirtualInterruptController(backend);
            uart = backend;
            this.profiler = profiler;
            Trace = new VirtualTraceBuffer();
            if (uartInputSource != null)
            {
                uartInputSource.Received += OnUartReceived;
                uartInputSource.PacketReceived += OnUartPacketReceived;
            }
            programCounter = package.Bytecode.EntryPoint;
            State = VirtualExecutionState.Ready;
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController)
            : this(package, hal, interruptController, null)
        {
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController, IVirtualUartHal uart)
            : this(package, hal, interruptController, uart, null)
        {
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController, IVirtualUartHal uart,
            VirtualMachineProfiler profiler)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.hal = hal ?? throw new ArgumentNullException(nameof(hal));
            inputSource = hal as IVirtualInputSource;
            uartInputSource = uart as IVirtualUartInputSource;
            this.interruptController = interruptController ??
                (hal is IVirtualGpioEdgeSource edgeSource ? new VirtualInterruptController(edgeSource) : null);
            this.uart = uart;
            this.profiler = profiler;
            Trace = new VirtualTraceBuffer();
            if (uartInputSource != null)
            {
                uartInputSource.Received += OnUartReceived;
                uartInputSource.PacketReceived += OnUartPacketReceived;
            }
            programCounter = package.Bytecode.EntryPoint;
            State = VirtualExecutionState.Ready;
        }

        public VirtualExecutionState State
        {
            get { return state; }
            private set
            {
                if (state == value)
                    return;
                var previous = state;
                state = value;
                if (backend != null)
                {
                    if (value == VirtualExecutionState.Running)
                        backend.Start();
                    else if (value == VirtualExecutionState.Halted)
                        backend.Stop();
                }
                if (Trace != null)
                    RecordTrace(VirtualTraceEventKind.StateTransition, -1,
                        (int)previous, (int)value);
            }
        }
        public uint ProgramCounter => programCounter;
        public double SimulatedTimeMilliseconds { get; private set; }
        public string FaultMessage { get; private set; }
        public int StackDepth => valueStack.Count;
        public int StackTop => valueStack.Count == 0 ? throw new InvalidOperationException("VM value stack is empty") : valueStack.Peek();
        public double WakeTimeMilliseconds => wakeTimeMilliseconds;
        public int InterruptDepth => irqReturnStack.Count;
        public VirtualMachineProfiler Profiler => profiler;
        public VirtualTraceBuffer Trace { get; }
        public event Action<VirtualTraceEvent> TraceRecorded;
        public event Action<VirtualInterruptRequest> InterruptDispatched;

        public List<VirtualTraceEvent> GetTraceSnapshot()
        {
            return Trace.Snapshot();
        }

        public void ClearTrace()
        {
            Trace.Clear();
        }

        public void Reset()
        {
            backend?.Reset();
            Trace.Clear();
            callStack.Clear();
            irqReturnStack.Clear();
            pendingUartCallbacks.Clear();
            uartCallbackReturnState.Active = false;
            for (var port = 0; port < uartCallbackTargets.Length; port++)
            {
                uartCallbackTargets[port] = uint.MaxValue;
                uartCallbackPending[port] = false;
                uartPacketCallbackTargets[port] = uint.MaxValue;
                uartPacketCallbackPending[port] = false;
            }
            pendingUartPacketCallbacks.Clear();
            uartPacketCallbackReturnState.Active = false;
            valueStack.Clear();
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(globals, 0, globals.Length);
            programCounter = package.Bytecode.EntryPoint;
            wakeTimeMilliseconds = 0.0;
            SetSimulatedTime(0.0);
            FaultMessage = null;
            State = VirtualExecutionState.Ready;
            profiler?.Reset();
        }

        public void Stop()
        {
            if (State == VirtualExecutionState.Faulted)
                return;
            State = VirtualExecutionState.Halted;
        }

        public void SafeState()
        {
            backend?.SafeState();
        }

        public bool Step()
        {
            if (State == VirtualExecutionState.Halted || State == VirtualExecutionState.Faulted)
                return false;

            try
            {
                AdvanceInputSourcesTo(SimulatedTimeMilliseconds);
                if (State == VirtualExecutionState.Ready)
                    State = VirtualExecutionState.Running;
                if (State == VirtualExecutionState.Yielded)
                    State = VirtualExecutionState.Running;

                if (TryDispatchUartCallback())
                    return true;
                if (TryDispatchUartPacketCallback())
                    return true;
                TryDispatchInterrupt();

                if (State == VirtualExecutionState.Waiting)
                {
                    if (wakeTimeMilliseconds > SimulatedTimeMilliseconds)
                        return false;
                    State = VirtualExecutionState.Running;
                }

                if (State != VirtualExecutionState.Running)
                    return false;

                ExecuteInstruction(package.Bytecode.Instructions[(int)programCounter]);
                if (State == VirtualExecutionState.Running && programCounter >= package.Bytecode.Instructions.Count)
                    throw new VirtualMachineFaultException("Program counter is outside bytecode");
                return true;
            }
            catch (Exception exception)
            {
                State = VirtualExecutionState.Faulted;
                FaultMessage = exception.Message;
                backend?.MarkFaulted(exception.Message);
                RecordRejectedMappingIfNeeded(exception);
                RecordTrace(VirtualTraceEventKind.Fault, -1, 0, 0, exception.Message);
                if (exception is VirtualMachineFaultException)
                    throw;
                throw new VirtualMachineFaultException(
                    "Virtual MCU execution failed at instruction " + programCounter, exception);
            }
        }

        public void Advance(double deltaMilliseconds)
        {
            if (deltaMilliseconds < 0 || double.IsNaN(deltaMilliseconds) || double.IsInfinity(deltaMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
            if (State == VirtualExecutionState.Faulted)
                return;
            if (State == VirtualExecutionState.Halted)
            {
                AdvanceInputSourcesTo(SimulatedTimeMilliseconds);
                return;
            }

            if (State == VirtualExecutionState.Yielded)
                State = VirtualExecutionState.Running;

            var targetTime = SimulatedTimeMilliseconds + deltaMilliseconds;
            AdvanceDmaTo(SimulatedTimeMilliseconds);
            if (State == VirtualExecutionState.Ready)
                State = VirtualExecutionState.Running;

            try
            {
                var executed = 0;
                while (State != VirtualExecutionState.Halted && State != VirtualExecutionState.Faulted)
                {
                    AdvanceInputSourcesTo(SimulatedTimeMilliseconds);
                    if (TryDispatchUartCallback())
                        continue;
                    if (TryDispatchUartPacketCallback())
                        continue;
                    if (TryDispatchInterrupt())
                        continue;
                    if (State == VirtualExecutionState.Waiting)
                    {
                        var nextInput = NextInputTimestampMilliseconds();
                        var nextDma = (hal as IVirtualAsyncDmaHal)?.NextDmaCompletionTimeMilliseconds;
                        var nextEvent = nextInput;
                        if (nextDma.HasValue && (!nextEvent.HasValue || nextDma.Value < nextEvent.Value))
                            nextEvent = nextDma;
                        if (nextEvent.HasValue && nextEvent.Value <= wakeTimeMilliseconds && nextEvent.Value <= targetTime)
                        {
                            SetSimulatedTime(nextEvent.Value);
                            AdvanceInputSourcesTo(SimulatedTimeMilliseconds);
                            if (TryDispatchUartCallback())
                                continue;
                            if (TryDispatchUartPacketCallback())
                                continue;
                            continue;
                        }
                        if (wakeTimeMilliseconds > targetTime)
                        {
                            SetSimulatedTime(targetTime);
                            AdvanceInputSourcesTo(targetTime);
                            return;
                        }
                        SetSimulatedTime(wakeTimeMilliseconds);
                        AdvanceInputSourcesTo(SimulatedTimeMilliseconds);
                        State = VirtualExecutionState.Running;
                    }
                    if (State != VirtualExecutionState.Running)
                        break;
                    if (++executed > MaxInstructionsPerAdvance)
                        throw new VirtualMachineFaultException("Instruction budget exceeded; possible tight loop");
                    ExecuteInstruction(package.Bytecode.Instructions[(int)programCounter]);
                }
                SetSimulatedTime(targetTime);
                AdvanceInputSourcesTo(targetTime);
            }
            catch (Exception exception)
            {
                State = VirtualExecutionState.Faulted;
                FaultMessage = exception.Message;
                backend?.MarkFaulted(exception.Message);
                RecordRejectedMappingIfNeeded(exception);
                RecordTrace(VirtualTraceEventKind.Fault, -1, 0, 0, exception.Message);
                if (exception is VirtualMachineFaultException)
                    throw;
                throw new VirtualMachineFaultException("Virtual MCU execution failed at instruction " + programCounter, exception);
            }
        }

        private void ExecuteInstruction(RsfwInstruction instruction)
        {
            profiler?.RecordInstruction();
            var next = programCounter + 1;
            switch (instruction.Opcode)
            {
                case RsfwOpcode.Nop:
                    break;
                case RsfwOpcode.Halt:
                    State = VirtualExecutionState.Halted;
                    return;
                case RsfwOpcode.ConstI32:
                    PushValue(unchecked((int)ReadU32(instruction.Operands, 0)));
                    break;
                case RsfwOpcode.Load:
                    PushValue(slots[ReadU16(instruction.Operands, 0)]);
                    break;
                case RsfwOpcode.Store:
                    slots[ReadU16(instruction.Operands, 0)] = PopValue();
                    break;
                case RsfwOpcode.GlobalLoad:
                    PushValue(globals[ReadU16(instruction.Operands, 0)]);
                    break;
                case RsfwOpcode.GlobalStore:
                    globals[ReadU16(instruction.Operands, 0)] = PopValue();
                    break;
                case RsfwOpcode.Yield:
                    State = VirtualExecutionState.Yielded;
                    break;
                case RsfwOpcode.AddI32:
                    PushValue(unchecked(PopValue() + PopValue()));
                    break;
                case RsfwOpcode.SubI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    PushValue(unchecked(left - right));
                    break;
                }
                case RsfwOpcode.CmpEqI32:
                    PushValue(PopValue() == PopValue() ? 1 : 0);
                    break;
                case RsfwOpcode.CmpNeI32:
                    PushValue(PopValue() != PopValue() ? 1 : 0);
                    break;
                case RsfwOpcode.CmpLtI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    PushValue(left < right ? 1 : 0);
                    break;
                }
                case RsfwOpcode.CmpLeI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    PushValue(left <= right ? 1 : 0);
                    break;
                }
                case RsfwOpcode.CmpGtI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    PushValue(left > right ? 1 : 0);
                    break;
                }
                case RsfwOpcode.CmpGeI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    PushValue(left >= right ? 1 : 0);
                    break;
                }
                case RsfwOpcode.Jump:
                    next = ReadU32(instruction.Operands, 0);
                    break;
                case RsfwOpcode.JumpIf:
                    if (PopValue() != 0)
                        next = ReadU32(instruction.Operands, 0);
                    break;
                case RsfwOpcode.Call:
                    if (callStack.Count >= MaxCallDepth)
                        throw new VirtualMachineFaultException("CALL stack depth limit exceeded");
                    var savedSlots = new int[slots.Length];
                    Array.Copy(slots, savedSlots, slots.Length);
                    callStack.Push(new CallFrame { ReturnProgramCounter = next, SavedSlots = savedSlots });
                    profiler?.RecordCallStackDepth(callStack.Count);
                    next = ReadU32(instruction.Operands, 0);
                    break;
                case RsfwOpcode.Return:
                    if (callStack.Count == 0)
                    {
                        State = VirtualExecutionState.Halted;
                        return;
                    }
                    var callFrame = callStack.Pop();
                    Array.Copy(callFrame.SavedSlots, slots, slots.Length);
                    next = callFrame.ReturnProgramCounter;
                    break;
                case RsfwOpcode.ReadTime:
                    PushValue(unchecked((int)SimulatedTimeMilliseconds));
                    break;
                case RsfwOpcode.Wait:
                    wakeTimeMilliseconds = SimulatedTimeMilliseconds + ReadU32(instruction.Operands, 0);
                    State = VirtualExecutionState.Waiting;
                    break;
                case RsfwOpcode.HalCall:
                    ExecuteHalCall(instruction.Operands);
                    break;
                case RsfwOpcode.AttachIrq:
                    RequireInterruptController().Attach(instruction.Operands[0], instruction.Operands[1],
                        ReadU32(instruction.Operands, 2));
                    break;
                case RsfwOpcode.DetachIrq:
                    RequireInterruptController().Detach(instruction.Operands[0]);
                    break;
                case RsfwOpcode.AttachUartRx:
                    if (instruction.Operands[0] >= uartCallbackTargets.Length)
                        throw new VirtualMachineFaultException("UART callback port is outside the supported range");
                    uartCallbackTargets[instruction.Operands[0]] = ReadU32(instruction.Operands, 1);
                    break;
                case RsfwOpcode.DetachUartRx:
                    if (instruction.Operands[0] >= uartCallbackTargets.Length)
                        throw new VirtualMachineFaultException("UART callback port is outside the supported range");
                    uartCallbackTargets[instruction.Operands[0]] = uint.MaxValue;
                    break;
                case RsfwOpcode.AttachUartPacketRx:
                    if (instruction.Operands[0] >= uartPacketCallbackTargets.Length)
                        throw new VirtualMachineFaultException("UART packet callback port is outside the supported range");
                    uartPacketCallbackTargets[instruction.Operands[0]] = ReadU32(instruction.Operands, 1);
                    break;
                case RsfwOpcode.DetachUartPacketRx:
                    if (instruction.Operands[0] >= uartPacketCallbackTargets.Length)
                        throw new VirtualMachineFaultException("UART packet callback port is outside the supported range");
                    uartPacketCallbackTargets[instruction.Operands[0]] = uint.MaxValue;
                    break;
                case RsfwOpcode.EnableIrq:
                    RequireInterruptController().Enable();
                    break;
                case RsfwOpcode.DisableIrq:
                    RequireInterruptController().Disable();
                    break;
                case RsfwOpcode.ReturnIrq:
                    if (irqReturnStack.Count == 0)
                        throw new VirtualMachineFaultException("RETURN_IRQ executed outside an interrupt handler");
                    var returnState = irqReturnStack.Pop();
                    wakeTimeMilliseconds = returnState.WakeTimeMilliseconds;
                    State = returnState.WasWaiting && wakeTimeMilliseconds > SimulatedTimeMilliseconds
                        ? VirtualExecutionState.Waiting : VirtualExecutionState.Running;
                    next = returnState.ProgramCounter;
                    break;
                case RsfwOpcode.ReturnUartRx:
                    if (!uartCallbackReturnState.Active)
                        throw new VirtualMachineFaultException("RETURN_UART_RX executed outside a UART callback");
                    wakeTimeMilliseconds = uartCallbackReturnState.WakeTimeMilliseconds;
                    State = uartCallbackReturnState.WasWaiting && wakeTimeMilliseconds > SimulatedTimeMilliseconds
                        ? VirtualExecutionState.Waiting : VirtualExecutionState.Running;
                    next = uartCallbackReturnState.ProgramCounter;
                    if (RequireUart().Available(uartCallbackReturnState.Port) != 0 &&
                        uartCallbackTargets[uartCallbackReturnState.Port] != uint.MaxValue &&
                        !uartCallbackPending[uartCallbackReturnState.Port])
                    {
                        uartCallbackPending[uartCallbackReturnState.Port] = true;
                        pendingUartCallbacks.Enqueue(new UartCallbackRequest
                        {
                            Port = uartCallbackReturnState.Port,
                            Sequence = Trace.Count == 0 ? 0 : Trace[Trace.Count - 1].Sequence
                        });
                    }
                    uartCallbackReturnState.Active = false;
                    break;
                case RsfwOpcode.ReturnUartPacketRx:
                    if (!uartPacketCallbackReturnState.Active)
                        throw new VirtualMachineFaultException("RETURN_UART_PACKET_RX executed outside a packet callback");
                    wakeTimeMilliseconds = uartPacketCallbackReturnState.WakeTimeMilliseconds;
                    State = uartPacketCallbackReturnState.WasWaiting && wakeTimeMilliseconds > SimulatedTimeMilliseconds
                        ? VirtualExecutionState.Waiting : VirtualExecutionState.Running;
                    next = uartPacketCallbackReturnState.ProgramCounter;
                    uartPacketCallbackReturnState.Active = false;
                    RequireUart().CompletePacket(uartPacketCallbackReturnState.Port);
                    if (RequireUart().HasPacket(uartPacketCallbackReturnState.Port) &&
                        uartPacketCallbackTargets[uartPacketCallbackReturnState.Port] != uint.MaxValue &&
                        !uartPacketCallbackPending[uartPacketCallbackReturnState.Port])
                    {
                        uartPacketCallbackPending[uartPacketCallbackReturnState.Port] = true;
                        pendingUartPacketCallbacks.Enqueue(new UartPacketCallbackRequest
                        {
                            Port = uartPacketCallbackReturnState.Port
                        });
                    }
                    break;
                default:
                    throw new VirtualMachineFaultException("Unsupported opcode " + instruction.Opcode);
            }

            programCounter = next;
        }

        private void SetSimulatedTime(double timeMilliseconds)
        {
            SimulatedTimeMilliseconds = timeMilliseconds;
            RecordTrace(VirtualTraceEventKind.TimeAdvanced, -1, 0);
            AdvanceAnalogTo(timeMilliseconds);
            AdvanceDmaTo(timeMilliseconds);
        }

        private void RecordTrace(VirtualTraceEventKind kind, int channel, int value,
            int auxiliaryValue = 0, string message = null)
        {
            var traceEvent = Trace.Record(kind, SimulatedTimeMilliseconds, channel, value,
                auxiliaryValue, message);
            TraceRecorded?.Invoke(traceEvent);
        }

        private double? NextInputTimestampMilliseconds()
        {
            var nextInput = inputSource?.NextInputTimestampMilliseconds;
            var nextUart = uartInputSource?.NextInputTimestampMilliseconds;
            if (nextUart.HasValue && (!nextInput.HasValue || nextUart.Value < nextInput.Value))
                return nextUart;
            return nextInput;
        }

        private void AdvanceInputSourcesTo(double timestampMilliseconds)
        {
            inputSource?.AdvanceInputTo(timestampMilliseconds);
            if (uartInputSource != null && !ReferenceEquals(uartInputSource, inputSource))
                uartInputSource.AdvanceInputTo(timestampMilliseconds);
        }

        private void OnUartReceived(byte port, byte value)
        {
            RecordTrace(VirtualTraceEventKind.UartReceive, port, value);
            if (port < uartCallbackTargets.Length && uartCallbackTargets[port] != uint.MaxValue)
            {
                if (!uartCallbackPending[port])
                {
                    uartCallbackPending[port] = true;
                    pendingUartCallbacks.Enqueue(new UartCallbackRequest
                    {
                        Port = port,
                        Sequence = Trace.Count == 0 ? 0 : Trace[Trace.Count - 1].Sequence
                    });
                }
            }
        }

        private void OnUartPacketReceived(byte port, HitlFrame frame)
        {
            RecordTrace(VirtualTraceEventKind.UartPacketReceive, port, frame.Payload.Length,
                (int)frame.Type);
            if (port < uartPacketCallbackTargets.Length && uartPacketCallbackTargets[port] != uint.MaxValue &&
                !uartPacketCallbackPending[port])
            {
                uartPacketCallbackPending[port] = true;
                pendingUartPacketCallbacks.Enqueue(new UartPacketCallbackRequest { Port = port });
            }
        }

        private bool TryDispatchUartCallback()
        {
            if (uartCallbackReturnState.Active || pendingUartCallbacks.Count == 0)
                return false;
            var request = pendingUartCallbacks.Dequeue();
            var target = uartCallbackTargets[request.Port];
            if (target == uint.MaxValue)
            {
                uartCallbackPending[request.Port] = false;
                return false;
            }
            uartCallbackPending[request.Port] = false;
            if (RequireUart().Available(request.Port) == 0)
                return false;
            uartCallbackReturnState.ProgramCounter = programCounter;
            uartCallbackReturnState.Port = request.Port;
            uartCallbackReturnState.WasWaiting = State == VirtualExecutionState.Waiting;
            uartCallbackReturnState.WakeTimeMilliseconds = wakeTimeMilliseconds;
            uartCallbackReturnState.Active = true;
            programCounter = target;
            State = VirtualExecutionState.Running;
            RecordTrace(VirtualTraceEventKind.UartCallback, request.Port, 0,
                unchecked((int)request.Sequence));
            return true;
        }

        private bool TryDispatchUartPacketCallback()
        {
            if (uartPacketCallbackReturnState.Active || pendingUartPacketCallbacks.Count == 0)
                return false;
            var request = pendingUartPacketCallbacks.Dequeue();
            var target = uartPacketCallbackTargets[request.Port];
            uartPacketCallbackPending[request.Port] = false;
            if (target == uint.MaxValue || !RequireUart().HasPacket(request.Port))
                return false;
            uartPacketCallbackReturnState.Port = request.Port;
            uartPacketCallbackReturnState.ProgramCounter = programCounter;
            uartPacketCallbackReturnState.WasWaiting = State == VirtualExecutionState.Waiting;
            uartPacketCallbackReturnState.WakeTimeMilliseconds = wakeTimeMilliseconds;
            uartPacketCallbackReturnState.Active = true;
            programCounter = target;
            State = VirtualExecutionState.Running;
            RecordTrace(VirtualTraceEventKind.UartPacketCallback, request.Port, 0);
            return true;
        }

        private void RecordRejectedMappingIfNeeded(Exception exception)
        {
            var message = exception.Message ?? string.Empty;
            if (message.IndexOf("not mapped", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("outside the supported range", StringComparison.OrdinalIgnoreCase) >= 0)
                RecordTrace(VirtualTraceEventKind.RejectedMapping, -1, 0, 0, message);
        }

        private void AdvanceAnalogTo(double timeMilliseconds)
        {
            if (hal is IVirtualAnalogInputSource analogInput)
                analogInput.AdvanceAnalogTo(timeMilliseconds);
        }

        private void AdvanceDmaTo(double timeMilliseconds)
        {
            if (hal is IVirtualAsyncDmaHal asyncDmaHal)
                asyncDmaHal.AdvanceDmaTo(timeMilliseconds);
        }

        private bool TryDispatchInterrupt()
        {
            if (interruptController == null || irqReturnStack.Count != 0 ||
                !interruptController.TryDequeue(out var request))
                return false;
            irqReturnStack.Push(new IrqReturnState
            {
                ProgramCounter = programCounter,
                WasWaiting = State == VirtualExecutionState.Waiting,
                WakeTimeMilliseconds = wakeTimeMilliseconds
            });
            profiler?.RecordInterruptStackDepth(irqReturnStack.Count);
            programCounter = request.TargetInstruction;
            State = VirtualExecutionState.Running;
            RecordTrace(VirtualTraceEventKind.Interrupt, request.VirtualPin,
                (int)request.Edge, unchecked((int)request.Sequence));
            InterruptDispatched?.Invoke(request);
            return true;
        }

        private IVirtualInterruptController RequireInterruptController()
        {
            if (interruptController == null)
                throw new VirtualMachineFaultException("Firmware requires an interrupt controller");
            return interruptController;
        }

        private void ExecuteHalCall(byte[] operands)
        {
            profiler?.RecordHalCall();
            var service = (RsmaHalService)ReadU16(operands, 0);
            var argument = operands[2];
            var value = operands[3];
            RecordTrace(VirtualTraceEventKind.HalCall, argument, value, (int)service,
                service.ToString());
            switch (service)
            {
                case RsmaHalService.PinMode:
                    if (value > (byte)VirtualPinMode.Output)
                        throw new VirtualMachineFaultException("Unsupported GPIO pin mode");
                    hal.PinMode(argument, (VirtualPinMode)value);
                    break;
                case RsmaHalService.ConfigurePin:
                    if ((value & 0x01) > (byte)VirtualPinMode.Output ||
                        ((value >> 1) & 0x03) > (byte)VirtualPinPullMode.PullDown ||
                        ((value >> 3) & 0x01) > (byte)VirtualPinOutputType.OpenDrain ||
                        (value & 0xF0) != 0)
                        throw new VirtualMachineFaultException("Unsupported GPIO configuration");
                    hal.ConfigurePin(
                        argument,
                        (VirtualPinMode)(value & 0x01),
                        (VirtualPinPullMode)((value >> 1) & 0x03),
                        (VirtualPinOutputType)((value >> 3) & 0x01));
                    break;
                case RsmaHalService.PwmWrite:
                    if (!(hal is IVirtualPwmHal pwmHal))
                        throw new VirtualMachineFaultException("Firmware requires PWM support");
                    pwmHal.PwmWrite(argument, value);
                    RecordTrace(VirtualTraceEventKind.PwmOutput, argument, value);
                    break;
                case RsmaHalService.AnalogWrite:
                    if (!(hal is IVirtualPwmHal dynamicPwmHal))
                        throw new VirtualMachineFaultException("Firmware requires PWM support");
                    var dynamicDutyCycle = PopValue();
                    if (dynamicDutyCycle < byte.MinValue || dynamicDutyCycle > byte.MaxValue)
                        throw new VirtualMachineFaultException("PWM duty must be between 0 and 255");
                    dynamicPwmHal.PwmWrite(argument, (byte)dynamicDutyCycle);
                    RecordTrace(VirtualTraceEventKind.PwmOutput, argument, dynamicDutyCycle);
                    break;
                case RsmaHalService.DigitalWrite:
                    hal.DigitalWrite(argument, value != 0);
                    RecordTrace(VirtualTraceEventKind.DigitalOutput, argument, value != 0 ? 1 : 0);
                    break;
                case RsmaHalService.DigitalRead:
                    PushValue(hal.DigitalRead(argument) ? 1 : 0);
                    break;
                case RsmaHalService.DelayMs:
                    wakeTimeMilliseconds = SimulatedTimeMilliseconds + value;
                    State = VirtualExecutionState.Waiting;
                    break;
                case RsmaHalService.Millis:
                    PushValue(unchecked((int)SimulatedTimeMilliseconds));
                    break;
                case RsmaHalService.UartBegin:
                    RequireUart().Begin(argument, value);
                    break;
                case RsmaHalService.UartAvailable:
                    PushValue(RequireUart().Available(argument));
                    break;
                case RsmaHalService.UartRead:
                    PushValue(RequireUart().Read(argument));
                    break;
                case RsmaHalService.UartWrite:
                {
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("UART write value must be between 0 and 255");
                    RequireUart().Write(argument, (byte)transmittedValue);
                    RecordTrace(VirtualTraceEventKind.UartTransmit, argument, transmittedValue);
                    break;
                }
                case RsmaHalService.UartPacketAvailable:
                    PushValue(RequireUart().PacketAvailable(argument));
                    break;
                case RsmaHalService.UartPacketRead:
                    PushValue(RequireUart().PacketRead(argument));
                    break;
                case RsmaHalService.UartPacketType:
                    PushValue(RequireUart().PacketType(argument));
                    break;
                case RsmaHalService.AnalogRead:
                {
                    if (!(hal is IVirtualAdcHal adcHal))
                        throw new VirtualMachineFaultException("Firmware requires ADC support");
                    var sample = adcHal.AnalogRead(argument);
                    PushValue(sample);
                    RecordTrace(VirtualTraceEventKind.AnalogSample, argument, sample);
                    break;
                }
                case RsmaHalService.SpiTransfer:
                {
                    if (value != 0)
                        throw new VirtualMachineFaultException("SPI transfer reserved value must be zero");
                    if (!(hal is IVirtualSpiHal spiHal))
                        throw new VirtualMachineFaultException("Firmware requires SPI support");
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("SPI transfer value must be between 0 and 255");
                    PushValue(spiHal.SpiTransfer(argument, (byte)transmittedValue));
                    break;
                }
                case RsmaHalService.I2cTransfer:
                {
                    if (value != 0)
                        throw new VirtualMachineFaultException("I2C transfer reserved value must be zero");
                    if (argument > 0x7F)
                        throw new VirtualMachineFaultException("I2C address must be 7-bit");
                    if (!(hal is IVirtualI2cHal i2cHal))
                        throw new VirtualMachineFaultException("Firmware requires I2C support");
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("I2C transfer value must be between 0 and 255");
                    PushValue(i2cHal.I2cTransfer(argument, (byte)transmittedValue));
                    break;
                }
                case RsmaHalService.DmaTransfer:
                {
                    if (value != 0)
                        throw new VirtualMachineFaultException("DMA transfer reserved value must be zero");
                    if (argument > 3)
                        throw new VirtualMachineFaultException("DMA channel must be between 0 and 3");
                    if (!(hal is IVirtualDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires DMA support");
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("DMA transfer value must be between 0 and 255");
                    PushValue(dmaHal.DmaTransfer(argument, (byte)transmittedValue));
                    break;
                }
                case RsmaHalService.DmaBufferWrite:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("DMA buffer value must be between 0 and 255");
                    dmaHal.DmaBufferWrite(argument, value, (byte)transmittedValue);
                    break;
                }
                case RsmaHalService.DmaBufferRead:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    PushValue(dmaHal.DmaBufferRead(argument, value));
                    break;
                }
                case RsmaHalService.DmaSubmit:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    var length = PopValue();
                    if (length < 1 || length > VirtualGpioBank.DmaBufferCapacity)
                        throw new VirtualMachineFaultException("DMA transfer length must be between 1 and 256 bytes");
                    PushValue(dmaHal.DmaSubmit(argument, value, length));
                    break;
                }
                case RsmaHalService.DmaStatus:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    var ticket = PopValue();
                    PushValue(ticket < 1 || ticket > VirtualGpioBank.DmaMaximumTicket
                        ? (int)VirtualDmaStatus.Invalid
                        : dmaHal.DmaStatus(ticket));
                    break;
                }
                case RsmaHalService.DmaReadByte:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    var ticket = PopValue();
                    if (ticket < 1 || ticket > VirtualGpioBank.DmaMaximumTicket)
                        throw new VirtualMachineFaultException("DMA ticket is invalid");
                    PushValue(dmaHal.DmaReadByte(ticket, argument));
                    break;
                }
                case RsmaHalService.DmaRelease:
                {
                    if (!(hal is IVirtualAsyncDmaHal dmaHal))
                        throw new VirtualMachineFaultException("Firmware requires asynchronous DMA support");
                    var ticket = PopValue();
                    PushValue(ticket < 1 || ticket > VirtualGpioBank.DmaMaximumTicket
                        ? 0
                        : dmaHal.DmaRelease(ticket) ? 1 : 0);
                    break;
                }
                case RsmaHalService.EepromRead:
                {
                    if (value != 0)
                        throw new VirtualMachineFaultException("EEPROM read reserved value must be zero");
                    if (!(hal is IVirtualEepromHal eepromHal))
                        throw new VirtualMachineFaultException("Firmware requires EEPROM support");
                    PushValue(eepromHal.EepromRead(argument));
                    break;
                }
                case RsmaHalService.EepromWrite:
                {
                    if (value != 0)
                        throw new VirtualMachineFaultException("EEPROM write reserved value must be zero");
                    if (!(hal is IVirtualEepromHal eepromHal))
                        throw new VirtualMachineFaultException("Firmware requires EEPROM support");
                    var transmittedValue = PopValue();
                    if (transmittedValue < byte.MinValue || transmittedValue > byte.MaxValue)
                        throw new VirtualMachineFaultException("EEPROM write value must be between 0 and 255");
                    eepromHal.EepromWrite(argument, (byte)transmittedValue);
                    break;
                }
                default:
                    throw new VirtualMachineFaultException("Unsupported HAL service " + service);
            }
        }

        private IVirtualUartHal RequireUart()
        {
            if (uart == null)
                throw new VirtualMachineFaultException("Firmware requires UART support");
            return uart;
        }

        private int PopValue()
        {
            if (valueStack.Count == 0)
                throw new VirtualMachineFaultException("VM value stack underflow");
            return valueStack.Pop();
        }

        private void PushValue(int value)
        {
            if (valueStack.Count >= MaxValueStackDepth)
                throw new VirtualMachineFaultException("VM value stack depth limit exceeded");
            valueStack.Push(value);
            profiler?.RecordValueStackDepth(valueStack.Count);
        }

        private static ushort ReadU16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        private static uint ReadU32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
        }
    }
}
