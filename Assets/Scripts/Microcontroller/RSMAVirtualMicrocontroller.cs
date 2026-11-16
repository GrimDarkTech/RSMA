using System;
using System.Collections.Generic;
using System.Linq;
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
    [Header("Virtual scheduling")]
    [SerializeField] private VirtualHostClockPolicy hostClockPolicy = VirtualHostClockPolicy.ScaledDeltaTime;
    [SerializeField] private bool pauseWhenUnityPaused = true;
    [SerializeField, Min(0.001f)] private float maximumHostDeltaMilliseconds = 1000f;
    [SerializeField, Min(1)] private int interruptQueueCapacity = VirtualSchedulingPolicy.DefaultInterruptQueueCapacity;
    [SerializeField, Min(0f)] private double interruptDebounceMilliseconds;
    [SerializeField] private VirtualInterruptMaskPolicy interruptMaskPolicy = VirtualInterruptMaskPolicy.QueueWhileMasked;
    [SerializeField] private int irqDispatchCount;
    [SerializeField] private int lastIrqVirtualPin = -1;
    [SerializeField] private string lastIrqEdge = "None";

    private RSMAGpioHalAdapter hal;
    private VirtualMcuBackend backend;
    private VirtualMachine virtualMachine;
    private VirtualUartBank uart;
    private VirtualBusBank buses;
    private VirtualGpioBank peripheralBank;
    private readonly List<VirtualAnalogSensor> analogSensors = new List<VirtualAnalogSensor>();
    private readonly List<string> analogSensorDiagnostics = new List<string>();

    public VirtualExecutionState ExecutionState => virtualMachine?.State ?? VirtualExecutionState.Ready;
    public string FaultMessage => virtualMachine?.FaultMessage;
    public double SimulatedTimeMilliseconds => virtualMachine?.SimulatedTimeMilliseconds ?? 0.0;
    public int IrqDispatchCount => irqDispatchCount;
    public int LastIrqVirtualPin => lastIrqVirtualPin;
    public string LastIrqEdge => lastIrqEdge;
    public ulong DroppedInterruptCount => backend == null ? 0UL : backend.DroppedInterruptCount;
    public IList<VirtualSchedulingDiagnostic> SchedulingDiagnostics => backend == null
        ? new List<VirtualSchedulingDiagnostic>() : backend.SchedulingDiagnostics;
    public IList<string> AnalogSensorDiagnostics => analogSensorDiagnostics.AsReadOnly();
    public event Action<byte, byte> UartTransmitted;
    public event Action<VirtualUartDiagnostic> UartDiagnosticRaised;
    public event Action<VirtualBusDiagnostic> BusDiagnosticRaised;
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

    public VirtualGpioElectricalState GetElectricalState(byte virtualPin)
    {
        if (backend == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        return backend.GetElectricalState(virtualPin);
    }

    public void SetExternalDrive(byte virtualPin, bool level)
    {
        if (backend == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        backend.SetExternalDrive(virtualPin, level);
    }

    public void ClearExternalDrive(byte virtualPin)
    {
        if (backend == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        backend.ClearExternalDrive(virtualPin);
    }

    public void PwmWrite(byte virtualPin, byte dutyCycle)
    {
        if (hal == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        backend.PwmWrite(virtualPin, dutyCycle);
    }

    public VirtualPwmOutputState GetPwmOutput(byte virtualPin)
    {
        if (backend == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        return backend.GetPwmOutput(virtualPin);
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

    public void InjectUartTransportBytes(byte port, byte[] data)
    {
        if (uart == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        backend.InjectTransportBytes(port, data);
    }

    public void ScheduleUartReceive(byte port, double timestampMilliseconds, byte[] data)
    {
        if (uart == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        backend.ScheduleReceived(port, timestampMilliseconds, data);
    }

    public int GetUartTransmitQueueCount(byte port)
    {
        if (backend == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        return backend.GetTransmitQueueCount(port);
    }

    public ulong DroppedUartReceiveByteCount => backend == null ? 0UL : backend.DroppedReceiveByteCount;
    public ulong DroppedUartTransmitByteCount => backend == null ? 0UL : backend.DroppedTransmitByteCount;
    public IList<VirtualUartDiagnostic> UartDiagnostics => backend == null
        ? new List<VirtualUartDiagnostic>() : backend.UartDiagnostics;
    public IList<VirtualBusDiagnostic> BusDiagnostics => backend == null
        ? new List<VirtualBusDiagnostic>() : backend.BusDiagnostics;

    public void RegisterI2cDevice(VirtualI2cDevice device)
    {
        if (buses == null)
            throw new InvalidOperationException("Virtual MCU bus bank is not initialized");
        buses.RegisterI2cDevice(device);
    }

    public void RegisterSpiDevice(VirtualSpiDevice device)
    {
        if (buses == null)
            throw new InvalidOperationException("Virtual MCU bus bank is not initialized");
        buses.RegisterSpiDevice(device);
    }

    public VirtualBusTransactionResult I2cReadRegister(byte address, byte registerAddress, int length,
        int speedHz = 0, double timeoutMilliseconds = -1)
    {
        return backend.I2cReadRegister(address, registerAddress, length, speedHz, timeoutMilliseconds);
    }

    public VirtualBusTransactionResult I2cWriteRegister(byte address, byte registerAddress, byte[] data,
        int speedHz = 0, double timeoutMilliseconds = -1)
    {
        return backend.I2cWriteRegister(address, registerAddress, data, speedHz, timeoutMilliseconds);
    }

    public VirtualBusTransactionResult SpiReadRegister(byte bus, byte chipSelect, byte registerAddress,
        int length, VirtualSpiMode mode = VirtualSpiMode.Mode0,
        VirtualBitOrder bitOrder = VirtualBitOrder.MsbFirst, int speedHz = 0, double timeoutMilliseconds = -1)
    {
        return backend.SpiReadRegister(bus, chipSelect, registerAddress, length, mode, bitOrder,
            speedHz, timeoutMilliseconds);
    }

    public VirtualBusTransactionResult SpiWriteRegister(byte bus, byte chipSelect, byte registerAddress,
        byte[] data, VirtualSpiMode mode = VirtualSpiMode.Mode0,
        VirtualBitOrder bitOrder = VirtualBitOrder.MsbFirst, int speedHz = 0, double timeoutMilliseconds = -1)
    {
        return backend.SpiWriteRegister(bus, chipSelect, registerAddress, data, mode, bitOrder,
            speedHz, timeoutMilliseconds);
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
        analogSensorDiagnostics.Clear();
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

            var boardProfile = RsmaBoardProfiles.Create(package.Manifest.BoardId);
            if (pinBindings.Count != 0)
                ValidateSceneBindings(boardProfile, pinBindings);
            hal = pinBindings.Count == 0
                ? new RSMAGpioHalAdapter(gpio, boardProfile)
                : new RSMAGpioHalAdapter(gpio, boardProfile, pinBindings);
            uart = new VirtualUartBank();
            buses = new VirtualBusBank();
            peripheralBank = new VirtualGpioBank();
            backend = new VirtualMcuBackend(boardProfile, hal, CreateSchedulingPolicy(), uart, buses, peripheralBank);
            backend.Transmitted += OnUartTransmitted;
            backend.DiagnosticRaised += OnUartDiagnostic;
            backend.BusDiagnosticRaised += OnBusDiagnostic;
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
            analogSensorDiagnostics.Add(exception.Message);
            Debug.LogError("Virtual MCU failed to load firmware: " + exception.Message, this);
            virtualMachine = null;
            if (backend != null)
            {
                backend.Transmitted -= OnUartTransmitted;
                backend.DiagnosticRaised -= OnUartDiagnostic;
                backend.BusDiagnosticRaised -= OnBusDiagnostic;
            }
            backend = null;
            hal = null;
            uart = null;
            buses = null;
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
            var policy = CreateSchedulingPolicy();
            var deltaMilliseconds = policy.ComputeHostDeltaMilliseconds(
                Time.deltaTime * 1000.0,
                Time.unscaledDeltaTime * 1000.0,
                pauseWhenUnityPaused && Mathf.Approximately(Time.timeScale, 0f),
                backend == null ? null : backend.Diagnostics);
            AdvanceSimulation(deltaMilliseconds);
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
        {
            backend.Transmitted -= OnUartTransmitted;
            backend.DiagnosticRaised -= OnUartDiagnostic;
            backend.BusDiagnosticRaised -= OnBusDiagnostic;
        }
        uart = null;
        buses = null;
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

    private VirtualSchedulingPolicy CreateSchedulingPolicy()
    {
        return new VirtualSchedulingPolicy
        {
            HostClockPolicy = hostClockPolicy,
            InterruptMaskPolicy = interruptMaskPolicy,
            InterruptQueueCapacity = Mathf.Clamp(interruptQueueCapacity, 1,
                VirtualInterruptController.MaxPendingInterrupts),
            InterruptDebounceMilliseconds = Math.Max(0.0, interruptDebounceMilliseconds),
            MaximumHostDeltaMilliseconds = Math.Max(0.001, maximumHostDeltaMilliseconds)
        };
    }

    private void OnUartTransmitted(byte port, byte value)
    {
        UartTransmitted?.Invoke(port, value);
    }

    private void OnUartDiagnostic(VirtualUartDiagnostic diagnostic)
    {
        UartDiagnosticRaised?.Invoke(diagnostic);
    }

    private void OnBusDiagnostic(VirtualBusDiagnostic diagnostic)
    {
        BusDiagnosticRaised?.Invoke(diagnostic);
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
        try
        {
            analogHal.RegisterAnalogSensor(sensor.AdcChannel, sensor.Sample);
        }
        catch (Exception exception)
        {
            analogSensorDiagnostics.Add("ADC channel " + sensor.AdcChannel + ": " + exception.Message);
            throw;
        }
    }

    private void UnregisterAnalogSensorFromHal(VirtualAnalogSensor sensor)
    {
        var analogHal = backend as IVirtualAnalogInputSource;
        analogHal?.UnregisterAnalogSensor(sensor.AdcChannel, sensor.Sample);
    }

    private static void ValidateSceneBindings(VirtualBoardProfile profile,
        IEnumerable<VirtualPinBinding> sceneBindings)
    {
        foreach (var binding in sceneBindings)
        {
            if (binding == null)
                throw new BoardProfileException("Scene contains a null virtual pin binding");
            var definition = profile.GetPin(binding.virtualPin);
            if (!definition.SupportsPhysicalRoute(binding.gpioPin.portName, binding.gpioPin.pinName))
                throw new BoardProfileException("Scene binding for virtual pin " + binding.virtualPin +
                    " does not match board profile physical mapping");
        }
    }
}
