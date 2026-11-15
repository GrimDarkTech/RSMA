using System;
using System.Collections.Generic;
using UnityEngine;
using RSMA.Microcontroller.Virtual;

[DisallowMultipleComponent]
public sealed class RSMAVirtualMicrocontroller : MonoBehaviour
{
    [SerializeField] private TextAsset firmware;
    [SerializeField] private RSMAGPIO gpio;
    [SerializeField] private List<VirtualPinBinding> pinBindings = new List<VirtualPinBinding>();
    [SerializeField] private string expectedBoardId = "RSMA-STM32F103";
    [SerializeField] private bool stopOutputsOnDisable = true;
    [SerializeField] private bool runAutomatically = true;
    [SerializeField] private int irqDispatchCount;
    [SerializeField] private int lastIrqVirtualPin = -1;
    [SerializeField] private string lastIrqEdge = "None";

    private RSMAGpioHalAdapter hal;
    private VirtualMcuBackend backend;
    private VirtualMachine virtualMachine;
    private VirtualUartBank uart;
    private VirtualGpioBank peripheralBank;
    private readonly List<VirtualAnalogSensor> analogSensors = new List<VirtualAnalogSensor>();

    public VirtualExecutionState ExecutionState => virtualMachine?.State ?? VirtualExecutionState.Ready;
    public string FaultMessage => virtualMachine?.FaultMessage;
    public double SimulatedTimeMilliseconds => virtualMachine?.SimulatedTimeMilliseconds ?? 0.0;
    public int IrqDispatchCount => irqDispatchCount;
    public int LastIrqVirtualPin => lastIrqVirtualPin;
    public string LastIrqEdge => lastIrqEdge;
    public event Action<byte, byte> UartTransmitted;
    public event Action<VirtualTraceEvent> TraceRecorded;

    public List<VirtualTraceEvent> GetTraceSnapshot()
    {
        return virtualMachine == null
            ? new List<VirtualTraceEvent>()
            : virtualMachine.GetTraceSnapshot();
    }

    public void ClearTrace()
    {
        virtualMachine?.ClearTrace();
    }

    public void RegisterAnalogSensor(VirtualAnalogSensor sensor)
    {
        if (sensor == null)
            throw new ArgumentNullException(nameof(sensor));
        if (analogSensors.Contains(sensor))
            return;
        TryRegisterAnalogSensor(sensor);
        analogSensors.Add(sensor);
    }

    public void UnregisterAnalogSensor(VirtualAnalogSensor sensor)
    {
        if (sensor == null)
            return;
        UnregisterAnalogSensorFromHal(sensor);
        analogSensors.Remove(sensor);
    }

    public float GetPinValue(byte virtualPin)
    {
        if (hal == null)
            return 0f;
        return backend.ReadPinValue(virtualPin);
    }

