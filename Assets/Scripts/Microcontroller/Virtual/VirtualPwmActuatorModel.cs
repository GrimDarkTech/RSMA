using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualActuatorKind
    {
        Led,
        Relay,
        Servo,
        DcMotor
    }

    public sealed class VirtualActuatorOutput
    {
        public double TimestampMilliseconds { get; internal set; }
        public float TargetValue { get; internal set; }
        public float EffectiveValue { get; internal set; }
        public float PhysicalValue { get; internal set; }
        public int Direction { get; internal set; }
        public bool Enabled { get; internal set; }
        public bool Brake { get; internal set; }
        public bool Fault { get; internal set; }
    }

    /// <summary>
    /// Deterministic actuator stage between a PWM command and physical output.
    /// The model owns saturation, slew limiting and safe-state behavior; Unity
    /// components only provide the command and consume the resulting output.
    /// </summary>
    public sealed class VirtualPwmActuatorModel
    {
        private const int OutputTraceCapacity = 256;
        private readonly List<VirtualActuatorOutput> outputTrace = new List<VirtualActuatorOutput>();
        public VirtualActuatorKind Kind { get; }
        public float PhysicalMinimum { get; }
        public float PhysicalMaximum { get; }
        public float SlewRatePerSecond { get; }
        public float TargetValue { get; private set; }
        public float EffectiveValue { get; private set; }
        public float PhysicalValue { get; private set; }
        public int Direction { get; private set; }
        public bool Enabled { get; private set; }
        public bool Brake { get; private set; }
        public bool Fault { get; private set; }
        public double LastTimestampMilliseconds { get; private set; }
        public IList<VirtualActuatorOutput> OutputTrace => outputTrace.AsReadOnly();

        public VirtualPwmActuatorModel(VirtualActuatorKind kind,
            float physicalMinimum = 0f,
            float physicalMaximum = 1f,
            float slewRatePerSecond = 0f)
        {
            if (float.IsNaN(physicalMinimum) || float.IsInfinity(physicalMinimum) ||
                float.IsNaN(physicalMaximum) || float.IsInfinity(physicalMaximum) ||
                physicalMaximum <= physicalMinimum)
                throw new ArgumentOutOfRangeException(nameof(physicalMaximum));
            if (float.IsNaN(slewRatePerSecond) || float.IsInfinity(slewRatePerSecond) || slewRatePerSecond < 0f)
                throw new ArgumentOutOfRangeException(nameof(slewRatePerSecond));

            Kind = kind;
            PhysicalMinimum = physicalMinimum;
            PhysicalMaximum = physicalMaximum;
            SlewRatePerSecond = slewRatePerSecond;
            Direction = 1;
        }

        public void SetCommand(float duty, bool digitalLevel, bool enabled = true,
            int direction = 1, bool brake = false, bool fault = false)
        {
            if (float.IsNaN(duty) || float.IsInfinity(duty))
                throw new ArgumentOutOfRangeException(nameof(duty));
            TargetValue = Kind == VirtualActuatorKind.Relay
                ? (digitalLevel ? 1f : 0f)
                : Clamp01(duty);
            Enabled = enabled;
            Direction = direction < 0 ? -1 : 1;
            Brake = brake;
            Fault = fault;
            if (!Enabled || Brake || Fault)
                TargetValue = 0f;
        }

        public VirtualActuatorOutput AdvanceTo(double timestampMilliseconds)
        {
            ValidateTimestamp(timestampMilliseconds);
            if (timestampMilliseconds < LastTimestampMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds),
                    "Actuator time cannot move backwards");

            var deltaSeconds = (float)((timestampMilliseconds - LastTimestampMilliseconds) / 1000.0);
            if (SlewRatePerSecond <= 0f)
                EffectiveValue = TargetValue;
            else
            {
                var maximumChange = SlewRatePerSecond * deltaSeconds;
                var difference = TargetValue - EffectiveValue;
                if (Math.Abs(difference) <= maximumChange)
                    EffectiveValue = TargetValue;
                else
                    EffectiveValue += Math.Sign(difference) * maximumChange;
            }
            LastTimestampMilliseconds = timestampMilliseconds;
            if (Kind == VirtualActuatorKind.DcMotor)
                PhysicalValue = EffectiveValue * Direction * Math.Max(Math.Abs(PhysicalMinimum), Math.Abs(PhysicalMaximum));
            else
                PhysicalValue = PhysicalMinimum + EffectiveValue * (PhysicalMaximum - PhysicalMinimum);
            return RecordSnapshot();
        }

        public VirtualActuatorOutput SafeState()
        {
            TargetValue = 0f;
            EffectiveValue = 0f;
            PhysicalValue = Kind == VirtualActuatorKind.DcMotor ? 0f : PhysicalMinimum;
            Enabled = false;
            Brake = false;
            Direction = 1;
            return RecordSnapshot();
        }

        private VirtualActuatorOutput RecordSnapshot()
        {
            var output = new VirtualActuatorOutput
            {
                TimestampMilliseconds = LastTimestampMilliseconds,
                TargetValue = TargetValue,
                EffectiveValue = EffectiveValue,
                PhysicalValue = PhysicalValue,
                Direction = Direction,
                Enabled = Enabled,
                Brake = Brake,
                Fault = Fault
            };
            if (outputTrace.Count == OutputTraceCapacity)
                outputTrace.RemoveAt(0);
            outputTrace.Add(output);
            return output;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static void ValidateTimestamp(double timestampMilliseconds)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
                timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
        }
    }
}