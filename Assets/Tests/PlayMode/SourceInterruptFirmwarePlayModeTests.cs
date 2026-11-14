using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class SourceInterruptFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator SourceBuiltAttachInterruptDispatchesHandlerAndDetachStopsFutureEdges()
        {
            var firmware = LoadFirmware("source_interrupt");
            Assert.IsNotNull(firmware, "Source interrupt RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "GPIO");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "IRQ");

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsFalse(gpio.DigitalRead(10));

            gpio.ScheduleInputLevel(13, 10, true);
            machine.Advance(10);

            Assert.IsTrue(gpio.DigitalRead(10));
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(0, machine.InterruptDepth);
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.Interrupt &&
                trace.TimestampMilliseconds == 10.0));
            var traceEvents = machine.GetTraceSnapshot();
            var interruptIndex = traceEvents.FindIndex(trace =>
                trace.Kind == VirtualTraceEventKind.Interrupt && trace.TimestampMilliseconds == 10.0);
            var outputIndex = traceEvents.FindIndex(trace =>
                trace.Kind == VirtualTraceEventKind.DigitalOutput && trace.Channel == 10 && trace.Value == 1);
            Assert.GreaterOrEqual(interruptIndex, 0);
            Assert.GreaterOrEqual(outputIndex, 0);
            Assert.Less(interruptIndex, outputIndex);
            Assert.AreEqual(10.0, traceEvents[outputIndex].TimestampMilliseconds);

            machine.Advance(90);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);

            gpio.ScheduleInputLevel(13, 100, false);
            machine.Advance(0);
            Assert.IsTrue(gpio.DigitalRead(10), "Detached interrupt must not dispatch the handler again");

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