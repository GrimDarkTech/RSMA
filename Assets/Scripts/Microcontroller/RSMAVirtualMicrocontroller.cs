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

    public VirtualExecutionState ExecutionState => virtualMachine?.State ?? VirtualExecutionState.Ready;
    public string FaultMessage => virtualMachine?.FaultMessage;
    public double SimulatedTimeMilliseconds => virtualMachine?.SimulatedTimeMilliseconds ?? 0.0;
    public int IrqDispatchCount => irqDispatchCount;
    public int LastIrqVirtualPin => lastIrqVirtualPin;
    public string LastIrqEdge => lastIrqEdge;

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

            hal = pinBindings.Count == 0 && expectedBoardId == "RSMA-STM32F103"
                ? RSMAGpioHalAdapter.CreateStm32F103Default(gpio)
                : new RSMAGpioHalAdapter(gpio, pinBindings);
            virtualMachine = new VirtualMachine(package, hal);
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
        virtualMachine = null;
        hal = null;
    }

    private void OnInterruptDispatched(VirtualInterruptRequest request)
    {
        irqDispatchCount++;
        lastIrqVirtualPin = request.VirtualPin;
        lastIrqEdge = request.Edge.ToString();
    }
}
