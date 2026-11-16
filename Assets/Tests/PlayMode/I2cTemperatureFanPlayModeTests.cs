using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class I2cTemperatureFanPlayModeTests
    {
        [UnityTest]
        public IEnumerator I2cTemperatureCalibrationDrivesFanPwmAndUartTelemetry()
        {
            var firmware = LoadFirmware("i2c_temperature_fan");
            Assert.IsNotNull(firmware, "I2C temperature/fan RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "I2C");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "PWM");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "UART");

            var gpio = new VirtualGpioBank();
            var uart = new VirtualUartBank();
            var buses = new VirtualBusBank();
            var sensor = new VirtualI2cDevice(0x48);
            sensor.SetRegister(0x10, 232);
            buses.RegisterI2cDevice(sensor);
            var backend = new VirtualMcuBackend("RSMA-STM32F103", gpio, uart, buses);
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);

            var machine = VirtualMachine.CreateWithBackend(package, backend);
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsTrue(transmitted.Contains(200));
            Assert.AreEqual(192, gpio.GetPwmOutput(13).DutyCycle);
            Assert.IsTrue(machine.GetTraceSnapshot().Any(item =>
                item.Kind == VirtualTraceEventKind.PwmOutput && item.Value == 192));
            Assert.IsTrue(machine.GetTraceSnapshot().Any(item =>
                item.Kind == VirtualTraceEventKind.UartTransmit && item.Value == 200));

            yield return null;
        }

        [UnityTest]
        public IEnumerator I2cTemperatureNackFaultsFirmwareAndLeavesFanSafe()
        {
            var firmware = LoadFirmware("i2c_temperature_fan");
            Assert.IsNotNull(firmware, "I2C temperature/fan RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var gpio = new VirtualGpioBank();
            var uart = new VirtualUartBank();
            var buses = new VirtualBusBank();
            var sensor = new VirtualI2cDevice(0x48) { Acknowledge = false };
            buses.RegisterI2cDevice(sensor);
            var backend = new VirtualMcuBackend("RSMA-STM32F103", gpio, uart, buses);
            var machine = VirtualMachine.CreateWithBackend(package, backend);

            var exception = Assert.Throws<VirtualMachineFaultException>(() => machine.Advance(0));

            Assert.AreEqual(VirtualExecutionState.Faulted, machine.State);
            StringAssert.Contains("I2C device did not acknowledge", exception.Message);
            Assert.AreEqual(0, gpio.GetPwmOutput(13).DutyCycle);
            Assert.IsTrue(buses.BusDiagnostics.Any(item => item.Code == "I2C_NACK"));

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
            return null;
        }
    }
}