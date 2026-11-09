using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class PwmMotorFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator PwmMotorFirmwareRampsReversesAndStops()
        {
            var firmware = Resources.Load<TextAsset>("RSMA/Firmware/pwm_motor_test");
            Assert.IsNotNull(firmware, "PWM motor RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(10).Mode);
            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(11).Mode);
            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(13).Mode);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);

            yield return null;

            machine.Advance(500);
            Assert.AreEqual(64f / 255f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsTrue(gpio.DigitalRead(10));
            Assert.IsFalse(gpio.DigitalRead(11));

            machine.Advance(4000);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsTrue(gpio.DigitalRead(10));
            Assert.IsFalse(gpio.DigitalRead(11));

            machine.Advance(500);
            Assert.AreEqual(64f / 255f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsFalse(gpio.DigitalRead(10));
            Assert.IsTrue(gpio.DigitalRead(11));

            machine.Advance(4000);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsFalse(gpio.DigitalRead(10));
            Assert.IsTrue(gpio.DigitalRead(11));

            machine.Advance(500);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsFalse(gpio.DigitalRead(10));
            Assert.IsFalse(gpio.DigitalRead(11));

            yield return null;
        }
    }
}