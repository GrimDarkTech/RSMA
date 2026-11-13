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
    [SerializeField] private int irqDispatchCount;
    [SerializeField] private int lastIrqVirtualPin = -1;
    [SerializeField] private string lastIrqEdge = "None";

    private RSMAGpioHalAdapter hal;
    private VirtualMachine virtualMachine;
    private VirtualUartBank uart;

    public VirtualExecutionState ExecutionState => virtualMachine?.State ?? VirtualExecutionState.Ready;
    public string FaultMessage => virtualMachine?.FaultMessage;
    public double SimulatedTimeMilliseconds => virtualMachine?.SimulatedTimeMilliseconds ?? 0.0;
    public int IrqDispatchCount => irqDispatchCount;
    public int LastIrqVirtualPin => lastIrqVirtualPin;
    public string LastIrqEdge => lastIrqEdge;
    public event Action<byte, byte> UartTransmitted;

    public float GetPinValue(byte virtualPin)
    {
        if (hal == null)
            return 0f;
        return hal.ReadPinValue(virtualPin);
    }

    public void PwmWrite(byte virtualPin, byte dutyCycle)
    {
        if (hal == null)
            throw new InvalidOperationException("Virtual MCU is not initialized");
        ((IVirtualPwmHal)hal).PwmWrite(virtualPin, dutyCycle);
    }

    public bool SetButtonPressed(byte virtualPin, bool pressed)
    {
        if (virtualMachine == null || hal == null)
            return false;

        hal.ScheduleInputLevel(virtualPin, virtualMachine.SimulatedTimeMilliseconds, !pressed);
        return true;
    }

    public void InjectUartReceive(byte port, byte[] data)
    {
        if (uart == null)
            throw new InvalidOperationException("Virtual MCU UART is not initialized");
        uart.InjectReceived(port, data);
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
                hal = new RSMAGpioHalAdapter(gpio, RsmaBoardProfiles.Create(expectedBoardId));
            else
                hal = new RSMAGpioHalAdapter(gpio, pinBindings);
            uart = new VirtualUartBank();
            uart.Transmitted += OnUartTransmitted;
            virtualMachine = new VirtualMachine(package, hal, null, uart);
            irqDispatchCount = 0;
            lastIrqVirtualPin = -1;
            lastIrqEdge = "None";
            virtualMachine.InterruptDispatched += OnInterruptDispatched;
        }
        catch (Exception exception)
        {
            Debug.LogError("Virtual MCU failed to load firmware: " + exception.Message, this);
            virtualMachine = null;
            hal = null;
            if (uart != null)
                uart.Transmitted -= OnUartTransmitted;
            uart = null;
            enabled = false;
        }
    }

    private void Update()
    {
        if (virtualMachine == null)
            return;

        try
        {
            virtualMachine.Advance(Time.deltaTime * 1000.0);
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
            virtualMachine.InterruptDispatched -= OnInterruptDispatched;
        if (stopOutputsOnDisable)
            hal?.ApplySafeState();
        if (uart != null)
            uart.Transmitted -= OnUartTransmitted;
        uart = null;
        virtualMachine = null;
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
}
