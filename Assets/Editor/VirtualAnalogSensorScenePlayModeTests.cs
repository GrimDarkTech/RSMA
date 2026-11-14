using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

public sealed class VirtualAnalogSensorScenePlayModeTests
{
    [UnitySetUp]
    public IEnumerator EnterPlayMode()
    {
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitPlayMode()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SceneAdapterRunsSourceBuiltAnalogThresholdFirmware()
    {
        var gpioObject = new GameObject("Analog sensor scene GPIO");
        var mcuObject = new GameObject("Analog sensor scene MCU");
        try
        {
            mcuObject.SetActive(false);
            var gpio = gpioObject.AddComponent<RSMAGPIO>();
            AddPin(gpio, "PC", "10");
            AddPin(gpio, "PC", "11");
            AddPin(gpio, "PC", "13");

            var mcu = mcuObject.AddComponent<RSMAVirtualMicrocontroller>();
            var sensor = mcuObject.AddComponent<VirtualAnalogSensor>();
            SetPrivateField(mcu, "firmware", LoadFirmware("analog_threshold_pwm"));
            SetPrivateField(mcu, "gpio", gpio);
            SetPrivateField(mcu, "runAutomatically", false);
            SetPrivateField(sensor, "microcontroller", mcu);
            SetPrivateField(sensor, "adcChannel", 10);
            SetPrivateField(sensor, "waveform", VirtualAnalogWaveform.Ramp);
            SetPrivateField(sensor, "rampStart", 0f);
            SetPrivateField(sensor, "rampEnd", 1f);
            SetPrivateField(sensor, "periodMilliseconds", 20f);
            SetPrivateField(sensor, "samplePeriodMilliseconds", 0f);
            SetPrivateField(sensor, "calibrationMinimum", 0f);
            SetPrivateField(sensor, "calibrationMaximum", 1f);

            mcuObject.SetActive(true);

            mcu.AdvanceSimulation(0);
            Assert.AreEqual(VirtualExecutionState.Waiting, mcu.ExecutionState);
            Assert.AreEqual(0, sensor.CurrentSample);
            Assert.AreEqual(0f, gpio.GetPin("PC", "13").value, 0.0001f);

            mcu.AdvanceSimulation(10);
            Assert.AreEqual(2048, sensor.CurrentSample);
            Assert.AreEqual(1f, gpio.GetPin("PC", "13").value, 0.0001f);

            mcu.AdvanceSimulation(10);
            Assert.AreEqual(0, sensor.CurrentSample);
            Assert.AreEqual(0f, gpio.GetPin("PC", "13").value, 0.0001f);
        }
        finally
        {
            Object.Destroy(gpioObject);
            Object.Destroy(mcuObject);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator SceneAdapterReportsUnmappedAnalogSensorBeforeSensorRuns()
    {
        var gpioObject = new GameObject("Unmapped analog scene GPIO");
        var mcuObject = new GameObject("Unmapped analog scene MCU");
        try
        {
            mcuObject.SetActive(false);
            var gpio = gpioObject.AddComponent<RSMAGPIO>();
            AddPin(gpio, "PC", "10");
            AddPin(gpio, "PC", "11");
            AddPin(gpio, "PC", "13");

            var mcu = mcuObject.AddComponent<RSMAVirtualMicrocontroller>();
            var sensor = mcuObject.AddComponent<VirtualAnalogSensor>();
            SetPrivateField(mcu, "firmware", LoadFirmware("analog_threshold_pwm"));
            SetPrivateField(mcu, "gpio", gpio);
            SetPrivateField(mcu, "runAutomatically", false);
            SetPrivateField(sensor, "microcontroller", mcu);
            SetPrivateField(sensor, "adcChannel", 42);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "VirtualAnalogSensor failed to initialize: ADC channel is not mapped: 42"));
            mcuObject.SetActive(true);

            Assert.IsFalse(sensor.enabled);
            Assert.AreEqual(0, sensor.CurrentSample);
        }
        finally
        {
            Object.Destroy(gpioObject);
            Object.Destroy(mcuObject);
        }

        yield return null;
    }

    private static TextAsset LoadFirmware(string assetName)
    {
        var assets = Resources.LoadAll<TextAsset>("RSMA/Firmware");
        foreach (var asset in assets)
        {
            if (asset.name == assetName || asset.name == assetName + ".rsfw")
                return asset;
        }
        Assert.Fail("No firmware TextAsset named '" + assetName + "' was found");
        return null;
    }

    private static void AddPin(RSMAGPIO gpio, string portName, string pinName)
    {
        var port = gpio.GetPortList().Find(candidate => candidate.name == portName);
        if (port == null)
        {
            port = new GPIOPort { name = portName };
            gpio.GetPortList().Add(port);
        }
        port.pins.Add(new GPIOPin
        {
            name = pinName,
            pinMode = PinModes.Input,
            pinPullMode = PinPullModes.NoPullUpDown,
            pinOutputMode = PinOutputModes.PushPull,
            value = RSMAGPIO.Low,
            outputDefaulValue = RSMAGPIO.Low
        });
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing private field '" + name + "'");
        field.SetValue(target, value);
    }
}