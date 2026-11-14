using System;
using RSMA.Microcontroller.Virtual;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VirtualAnalogSensor : MonoBehaviour
{
    [SerializeField] private RSMAVirtualMicrocontroller microcontroller;
    [SerializeField, Range(0, 255)] private int adcChannel;
    [SerializeField] private VirtualAnalogWaveform waveform = VirtualAnalogWaveform.Constant;
    [SerializeField, Range(-1f, 1f)] private float constantValue;
    [SerializeField, Range(-1f, 1f)] private float rampStart;
    [SerializeField, Range(-1f, 1f)] private float rampEnd = 1f;
    [SerializeField, Min(0.000001f)] private float periodMilliseconds = 1000f;
    [SerializeField, Min(0f)] private float samplePeriodMilliseconds;
    [SerializeField, Min(0f)] private float noiseAmplitude;
    [SerializeField] private int noiseSeed = 1;
    [SerializeField] private float calibrationOffset;
    [SerializeField] private float calibrationGain = 1f;
    [SerializeField] private float calibrationMinimum;
    [SerializeField] private float calibrationMaximum = 1f;
    [SerializeField] private float physicalMinimum;
    [SerializeField] private float physicalMaximum = 1f;
    [SerializeField] private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    [SerializeField] private string units = "raw";

    private VirtualAnalogSensorModel model;
    private bool registered;

    public byte AdcChannel => (byte)adcChannel;
    public string Units => units;
    public ushort CurrentSample { get; private set; }
    public double CurrentPhysicalValue { get; private set; }

    private void OnEnable()
    {
        if (microcontroller == null)
            microcontroller = GetComponent<RSMAVirtualMicrocontroller>();
        if (microcontroller == null)
        {
            Debug.LogError("VirtualAnalogSensor requires an RSMAVirtualMicrocontroller", this);
            enabled = false;
            return;
        }

        try
        {
            model = CreateModel();
            microcontroller.RegisterAnalogSensor(this);
            registered = true;
        }
        catch (Exception exception)
        {
            Debug.LogError("VirtualAnalogSensor failed to initialize: " + exception.Message, this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (registered && microcontroller != null)
            microcontroller.UnregisterAnalogSensor(this);
        registered = false;
        model = null;
    }

    public ushort Sample(double simulatedTimeMilliseconds)
    {
        if (model == null)
            throw new InvalidOperationException("VirtualAnalogSensor is not initialized");
        CurrentSample = model.Sample(simulatedTimeMilliseconds);
        CurrentPhysicalValue = model.PhysicalValue(simulatedTimeMilliseconds);
        return CurrentSample;
    }

    private VirtualAnalogSensorModel CreateModel()
    {
        if (curve == null)
            throw new InvalidOperationException("Curve waveform requires an AnimationCurve");
        var capturedCurve = curve;
        return new VirtualAnalogSensorModel(
            waveform,
            constantValue,
            rampStart,
            rampEnd,
            periodMilliseconds,
            samplePeriodMilliseconds,
            noiseAmplitude,
            noiseSeed,
            calibrationOffset,
            calibrationGain,
            calibrationMinimum,
            calibrationMaximum,
            physicalMinimum,
            physicalMaximum,
            phase => capturedCurve.Evaluate((float)phase));
    }
}