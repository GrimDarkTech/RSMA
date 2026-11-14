using System;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualAnalogWaveform
    {
        Constant,
        Ramp,
        Sine,
        Curve
    }

    /// <summary>
    /// Deterministic, Unity-independent analog sensor model.
    /// The waveform and noise operate in normalized units; calibration maps the
    /// result to the raw 12-bit ADC range consumed by firmware.
    /// </summary>
    public sealed class VirtualAnalogSensorModel
    {
        public const ushort AdcMaximum = 4095;

        private readonly Func<double, double> curve;

        public VirtualAnalogWaveform Waveform { get; }
        public double ConstantValue { get; }
        public double RampStart { get; }
        public double RampEnd { get; }
        public double PeriodMilliseconds { get; }
        public double SamplePeriodMilliseconds { get; }
        public double NoiseAmplitude { get; }
        public int NoiseSeed { get; }
        public double CalibrationOffset { get; }
        public double CalibrationGain { get; }
        public double CalibrationMinimum { get; }
        public double CalibrationMaximum { get; }
        public double PhysicalMinimum { get; }
        public double PhysicalMaximum { get; }

        public VirtualAnalogSensorModel(
            VirtualAnalogWaveform waveform,
            double constantValue = 0.0,
            double rampStart = 0.0,
            double rampEnd = 1.0,
            double periodMilliseconds = 1000.0,
            double samplePeriodMilliseconds = 0.0,
            double noiseAmplitude = 0.0,
            int noiseSeed = 1,
            double calibrationOffset = 0.0,
            double calibrationGain = 1.0,
            double calibrationMinimum = 0.0,
            double calibrationMaximum = 1.0,
            double physicalMinimum = 0.0,
            double physicalMaximum = 1.0,
            Func<double, double> curve = null)
        {
            if (double.IsNaN(periodMilliseconds) || double.IsInfinity(periodMilliseconds) || periodMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(periodMilliseconds));
            if (double.IsNaN(samplePeriodMilliseconds) || double.IsInfinity(samplePeriodMilliseconds) || samplePeriodMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(samplePeriodMilliseconds));
            if (double.IsNaN(noiseAmplitude) || double.IsInfinity(noiseAmplitude) || noiseAmplitude < 0)
                throw new ArgumentOutOfRangeException(nameof(noiseAmplitude));
            if (double.IsNaN(calibrationGain) || double.IsInfinity(calibrationGain))
                throw new ArgumentOutOfRangeException(nameof(calibrationGain));
            if (double.IsNaN(calibrationMinimum) || double.IsInfinity(calibrationMinimum) ||
                double.IsNaN(calibrationMaximum) || double.IsInfinity(calibrationMaximum) ||
                calibrationMaximum <= calibrationMinimum)
                throw new ArgumentOutOfRangeException(nameof(calibrationMaximum));
            if (double.IsNaN(physicalMinimum) || double.IsInfinity(physicalMinimum) ||
                double.IsNaN(physicalMaximum) || double.IsInfinity(physicalMaximum) ||
                physicalMaximum <= physicalMinimum)
                throw new ArgumentOutOfRangeException(nameof(physicalMaximum));
            if (waveform == VirtualAnalogWaveform.Curve && curve == null)
                throw new ArgumentNullException(nameof(curve), "Curve waveform requires a curve evaluator");

            Waveform = waveform;
            ConstantValue = constantValue;
            RampStart = rampStart;
            RampEnd = rampEnd;
            PeriodMilliseconds = periodMilliseconds;
            SamplePeriodMilliseconds = samplePeriodMilliseconds;
            NoiseAmplitude = noiseAmplitude;
            NoiseSeed = noiseSeed;
            CalibrationOffset = calibrationOffset;
            CalibrationGain = calibrationGain;
            CalibrationMinimum = calibrationMinimum;
            CalibrationMaximum = calibrationMaximum;
            PhysicalMinimum = physicalMinimum;
            PhysicalMaximum = physicalMaximum;
            this.curve = curve;
        }

        public ushort Sample(double timestampMilliseconds)
        {
            var normalized = EvaluateSampledNormalized(timestampMilliseconds);

            var calibrated = (normalized + CalibrationOffset) * CalibrationGain;
            var range = CalibrationMaximum - CalibrationMinimum;
            var raw = (calibrated - CalibrationMinimum) / range;
            raw = Math.Max(0.0, Math.Min(1.0, raw));
            return (ushort)Math.Round(raw * AdcMaximum, MidpointRounding.AwayFromZero);
        }

        public double PhysicalValue(double timestampMilliseconds)
        {
            var normalized = EvaluateSampledNormalized(timestampMilliseconds);
            return PhysicalMinimum + normalized * (PhysicalMaximum - PhysicalMinimum);
        }

        private double EvaluateSampledNormalized(double timestampMilliseconds)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) || timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));

            var sampledTime = SamplePeriodMilliseconds <= 0
                ? timestampMilliseconds
                : Math.Floor(timestampMilliseconds / SamplePeriodMilliseconds) * SamplePeriodMilliseconds;
            var normalized = EvaluateWaveform(sampledTime);
            if (NoiseAmplitude > 0)
                normalized += DeterministicNoise(SampleIndex(sampledTime)) * NoiseAmplitude;
            return normalized;
        }

        private double EvaluateWaveform(double timestampMilliseconds)
        {
            var phase = (timestampMilliseconds % PeriodMilliseconds) / PeriodMilliseconds;
            switch (Waveform)
            {
                case VirtualAnalogWaveform.Constant:
                    return ConstantValue;
                case VirtualAnalogWaveform.Ramp:
                    return RampStart + (RampEnd - RampStart) * phase;
                case VirtualAnalogWaveform.Sine:
                    return 0.5 + 0.5 * Math.Sin(phase * Math.PI * 2.0);
                case VirtualAnalogWaveform.Curve:
                    return curve(phase);
                default:
                    throw new InvalidOperationException("Unsupported analog waveform");
            }
        }

        private long SampleIndex(double sampledTimeMilliseconds)
        {
            if (SamplePeriodMilliseconds <= 0)
                return BitConverter.DoubleToInt64Bits(sampledTimeMilliseconds);
            return (long)Math.Floor(sampledTimeMilliseconds / SamplePeriodMilliseconds);
        }

        private double DeterministicNoise(long sampleIndex)
        {
            unchecked
            {
                ulong value = (ulong)(uint)NoiseSeed;
                value ^= (ulong)sampleIndex + 0x9E3779B97F4A7C15UL + (value << 6) + (value >> 2);
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return ((value & 0x00FFFFFFFFFFFFFFUL) / (double)0x0100000000000000UL) * 2.0 - 1.0;
            }
        }
    }
}