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

    public sealed class VirtualGpioBank : IVirtualMcuHal, IVirtualInputSource, IVirtualButtonHost,
        IVirtualGpioEdgeSource, IVirtualPwmHal
    {
        private readonly Dictionary<byte, VirtualPinMode> modes = new Dictionary<byte, VirtualPinMode>();
        private readonly Dictionary<byte, bool> values = new Dictionary<byte, bool>();
        private readonly Dictionary<byte, float> pwmValues = new Dictionary<byte, float>();
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

        private readonly RsfwPackage package;
        private readonly IVirtualMcuHal hal;
        private readonly IVirtualInputSource inputSource;
        private readonly IVirtualInterruptController interruptController;
        private readonly Stack<uint> callStack = new Stack<uint>();
        private readonly Stack<IrqReturnState> irqReturnStack = new Stack<IrqReturnState>();
        private readonly Stack<int> valueStack = new Stack<int>();
        private readonly int[] slots = new int[256];
        private uint programCounter;
        private double wakeTimeMilliseconds;

        private sealed class IrqReturnState
        {
            public uint ProgramCounter;
            public bool WasWaiting;
            public double WakeTimeMilliseconds;
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal)
            : this(package, hal, null)
        {
        }

        public VirtualMachine(RsfwPackage package, IVirtualMcuHal hal,
            IVirtualInterruptController interruptController)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            this.hal = hal ?? throw new ArgumentNullException(nameof(hal));
            inputSource = hal as IVirtualInputSource;
            this.interruptController = interruptController ??
                (hal is IVirtualGpioEdgeSource edgeSource ? new VirtualInterruptController(edgeSource) : null);
            programCounter = package.Bytecode.EntryPoint;
            State = VirtualExecutionState.Ready;
        }

        public VirtualExecutionState State { get; private set; }
        public uint ProgramCounter => programCounter;
        public double SimulatedTimeMilliseconds { get; private set; }
        public string FaultMessage { get; private set; }
        public int StackDepth => valueStack.Count;
        public double WakeTimeMilliseconds => wakeTimeMilliseconds;
        public int InterruptDepth => irqReturnStack.Count;
        public event Action<VirtualInterruptRequest> InterruptDispatched;

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
                    valueStack.Push(unchecked((int)ReadU32(instruction.Operands, 0)));
                    break;
                case RsfwOpcode.Load:
                    valueStack.Push(slots[ReadU16(instruction.Operands, 0)]);
                    break;
                case RsfwOpcode.Store:
                    slots[ReadU16(instruction.Operands, 0)] = PopValue();
                    break;
                case RsfwOpcode.AddI32:
                    valueStack.Push(unchecked(PopValue() + PopValue()));
                    break;
                case RsfwOpcode.SubI32:
                {
                    var right = PopValue();
                    var left = PopValue();
                    valueStack.Push(unchecked(left - right));
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
                    callStack.Push(next);
                    next = ReadU32(instruction.Operands, 0);
                    break;
                case RsfwOpcode.Return:
                    if (callStack.Count == 0)
                    {
                        State = VirtualExecutionState.Halted;
                        return;
                    }
                    next = callStack.Pop();
                    break;
                case RsfwOpcode.ReadTime:
                    valueStack.Push(unchecked((int)SimulatedTimeMilliseconds));
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
                    valueStack.Push(hal.DigitalRead(argument) ? 1 : 0);
                    break;
                case RsmaHalService.DelayMs:
                    wakeTimeMilliseconds = SimulatedTimeMilliseconds + value;
                    State = VirtualExecutionState.Waiting;
                    break;
                case RsmaHalService.Millis:
                    valueStack.Push(unchecked((int)SimulatedTimeMilliseconds));
                    break;
                default:
                    throw new VirtualMachineFaultException("Unsupported HAL service " + service);
            }
        }

        private int PopValue()
        {
            if (valueStack.Count == 0)
                throw new VirtualMachineFaultException("VM value stack underflow");
            return valueStack.Pop();
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
