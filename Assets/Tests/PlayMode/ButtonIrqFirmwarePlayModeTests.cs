using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class ButtonIrqFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator ButtonIrqFirmwareDispatchesPressAndResumesMainLoop()
        {
            var firmware = LoadFirmware("button_irq");
            Assert.IsNotNull(firmware, "Button IRQ RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "IRQ");

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            var dispatched = new List<VirtualInterruptRequest>();
            machine.InterruptDispatched += dispatched.Add;
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(VirtualPinMode.Input, gpio.GetState(13).Mode);
            Assert.AreEqual(VirtualPinPullMode.PullUp, gpio.GetState(13).PullMode);
            Assert.IsTrue(gpio.DigitalRead(13));
            Assert.AreEqual(1000.0, machine.WakeTimeMilliseconds);

            var button = new VirtualButton(gpio, 13);
            button.PressAt(10);
            machine.Advance(50);

            Assert.AreEqual(1, dispatched.Count);
            Assert.AreEqual(13, dispatched[0].VirtualPin);
            Assert.AreEqual(VirtualGpioEdge.Falling, dispatched[0].Edge);
            Assert.AreEqual(10.0, dispatched[0].TimestampMilliseconds);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(0, machine.InterruptDepth);
            Assert.IsFalse(gpio.DigitalRead(13));

            button.ReleaseAt(60);
            machine.Advance(50);

            Assert.AreEqual(1, dispatched.Count);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsTrue(gpio.DigitalRead(13));

            machine.Advance(900);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(2000.0, machine.WakeTimeMilliseconds);
            Assert.AreEqual(1, dispatched.Count);

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

            var importedNames = new System.Text.StringBuilder();
            foreach (var asset in assets)
            {
                if (importedNames.Length != 0)
                    importedNames.Append(", ");
                importedNames.Append(asset.name);
            }
            Assert.Fail("No firmware TextAsset named '" + assetName + "' was found. Imported resources: " + importedNames);
            return null;
        }
    }
}