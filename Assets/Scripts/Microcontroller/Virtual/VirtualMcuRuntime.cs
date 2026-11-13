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

    public interface IVirtualPwmHal
    {
        void PwmWrite(byte pin, byte dutyCycle);
    }

    public interface IVirtualAdcHal
    {
        ushort AnalogRead(byte channel);
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

    public interface IVirtualUartHal
    {
        void Begin(byte port, byte baudRatePreset);
        int Available(byte port);
        byte Read(byte port);
        void Write(byte port, byte value);
    }

    public sealed class VirtualUartBank : IVirtualUartHal
    {
        public const byte PortCount = 4;
        public const byte BaudRateCount = 5;
        public const int ReceiveBufferCapacity = 256;

        private static readonly int[] BaudRates = { 9600, 19200, 38400, 57600, 115200 };
        private readonly PortState[] ports = { new PortState(), new PortState(), new PortState(), new PortState() };

        private sealed class PortState
        {
            public bool Initialized;
            public int BaudRate;
            public readonly Queue<byte> ReceiveBuffer = new Queue<byte>();
        }

        public event Action<byte, byte> Transmitted;

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

        public void InjectReceived(byte port, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var buffer = GetInitializedPort(port).ReceiveBuffer;
            if (data.Length > ReceiveBufferCapacity - buffer.Count)
                throw new InvalidOperationException("UART receive buffer capacity exceeded");
            foreach (var value in data)
                buffer.Enqueue(value);
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

        private PortState GetPort(byte port)
        {
            if (port >= ports.Length)
                throw new InvalidOperationException("UART port is outside the supported range");
            return ports[port];
        }
    }

    public sealed class VirtualGpioBank : IVirtualMcuHal, IVirtualInputSource, IVirtualButtonHost,
        IVirtualGpioEdgeSource, IVirtualPwmHal, IVirtualAdcHal, IVirtualSpiHal, IVirtualI2cHal, IVirtualDmaHal
    {
        public const int MaxScheduledInputEvents = 4096;

        private readonly Dictionary<byte, VirtualPinMode> modes = new Dictionary<byte, VirtualPinMode>();
        private readonly Dictionary<byte, bool> values = new Dictionary<byte, bool>();
        private readonly Dictionary<byte, float> pwmValues = new Dictionary<byte, float>();
        private readonly Dictionary<byte, ushort> analogValues = new Dictionary<byte, ushort>();
        private readonly Dictionary<byte, byte> spiResponses = new Dictionary<byte, byte>();
        private readonly Dictionary<byte, byte> i2cResponses = new Dictionary<byte, byte>();
        private readonly Dictionary<byte, byte> dmaResponses = new Dictionary<byte, byte>();
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

        public event Action<byte, bool> OutputChanged;
        public event Action<VirtualGpioInputEvent> InputEdge;
        public double? NextInputTimestampMilliseconds => scheduledInputs.Count == 0
            ? (double?)null : scheduledInputs[0].Timestamp;

        public void PinMode(byte pin, VirtualPinMode mode)
        {
            ConfigurePin(pin, mode, VirtualPinPullMode.None, VirtualPinOutputType.PushPull);
        }

        public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
            VirtualPinOutputType outputType)
        {
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
            states[pin].LineLevel = value;
            if (previous != value)
                OutputChanged?.Invoke(pin, value);
            pwmValues[pin] = value ? 1f : 0f;
        }

        public bool DigitalRead(byte pin)
        {
            if (!values.TryGetValue(pin, out var value))
                throw new InvalidOperationException("GPIO pin has not been configured");
            return value;
        }

        public float GetOutputValue(byte pin)
        {
            if (!modes.ContainsKey(pin))
                throw new InvalidOperationException("GPIO pin has not been configured");
            return pwmValues.TryGetValue(pin, out var value) ? value : (values[pin] ? 1f : 0f);
        }

        public void SetAnalogInput(byte channel, ushort value)
        {
            if (value > 4095)
                throw new ArgumentOutOfRangeException(nameof(value), "ADC sample must be between 0 and 4095");
            analogValues[channel] = value;
        }

        public ushort AnalogRead(byte channel)
        {
            return analogValues.TryGetValue(channel, out var value) ? value : (ushort)0;
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

        public void PwmWrite(byte pin, byte dutyCycle)
        {
            if (!modes.TryGetValue(pin, out var mode))
                throw new InvalidOperationException("GPIO pin has not been configured");
            if (mode != VirtualPinMode.Output)
                throw new InvalidOperationException("PWM write requires output mode");

            var value = dutyCycle / 255f;
            pwmValues[pin] = value;
            var digitalValue = dutyCycle >= 128;
            values[pin] = digitalValue;
            states[pin].LatchLevel = digitalValue;
            states[pin].LineLevel = digitalValue;
            OutputChanged?.Invoke(pin, digitalValue);
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
                var previous = values[input.Pin];
                values[input.Pin] = input.Level;
                states[input.Pin].LineLevel = input.Level;
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

    public sealed class VirtualMachine
    {
        private const int MaxInstructionsPerAdvance = 100000;
        private const int MaxCallDepth = 256;
        public const int MaxValueStackDepth = 1024;

        private readonly RsfwPackage package;
        private readonly IVirtualMcuHal hal;
        private readonly IVirtualInputSource inputSource;
        private readonly IVirtualInterruptController interruptController;
        private readonly IVirtualUartHal uart;
        private readonly Stack<CallFrame> callStack = new Stack<CallFrame>();
        private readonly Stack<IrqReturnState> irqReturnStack = new Stack<IrqReturnState>();
        private readonly Stack<int> valueStack = new Stack<int>();
        private readonly int[] slots = new int[256];
        private readonly int[] globals = new int[256];
        private uint programCounter;
        private double wakeTimeMilliseconds;

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

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal)
            : this(package, hal, null, null)
        {
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController)
            : this(package, hal, interruptController, null)
        {
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController, IVirtualUartHal uart)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.hal = hal ?? throw new ArgumentNullException(nameof(hal));
            inputSource = hal as IVirtualInputSource;
            this.interruptController = interruptController ??
                (hal is IVirtualGpioEdgeSource edgeSource ? new VirtualInterruptController(edgeSource) : null);
            this.uart = uart;
            programCounter = package.Bytecode.EntryPoint;
            State = VirtualExecutionState.Ready;
        }

        public VirtualExecutionState State { get; private set; }
        public uint ProgramCounter => programCounter;
        public double SimulatedTimeMilliseconds { get; private set; }
        public string FaultMessage { get; private set; }
        public int StackDepth => valueStack.Count;
        public int StackTop => valueStack.Count == 0 ? throw new InvalidOperationException("VM value stack is empty") : valueStack.Peek();
        public double WakeTimeMilliseconds => wakeTimeMilliseconds;
        public int InterruptDepth => irqReturnStack.Count;
        public event Action<VirtualInterruptRequest> InterruptDispatched;

        public void Reset()
        {
            callStack.Clear();
            irqReturnStack.Clear();
            valueStack.Clear();
            Array.Clear(slots, 0, slots.Length);
            Array.Clear(globals, 0, globals.Length);
            programCounter = package.Bytecode.EntryPoint;
            wakeTimeMilliseconds = 0.0;
            SimulatedTimeMilliseconds = 0.0;
            FaultMessage = null;
            State = VirtualExecutionState.Ready;
        }

        public bool Step()
        {
            if (State == VirtualExecutionState.Halted || State == VirtualExecutionState.Faulted)
                return false;

            try
            {
                inputSource?.AdvanceInputTo(SimulatedTimeMilliseconds);
                if (State == VirtualExecutionState.Ready)
                    State = VirtualExecutionState.Running;
                if (State == VirtualExecutionState.Yielded)
                    State = VirtualExecutionState.Running;

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
                inputSource?.AdvanceInputTo(SimulatedTimeMilliseconds);
                return;
            }

            if (State == VirtualExecutionState.Yielded)
                State = VirtualExecutionState.Running;

            var targetTime = SimulatedTimeMilliseconds + deltaMilliseconds;
            if (State == VirtualExecutionState.Ready)
                State = VirtualExecutionState.Running;

            try
            {
                var executed = 0;
                while (State != VirtualExecutionState.Halted && State != VirtualExecutionState.Faulted)
                {
                    inputSource?.AdvanceInputTo(SimulatedTimeMilliseconds);
                    if (TryDispatchInterrupt())
                        continue;
                    if (State == VirtualExecutionState.Waiting)
                    {
                        var nextInput = inputSource?.NextInputTimestampMilliseconds;
                        if (nextInput.HasValue && nextInput.Value <= wakeTimeMilliseconds && nextInput.Value <= targetTime)
                        {
                            SimulatedTimeMilliseconds = nextInput.Value;
                            inputSource.AdvanceInputTo(SimulatedTimeMilliseconds);
                            continue;
                        }
                        if (wakeTimeMilliseconds > targetTime)
                        {
                            SimulatedTimeMilliseconds = targetTime;
                            inputSource?.AdvanceInputTo(targetTime);
                            return;
                        }
                        SimulatedTimeMilliseconds = wakeTimeMilliseconds;
                        inputSource?.AdvanceInputTo(SimulatedTimeMilliseconds);
                        State = VirtualExecutionState.Running;
                    }
                    if (State != VirtualExecutionState.Running)
                        break;
                    if (++executed > MaxInstructionsPerAdvance)
                        throw new VirtualMachineFaultException("Instruction budget exceeded; possible tight loop");
                    ExecuteInstruction(package.Bytecode.Instructions[(int)programCounter]);
                }
                SimulatedTimeMilliseconds = targetTime;
                inputSource?.AdvanceInputTo(targetTime);
            }
            catch (Exception exception)
            {
                State = VirtualExecutionState.Faulted;
                FaultMessage = exception.Message;
                if (exception is VirtualMachineFaultException)
                    throw;
                throw new VirtualMachineFaultException("Virtual MCU execution failed at instruction " + programCounter, exception);
            }
        }

        private void ExecuteInstruction(RsfwInstruction instruction)
        {
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
                default:
                    throw new VirtualMachineFaultException("Unsupported opcode " + instruction.Opcode);
            }

            programCounter = next;
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
            programCounter = request.TargetInstruction;
            State = VirtualExecutionState.Running;
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
            var service = (RsmaHalService)ReadU16(operands, 0);
            var argument = operands[2];
            var value = operands[3];
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
                    break;
                case RsmaHalService.DigitalWrite:
                    hal.DigitalWrite(argument, value != 0);
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
                    break;
                }
                case RsmaHalService.AnalogRead:
                    if (!(hal is IVirtualAdcHal adcHal))
                        throw new VirtualMachineFaultException("Firmware requires ADC support");
                    PushValue(adcHal.AnalogRead(argument));
                    break;
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
