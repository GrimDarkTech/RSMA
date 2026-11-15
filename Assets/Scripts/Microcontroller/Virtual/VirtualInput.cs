using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualGpioEdge : byte
    {
        Rising = 1,
        Falling = 2
    }

    public sealed class VirtualGpioInputEvent
    {
        public byte VirtualPin { get; }
        public bool PreviousLevel { get; }
        public bool Level { get; }
        public double TimestampMilliseconds { get; }
        public ulong Sequence { get; }
        public VirtualGpioEdge Edge { get; }

        public VirtualGpioInputEvent(byte virtualPin, bool previousLevel, bool level,
            double timestampMilliseconds, ulong sequence, VirtualGpioEdge edge)
        {
            VirtualPin = virtualPin;
            PreviousLevel = previousLevel;
            Level = level;
            TimestampMilliseconds = timestampMilliseconds;
            Sequence = sequence;
            Edge = edge;
        }
    }

    public interface IVirtualInputSource
    {
        double? NextInputTimestampMilliseconds { get; }
        void AdvanceInputTo(double timestampMilliseconds);
    }

    public interface IVirtualUartInputSource : IVirtualInputSource
    {
        event Action<byte, byte> Received;
        event Action<byte, HitlFrame> PacketReceived;
        void ScheduleReceived(byte port, double timestampMilliseconds, byte[] data);
    }

    public interface IVirtualUartOutputSource
    {
        event Action<byte, byte> Transmitted;
    }

    public sealed class VirtualUartDiagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public byte Port { get; }
        public double TimestampMilliseconds { get; }

        public VirtualUartDiagnostic(string code, string message, byte port,
            double timestampMilliseconds)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Port = port;
            TimestampMilliseconds = timestampMilliseconds;
        }
    }

    public interface IVirtualUartDiagnostics
    {
        event Action<VirtualUartDiagnostic> DiagnosticRaised;
        IList<VirtualUartDiagnostic> UartDiagnostics { get; }
        ulong DroppedReceiveByteCount { get; }
        ulong DroppedTransmitByteCount { get; }
    }

    public interface IVirtualUartOutputScheduler
    {
        double? NextOutputTimestampMilliseconds { get; }
        void AdvanceOutputTo(double timestampMilliseconds);
        int GetTransmitQueueCount(byte port);
    }

    public interface IVirtualUartTransportInput
    {
        void InjectReceived(byte port, byte[] data);
        void InjectTransportBytes(byte port, byte[] data);
    }

    public interface IVirtualGpioEdgeSource
    {
        event Action<VirtualGpioInputEvent> InputEdge;
    }

    public interface IVirtualButtonHost
    {
        VirtualPinMode GetMode(byte pin);
        void ScheduleInputLevel(byte pin, double timestampMilliseconds, bool level);
    }

    public interface IVirtualInterruptController
    {
        bool HasPendingInterrupt { get; }
        bool TryDequeue(out VirtualInterruptRequest request);
        void Attach(byte pin, byte edgeMask, uint targetInstruction);
        void Detach(byte pin);
        void Enable();
        void Disable();
    }

    public sealed class VirtualInterruptRequest
    {
        public byte VirtualPin { get; internal set; }
        public VirtualGpioEdge Edge { get; internal set; }
        public double TimestampMilliseconds { get; internal set; }
        public ulong Sequence { get; internal set; }
        public uint TargetInstruction { get; internal set; }
        public int Priority { get; internal set; }
    }

    public sealed class VirtualInterruptController : IVirtualInterruptController, IVirtualInterruptDiagnostics
    {
        public const int MaxPendingInterrupts = 4096;

        private sealed class Binding
        {
            public byte EdgeMask;
            public uint TargetInstruction;
            public int Priority;
            public double LastAcceptedTimestampMilliseconds = double.NegativeInfinity;
        }

        private readonly Dictionary<byte, Binding> bindings = new Dictionary<byte, Binding>();
        private readonly Queue<VirtualInterruptRequest> pending = new Queue<VirtualInterruptRequest>();
        private readonly VirtualSchedulingPolicy policy;
        private readonly List<VirtualSchedulingDiagnostic> diagnostics =
            new List<VirtualSchedulingDiagnostic>();

        public VirtualInterruptController()
        {
            policy = new VirtualSchedulingPolicy();
            ValidatePolicy();
        }

        public VirtualInterruptController(IVirtualGpioEdgeSource gpio,
            VirtualSchedulingPolicy schedulingPolicy = null)
        {
            policy = schedulingPolicy?.Clone() ?? new VirtualSchedulingPolicy();
            ValidatePolicy();
            Connect(gpio);
        }

        public VirtualInterruptController(VirtualSchedulingPolicy schedulingPolicy)
        {
            policy = schedulingPolicy?.Clone() ?? new VirtualSchedulingPolicy();
            ValidatePolicy();
        }

        public bool Enabled { get; private set; }
        public bool HasPendingInterrupt => pending.Count != 0;
        public VirtualSchedulingPolicy Policy => policy.Clone();
        public IList<VirtualSchedulingDiagnostic> SchedulingDiagnostics => diagnostics.AsReadOnly();
        public ulong DroppedInterruptCount { get; private set; }
        public event Action<VirtualSchedulingDiagnostic> DiagnosticRaised;

        public void Attach(byte pin, byte edgeMask, uint targetInstruction)
        {
            Attach(pin, edgeMask, targetInstruction, policy.GetInterruptPriority(pin));
        }

        public void Attach(byte pin, byte edgeMask, uint targetInstruction, int priority)
        {
            if ((edgeMask & 0x03) == 0 || (edgeMask & 0xFC) != 0)
                throw new ArgumentOutOfRangeException(nameof(edgeMask));
            bindings[pin] = new Binding
            {
                EdgeMask = edgeMask,
                TargetInstruction = targetInstruction,
                Priority = priority
            };
        }

        public void Detach(byte pin)
        {
            bindings.Remove(pin);
        }

        public void Enable() { Enabled = true; }
        public void Disable() { Enabled = false; }

        public bool TryDequeue(out VirtualInterruptRequest request)
        {
            if (!Enabled || pending.Count == 0)
            {
                request = null;
                return false;
            }
            request = pending.Dequeue();
            return true;
        }

        public void Connect(IVirtualGpioEdgeSource gpio)
        {
            if (gpio == null) throw new ArgumentNullException(nameof(gpio));
            gpio.InputEdge += OnInputEdge;
        }

        private void OnInputEdge(VirtualGpioInputEvent input)
        {
            if (!bindings.TryGetValue(input.VirtualPin, out var binding))
                return;
            var edgeBit = (byte)input.Edge;
            if ((binding.EdgeMask & edgeBit) == 0)
                return;
            if (policy.InterruptDebounceMilliseconds > 0 &&
                input.TimestampMilliseconds - binding.LastAcceptedTimestampMilliseconds <
                policy.InterruptDebounceMilliseconds)
            {
                RaiseDiagnostic("IRQ_DEBOUNCED", "GPIO edge was ignored by interrupt debounce policy",
                    input.VirtualPin, input.TimestampMilliseconds);
                return;
            }
            if (!Enabled && policy.InterruptMaskPolicy == VirtualInterruptMaskPolicy.DropWhileMasked)
            {
                Drop("IRQ_MASKED", "GPIO edge was dropped while IRQs were masked", input);
                return;
            }
            if (pending.Count >= policy.InterruptQueueCapacity)
            {
                Drop("IRQ_QUEUE_OVERFLOW", "Virtual interrupt queue capacity exceeded", input);
                return;
            }
            binding.LastAcceptedTimestampMilliseconds = input.TimestampMilliseconds;
            pending.Enqueue(new VirtualInterruptRequest
            {
                VirtualPin = input.VirtualPin,
                Edge = input.Edge,
                TimestampMilliseconds = input.TimestampMilliseconds,
                Sequence = input.Sequence,
                TargetInstruction = binding.TargetInstruction,
                Priority = binding.Priority
            });
            ReorderPendingByPriority();
        }

        private void ReorderPendingByPriority()
        {
            if (pending.Count < 2)
                return;
            var ordered = new List<VirtualInterruptRequest>(pending);
            ordered.Sort((left, right) =>
            {
                var timestamp = left.TimestampMilliseconds.CompareTo(right.TimestampMilliseconds);
                if (timestamp != 0)
                    return timestamp;
                var priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : left.Sequence.CompareTo(right.Sequence);
            });
            pending.Clear();
            foreach (var request in ordered)
                pending.Enqueue(request);
        }

        private void Drop(string code, string message, VirtualGpioInputEvent input)
        {
            DroppedInterruptCount++;
            RaiseDiagnostic(code, message, input.VirtualPin, input.TimestampMilliseconds);
        }

        private void RaiseDiagnostic(string code, string message, byte pin, double timestamp)
        {
            var diagnostic = new VirtualSchedulingDiagnostic(code, message, pin, timestamp);
            diagnostics.Add(diagnostic);
            DiagnosticRaised?.Invoke(diagnostic);
        }

        private void ValidatePolicy()
        {
            if (policy.InterruptQueueCapacity < 1 || policy.InterruptQueueCapacity > MaxPendingInterrupts)
                throw new ArgumentOutOfRangeException(nameof(policy.InterruptQueueCapacity));
            if (policy.InterruptDebounceMilliseconds < 0 ||
                double.IsNaN(policy.InterruptDebounceMilliseconds) ||
                double.IsInfinity(policy.InterruptDebounceMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(policy.InterruptDebounceMilliseconds));
        }
    }

    public sealed class VirtualButton
    {
        private readonly IVirtualButtonHost input;
        private readonly byte pin;

        public VirtualButton(VirtualGpioBank gpio, byte pin)
            : this((IVirtualButtonHost)gpio, pin)
        {
        }

        public VirtualButton(IVirtualButtonHost input, byte pin)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.pin = pin;
            if (input.GetMode(pin) != VirtualPinMode.Input)
                throw new InvalidOperationException("Button requires an input GPIO pin");
        }

        public void PressAt(double timestampMilliseconds)
        {
            input.ScheduleInputLevel(pin, timestampMilliseconds, false);
        }

        public void ReleaseAt(double timestampMilliseconds)
        {
            input.ScheduleInputLevel(pin, timestampMilliseconds, true);
        }
    }
}