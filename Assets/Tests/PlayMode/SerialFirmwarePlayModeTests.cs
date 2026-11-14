using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class SerialFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator SerialFacadeEchoesInjectedByteAndRecordsUartTrace()
        {
            var firmware = LoadFirmware("serial_echo");
            Assert.IsNotNull(firmware, "Serial echo RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "UART");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "SYSTICK");

            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            Assert.AreEqual(115200, uart.GetBaudRate(0));
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);

            uart.InjectReceived(0, new byte[] { 0x5A });
            machine.Advance(1);

            CollectionAssert.AreEqual(new byte[] { 0x5A }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            var trace = machine.GetTraceSnapshot();
            Assert.IsNotNull(trace.Find(item => item.Kind == VirtualTraceEventKind.UartTransmit &&
                item.Channel == 0 && item.Value == 0x5A && item.TimestampMilliseconds == 1.0));

            yield return null;
        }

        [UnityTest]
        public IEnumerator SerialWriteRejectsRuntimeValueOutsideByteRange()
        {
            var firmware = LoadFirmware("serial_invalid_write");
            Assert.IsNotNull(firmware, "Serial invalid-write RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, new VirtualUartBank());
            var exception = Assert.Throws<VirtualMachineFaultException>(() => machine.Advance(0));

            Assert.AreEqual(VirtualExecutionState.Faulted, machine.State);
            StringAssert.Contains("UART write value must be between 0 and 255", exception.Message);
            StringAssert.Contains("UART write value must be between 0 and 255", machine.FaultMessage);
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.Fault &&
                trace.Message.Contains("UART write value must be between 0 and 255")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScheduledSerialReceiveWakesPollingFirmwareAtDeterministicTimestamp()
        {
            var firmware = LoadFirmware("serial_echo");
            Assert.IsNotNull(firmware, "Serial echo RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            uart.ScheduleReceived(0, 25, new byte[] { 0x33 });
            machine.Advance(100);

            CollectionAssert.AreEqual(new byte[] { 0x33 }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            var trace = machine.GetTraceSnapshot();
            var received = trace.Find(item => item.Kind == VirtualTraceEventKind.UartReceive &&
                item.Channel == 0 && item.Value == 0x33);
            var transmittedTrace = trace.Find(item => item.Kind == VirtualTraceEventKind.UartTransmit &&
                item.Channel == 0 && item.Value == 0x33);
            Assert.IsNotNull(received);
            Assert.IsNotNull(transmittedTrace);
            Assert.AreEqual(25.0, received.TimestampMilliseconds);
            Assert.AreEqual(25.0, transmittedTrace.TimestampMilliseconds);
            Assert.Less(received.Sequence, transmittedTrace.Sequence);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Serial1AndSerial2UseIndependentUartPorts()
        {
            var firmware = LoadFirmware("serial_ports");
            Assert.IsNotNull(firmware, "Multi-port Serial RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<string>();
            uart.Transmitted += (port, value) => transmitted.Add(port + ":" + value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            Assert.AreEqual(9600, uart.GetBaudRate(1));
            Assert.AreEqual(19200, uart.GetBaudRate(2));
            uart.ScheduleReceived(2, 10, new byte[] { 0x22 });
            uart.ScheduleReceived(1, 20, new byte[] { 0x11 });
            machine.Advance(30);

            CollectionAssert.AreEqual(new[] { "1:17", "2:34" }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            var trace = machine.GetTraceSnapshot();
            Assert.IsNotNull(trace.Find(item => item.Kind == VirtualTraceEventKind.UartReceive &&
                item.Channel == 1 && item.Value == 0x11 && item.TimestampMilliseconds == 20.0));
            Assert.IsNotNull(trace.Find(item => item.Kind == VirtualTraceEventKind.UartReceive &&
                item.Channel == 2 && item.Value == 0x22 && item.TimestampMilliseconds == 10.0));

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