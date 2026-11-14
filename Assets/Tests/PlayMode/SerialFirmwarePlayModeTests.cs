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

        [UnityTest]
        public IEnumerator SerialReceiveCallbackRunsAfterScheduledRxAndCanEchoTheByte()
        {
            var firmware = LoadFirmware("serial_callback");
            Assert.IsNotNull(firmware, "Serial callback RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            uart.ScheduleReceived(1, 12, new byte[] { 0x44 });
            machine.Advance(20);

            CollectionAssert.AreEqual(new byte[] { 0x44 }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            var trace = machine.GetTraceSnapshot();
            var received = trace.Find(item => item.Kind == VirtualTraceEventKind.UartReceive &&
                item.Channel == 1 && item.Value == 0x44);
            var callback = trace.Find(item => item.Kind == VirtualTraceEventKind.UartCallback &&
                item.Channel == 1);
            var transmittedTrace = trace.Find(item => item.Kind == VirtualTraceEventKind.UartTransmit &&
                item.Channel == 1 && item.Value == 0x44);
            Assert.IsNotNull(received);
            Assert.IsNotNull(callback);
            Assert.IsNotNull(transmittedTrace);
            Assert.AreEqual(12.0, callback.TimestampMilliseconds);
            Assert.Less(received.Sequence, callback.Sequence);
            Assert.Less(callback.Sequence, transmittedTrace.Sequence);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SerialPacketCallbackRunsOnlyForValidHitlFrame()
        {
            var firmware = LoadFirmware("serial_packet_callback");
            Assert.IsNotNull(firmware, "Serial packet callback RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            var frame = HitlFrameCodec.Encode(new HitlFrame
            {
                Type = HitlMessageType.Data,
                Sequence = 7,
                TimestampMilliseconds = 12,
                Payload = new byte[] { 0x6B }
            });
            var invalid = (byte[])frame.Clone();
            invalid[invalid.Length - 1] ^= 0x01;
            uart.InjectHitlFrame(1, invalid);
            Assert.AreEqual(0, transmitted.Count);
            var split = frame.Length / 2;
            uart.InjectHitlFrame(1, SubArray(frame, 0, split));
            machine.Advance(1);
            Assert.AreEqual(0, transmitted.Count);
            uart.InjectHitlFrame(1, SubArray(frame, split, frame.Length - split));
            machine.Advance(1);

            CollectionAssert.AreEqual(new byte[] { 0x6B }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            var trace = machine.GetTraceSnapshot();
            Assert.IsNotNull(trace.Find(item => item.Kind == VirtualTraceEventKind.UartPacketReceive &&
                item.Channel == 1 && item.Value == 1 && item.AuxiliaryValue == 5));
            Assert.IsNotNull(trace.Find(item => item.Kind == VirtualTraceEventKind.UartPacketCallback &&
                item.Channel == 1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator SerialPacketCallbackAcceptsValidZeroLengthPayload()
        {
            var firmware = LoadFirmware("serial_packet_callback");
            Assert.IsNotNull(firmware, "Serial packet callback RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);

            machine.Advance(0);
            uart.InjectHitlFrame(1, HitlFrameCodec.Encode(new HitlFrame
            {
                Type = HitlMessageType.Data,
                Sequence = 8,
                Payload = System.Array.Empty<byte>()
            }));
            machine.Advance(1);

            Assert.IsEmpty(transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(item =>
                item.Kind == VirtualTraceEventKind.UartPacketCallback && item.Channel == 1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator SerialPacketCallbackCanRunAgainAfterVirtualMachineReset()
        {
            var firmware = LoadFirmware("serial_packet_callback");
            Assert.IsNotNull(firmware, "Serial packet callback RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            var uart = new VirtualUartBank();
            var transmitted = new List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);
            var frame = HitlFrameCodec.Encode(new HitlFrame
            {
                Type = HitlMessageType.Data,
                Sequence = 9,
                Payload = new byte[] { 0x31 }
            });

            machine.Advance(0);
            uart.InjectHitlFrame(1, frame);
            machine.Advance(1);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);

            machine.Reset();
            machine.Advance(0);
            uart.InjectHitlFrame(1, frame);
            machine.Advance(1);

            CollectionAssert.AreEqual(new byte[] { 0x31, 0x31 }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            Assert.GreaterOrEqual(machine.GetTraceSnapshot().FindAll(item =>
                item.Kind == VirtualTraceEventKind.UartPacketCallback && item.Channel == 1).Count, 1);

            yield return null;
        }

        private static byte[] SubArray(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            System.Array.Copy(source, offset, result, 0, count);
            return result;
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