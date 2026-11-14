using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class MillisFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator SourceBuiltMillisReadsDeterministicSimulatedTime()
        {
            var firmware = LoadFirmware("millis_control");
            Assert.IsNotNull(firmware, "millis source-built RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "GPIO");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "SYSTICK");

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);

            machine.Advance(0);

            Assert.AreEqual(0.0, machine.SimulatedTimeMilliseconds);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsFalse(gpio.DigitalRead(13));
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.HalCall &&
                trace.AuxiliaryValue == (int)RsmaHalService.Millis &&
                trace.TimestampMilliseconds == 0.0));

            machine.Advance(25);

            Assert.AreEqual(25.0, machine.SimulatedTimeMilliseconds);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            Assert.IsTrue(gpio.DigitalRead(13));
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.HalCall &&
                trace.AuxiliaryValue == (int)RsmaHalService.Millis &&
                trace.TimestampMilliseconds == 25.0));

            yield return null;
        }

        private static UnityEngine.TextAsset LoadFirmware(string assetName)
        {
            var assets = UnityEngine.Resources.LoadAll<UnityEngine.TextAsset>("RSMA/Firmware");
            foreach (var asset in assets)
            {
                if (asset.name == assetName || asset.name == assetName + ".rsfw")
                    return asset;
            }

            Assert.Fail("No firmware TextAsset named '" + assetName + "' was found");
            return null;
        }
    }
}