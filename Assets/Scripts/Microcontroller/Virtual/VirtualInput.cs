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
    }

    public sealed class VirtualInterruptController : IVirtualInterruptController
    {
        public const int MaxPendingInterrupts = 4096;

        private sealed class Binding
        {
            public byte EdgeMask;
            public uint TargetInstruction;
        }

        private readonly Dictionary<byte, Binding> bindings = new Dictionary<byte, Binding>();
        private readonly Queue<VirtualInterruptRequest> pending = new Queue<VirtualInterruptRequest>();

        public VirtualInterruptController() { }

        public VirtualInterruptController(IVirtualGpioEdgeSource gpio)
        {
            Connect(gpio);
        }

        public bool Enabled { get; private set; }
        public bool HasPendingInterrupt => pending.Count != 0;

        public void Attach(byte pin, byte edgeMask, uint targetInstruction)
        {
            if ((edgeMask & 0x03) == 0 || (edgeMask & 0xFC) != 0)
                throw new ArgumentOutOfRangeException(nameof(edgeMask));
            bindings[pin] = new Binding { EdgeMask = edgeMask, TargetInstruction = targetInstruction };
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
            if (pending.Count >= MaxPendingInterrupts)
                throw new InvalidOperationException("Virtual interrupt queue capacity exceeded");
            pending.Enqueue(new VirtualInterruptRequest
            {
                VirtualPin = input.VirtualPin,
                Edge = input.Edge,
                TimestampMilliseconds = input.TimestampMilliseconds,
                Sequence = input.Sequence,
                TargetInstruction = binding.TargetInstruction
            });
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