    public void PwmWrite(byte virtualPin, byte dutyCycle)
    {
        if (hal == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        backend.PwmWrite(virtualPin, dutyCycle);
    }

    public bool SetButtonPressed(byte virtualPin, bool pressed)
    {
        if (virtualMachine == null || hal == null)
            return false;

        backend.ScheduleInputLevel(virtualPin, virtualMachine.SimulatedTimeMilliseconds, !pressed);
        return true;
    }

    public void InjectUartReceive(byte port, byte[] data)
    {
        if (uart == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        backend.InjectReceived(port, data);
    }

    public void ScheduleUartReceive(byte port, double timestampMilliseconds, byte[] data)
    {
        if (uart == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        backend.ScheduleReceived(port, timestampMilliseconds, data);
    }

    public void Reset()
    {
        if (virtualMachine == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        virtualMachine.Reset();
        irqDispatchCount = 0;
        lastIrqVirtualPin = -1;
        lastIrqEdge = "None";
    }

    public void AdvanceSimulation(double deltaMilliseconds)
    {
        if (virtualMachine == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        if (double.IsNaN(deltaMilliseconds) || double.IsInfinity(deltaMilliseconds) || deltaMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        virtualMachine.Advance(deltaMilliseconds);
    }

    public bool Step()
    {
        if (virtualMachine == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        return virtualMachine.Step();
    }

    private void OnEnable()
    {
        if (firmware == null)
            firmware = Resources.Load<TextAsset>("RSMA/Firmware/blink.rsfw");
        if (firmware == null)
        {
            Debug.LogError("Virtual MCU firmware asset is not assigned and default Blink resource is missing", this);
            enabled = false;
            return;
        }
        if (gpio == null)
        {
            Debug.LogError("Virtual MCU GPIO component is not assigned", this);
            enabled = false;
            return;
        }

        try
        {
            var package = RsfwReader.Read(firmware.bytes);
            if (!string.IsNullOrEmpty(expectedBoardId) && package.Manifest.BoardId != expectedBoardId)
                throw new RsfwFormatException("Firmware board '" + package.Manifest.BoardId +
                    "' does not match configured board '" + expectedBoardId + "'");

            if (pinBindings.Count == 0)
                hal = new RSMAGpioHalAdapter(gpio, RsmaBoardProfiles.Create(package.Manifest.BoardId));
            else
                hal = new RSMAGpioHalAdapter(gpio, pinBindings);
            uart = new VirtualUartBank();
            peripheralBank = new VirtualGpioBank();
            backend = new VirtualMcuBackend(package.Manifest.BoardId, hal, uart, peripheralBank);
            backend.Transmitted += OnUartTransmitted;
            virtualMachine = VirtualMachine.CreateWithBackend(package, backend);
            virtualMachine.TraceRecorded += OnTraceRecorded;
            for (var i = 0; i < analogSensors.Count; i++)
                TryRegisterAnalogSensor(analogSensors[i]);
            irqDispatchCount = 0;
            lastIrqVirtualPin = -1;
            lastIrqEdge = "None";
            virtualMachine.InterruptDispatched += OnInterruptDispatched;
        }
        catch (Exception exception)
        {
            Debug.LogError("Virtual MCU failed to load firmware: " + exception.Message, this);
            virtualMachine = null;
            if (backend != null)
                backend.Transmitted -= OnUartTransmitted;
            backend = null;
            hal = null;
            uart = null;
            peripheralBank = null;
            enabled = false;
        }
    }

    private void Update()
    {
        if (virtualMachine == null || !runAutomatically)
            return;

        try
        {
            AdvanceSimulation(Time.deltaTime * 1000.0);
        }
        catch (VirtualMachineFaultException exception)
        {
            Debug.LogError(exception.Message, this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (virtualMachine != null)
        {
            virtualMachine.InterruptDispatched -= OnInterruptDispatched;
            virtualMachine.TraceRecorded -= OnTraceRecorded;
        }
        if (stopOutputsOnDisable)
        {
            if (backend != null)
                backend.SafeState();
            else
                hal?.ApplySafeState();
        }
        if (backend != null)
            backend.Transmitted -= OnUartTransmitted;
        uart = null;
        peripheralBank = null;
        for (var i = 0; i < analogSensors.Count; i++)
            UnregisterAnalogSensorFromHal(analogSensors[i]);
        virtualMachine = null;
        backend = null;
        hal = null;
    }

    private void OnInterruptDispatched(VirtualInterruptRequest request)
    {
        irqDispatchCount++;
        lastIrqVirtualPin = request.VirtualPin;
        lastIrqEdge = request.Edge.ToString();
    }

    private void OnUartTransmitted(byte port, byte value)
    {
        UartTransmitted?.Invoke(port, value);
    }

    private void OnTraceRecorded(VirtualTraceEvent traceEvent)
    {
        TraceRecorded?.Invoke(traceEvent);
    }

    private void TryRegisterAnalogSensor(VirtualAnalogSensor sensor)
    {
        if (hal == null)
            return;
        var analogHal = backend as IVirtualAnalogInputSource;
        if (analogHal == null)
            throw new InvalidOperationException("Configured MCU backend does not support virtual analog sensors");
        analogHal.RegisterAnalogSensor(sensor.AdcChannel, sensor.Sample);
    }

    private void UnregisterAnalogSensorFromHal(VirtualAnalogSensor sensor)
    {
        var analogHal = backend as IVirtualAnalogInputSource;
        analogHal?.UnregisterAnalogSensor(sensor.AdcChannel, sensor.Sample);
    }
}
