using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualHostClockPolicy
    {
        ScaledDeltaTime,
        UnscaledDeltaTime,
        Paused
    }

    public enum VirtualInterruptMaskPolicy
    {
        QueueWhileMasked,
        DropWhileMasked
    }

    public enum VirtualInterruptReentrancyPolicy
    {
        NonReentrant,
        RejectNested
    }

    public sealed class VirtualSchedulingPolicy
    {
        public const int DefaultInterruptQueueCapacity = 256;
        public const double DefaultMaximumHostDeltaMilliseconds = 1000.0;

        public VirtualHostClockPolicy HostClockPolicy { get; set; } = VirtualHostClockPolicy.ScaledDeltaTime;
        public VirtualInterruptMaskPolicy InterruptMaskPolicy { get; set; } = VirtualInterruptMaskPolicy.QueueWhileMasked;
        public VirtualInterruptReentrancyPolicy InterruptReentrancyPolicy { get; set; } = VirtualInterruptReentrancyPolicy.NonReentrant;
        public int InterruptQueueCapacity { get; set; } = DefaultInterruptQueueCapacity;
        public double InterruptDebounceMilliseconds { get; set; }
        public double MaximumHostDeltaMilliseconds { get; set; } = DefaultMaximumHostDeltaMilliseconds;
        public IDictionary<byte, int> InterruptPriorities { get; } =
            new Dictionary<byte, int>();

        public void SetInterruptPriority(byte pin, int priority)
        {
            InterruptPriorities[pin] = priority;
        }

        public int GetInterruptPriority(byte pin)
        {
            return InterruptPriorities.TryGetValue(pin, out var priority) ? priority : 0;
        }

        public VirtualSchedulingPolicy Clone()
        {
            var clone = new VirtualSchedulingPolicy
            {
                HostClockPolicy = HostClockPolicy,
                InterruptMaskPolicy = InterruptMaskPolicy,
                InterruptReentrancyPolicy = InterruptReentrancyPolicy,
                InterruptQueueCapacity = InterruptQueueCapacity,
                InterruptDebounceMilliseconds = InterruptDebounceMilliseconds,
                MaximumHostDeltaMilliseconds = MaximumHostDeltaMilliseconds
            };
            foreach (var priority in InterruptPriorities)
                clone.InterruptPriorities.Add(priority.Key, priority.Value);
            return clone;
        }

        public double ComputeHostDeltaMilliseconds(double scaledDeltaMilliseconds,
            double unscaledDeltaMilliseconds, bool paused, IList<string> diagnostics = null)
        {
            if (double.IsNaN(scaledDeltaMilliseconds) || double.IsInfinity(scaledDeltaMilliseconds) ||
                scaledDeltaMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaMilliseconds));
            if (double.IsNaN(unscaledDeltaMilliseconds) || double.IsInfinity(unscaledDeltaMilliseconds) ||
                unscaledDeltaMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaMilliseconds));
            if (MaximumHostDeltaMilliseconds <= 0 || double.IsNaN(MaximumHostDeltaMilliseconds) ||
                double.IsInfinity(MaximumHostDeltaMilliseconds))
                throw new InvalidOperationException("Maximum host delta must be finite and positive");

            var delta = paused || HostClockPolicy == VirtualHostClockPolicy.Paused
                ? 0.0
                : HostClockPolicy == VirtualHostClockPolicy.UnscaledDeltaTime
                    ? unscaledDeltaMilliseconds : scaledDeltaMilliseconds;
            if (delta > MaximumHostDeltaMilliseconds)
            {
                diagnostics?.Add("Host clock delta was clamped from " + delta + " ms to " +
                    MaximumHostDeltaMilliseconds + " ms");
                delta = MaximumHostDeltaMilliseconds;
            }
            return delta;
        }
    }

    public sealed class VirtualSchedulingDiagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public byte Pin { get; }
        public double TimestampMilliseconds { get; }

        public VirtualSchedulingDiagnostic(string code, string message, byte pin = 0,
            double timestampMilliseconds = 0)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Pin = pin;
            TimestampMilliseconds = timestampMilliseconds;
        }
    }

    public interface IVirtualInterruptDiagnostics
    {
        event Action<VirtualSchedulingDiagnostic> DiagnosticRaised;
        IList<VirtualSchedulingDiagnostic> SchedulingDiagnostics { get; }
        ulong DroppedInterruptCount { get; }
    }

    public sealed class VirtualPeriodicScheduler
    {
        private sealed class Entry
        {
            public int Id;
            public double PeriodMilliseconds;
            public double NextTimestampMilliseconds;
            public Action<double> Callback;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private int nextId = 1;

        public int MaximumCallbacksPerAdvance { get; set; } = 4096;
        public int CallbackCount { get; private set; }

        public void Reset()
        {
            entries.Clear();
            CallbackCount = 0;
        }

        public int SchedulePeriodic(double firstTimestampMilliseconds, double periodMilliseconds,
            Action<double> callback)
        {
            ValidateTimestamp(firstTimestampMilliseconds, nameof(firstTimestampMilliseconds));
            if (double.IsNaN(periodMilliseconds) || double.IsInfinity(periodMilliseconds) || periodMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(periodMilliseconds));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            var id = nextId++;
            entries.Add(new Entry
            {
                Id = id,
                NextTimestampMilliseconds = firstTimestampMilliseconds,
                PeriodMilliseconds = periodMilliseconds,
                Callback = callback
            });
            return id;
        }

        public bool Cancel(int id)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Id != id)
                    continue;
                entries.RemoveAt(index);
                return true;
            }
            return false;
        }

        public int AdvanceTo(double timestampMilliseconds)
        {
            ValidateTimestamp(timestampMilliseconds, nameof(timestampMilliseconds));
            if (MaximumCallbacksPerAdvance < 1)
                throw new InvalidOperationException("MaximumCallbacksPerAdvance must be positive");
            var callbacks = 0;
            while (callbacks < MaximumCallbacksPerAdvance)
            {
                Entry next = null;
                for (var index = 0; index < entries.Count; index++)
                {
                    var candidate = entries[index];
                    if (candidate.NextTimestampMilliseconds > timestampMilliseconds || next != null &&
                        (candidate.NextTimestampMilliseconds > next.NextTimestampMilliseconds ||
                            candidate.NextTimestampMilliseconds == next.NextTimestampMilliseconds &&
                            candidate.Id > next.Id))
                        continue;
                    next = candidate;
                }
                if (next == null)
                    break;
                var callbackTimestamp = next.NextTimestampMilliseconds;
                next.NextTimestampMilliseconds += next.PeriodMilliseconds;
                next.Callback(callbackTimestamp);
                callbacks++;
                CallbackCount++;
            }
            if (callbacks == MaximumCallbacksPerAdvance && entries.Exists(entry =>
                    entry.NextTimestampMilliseconds <= timestampMilliseconds))
                throw new InvalidOperationException("Periodic scheduler callback budget exceeded");
            return callbacks;
        }

        private static void ValidateTimestamp(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}