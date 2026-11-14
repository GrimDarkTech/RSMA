using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualTraceEventKind
    {
        TimeAdvanced,
        AnalogSample,
        DigitalOutput,
        PwmOutput,
        HalCall,
        StateTransition,
        Interrupt,
        UartTransmit,
        RejectedMapping,
        Fault
    }

    public sealed class VirtualTraceEvent
    {
        public VirtualTraceEventKind Kind { get; }
        public ulong Sequence { get; }
        public double TimestampMilliseconds { get; }
        public int Channel { get; }
        public int Value { get; }
        public int AuxiliaryValue { get; }
        public string Message { get; }

        internal VirtualTraceEvent(VirtualTraceEventKind kind, ulong sequence,
            double timestampMilliseconds, int channel, int value, int auxiliaryValue, string message)
        {
            Kind = kind;
            Sequence = sequence;
            TimestampMilliseconds = timestampMilliseconds;
            Channel = channel;
            Value = value;
            AuxiliaryValue = auxiliaryValue;
            Message = message;
        }
    }

    /// <summary>
    /// Bounded deterministic trace buffer shared by standalone and Unity VM runs.
    /// The sequence remains monotonic even when the oldest events are dropped.
    /// </summary>
    public sealed class VirtualTraceBuffer
    {
        public const int DefaultCapacity = 4096;

        private readonly int capacity;
        private readonly List<VirtualTraceEvent> events = new List<VirtualTraceEvent>();
        private ulong nextSequence;

        public VirtualTraceBuffer(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public int Capacity => capacity;
        public int Count => events.Count;
        public ulong DroppedEventCount { get; private set; }
        public VirtualTraceEvent this[int index] => events[index];

        public VirtualTraceEvent Record(VirtualTraceEventKind kind, double timestampMilliseconds,
            int channel = -1, int value = 0, int auxiliaryValue = 0, string message = null)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
                timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));

            var traceEvent = new VirtualTraceEvent(kind, nextSequence++, timestampMilliseconds,
                channel, value, auxiliaryValue, message);
            if (events.Count == capacity)
            {
                events.RemoveAt(0);
                DroppedEventCount++;
            }
            events.Add(traceEvent);
            return traceEvent;
        }

        public List<VirtualTraceEvent> Snapshot()
        {
            return new List<VirtualTraceEvent>(events);
        }

        public void Clear()
        {
            events.Clear();
            DroppedEventCount = 0;
        }
    }
}