using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.Tests
{
    public sealed class VirtualMcuTests
    {
        [Test]
        public void ReaderRejectsBadMagic()
        {
            Assert.Throws<RsfwFormatException>(() => RsfwReader.Read(new byte[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void ReaderRejectsTruncatedHeader()
        {
            Assert.Throws<RsfwFormatException>(() => RsfwReader.Read(new byte[40]));
        }

        [Test]
        public void ReaderRejectsCorruptHeaderChecksum()
        {
            var firmware = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/RSMA/Firmware/blink.rsfw.bytes");
            Assert.IsNotNull(firmware);

            var corrupted = (byte[])firmware.bytes.Clone();
            corrupted[52] ^= 0x01;
            Assert.Throws<RsfwFormatException>(() => RsfwReader.Read(corrupted));
        }

        [Test]
        public void ReaderLoadsCxxGeneratedBlinkFixture()
        {
            var firmware = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/RSMA/Firmware/blink.rsfw.bytes");
            Assert.IsNotNull(firmware, "C++ generated RSFW fixture was not imported as TextAsset");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            Assert.AreEqual(6, package.Bytecode.Instructions.Count);
            Assert.AreEqual(RsfwOpcode.HalCall, package.Bytecode.Instructions[0].Opcode);
            CollectionAssert.AreEqual(new byte[] { 1, 0, 13, 1 },
                package.Bytecode.Instructions[0].Operands);
        }

        [Test]
        public void CxxGeneratedBlinkFixtureExecutesOnVirtualPin13()
        {
            var firmware = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/RSMA/Firmware/blink.rsfw.bytes");
            Assert.IsNotNull(firmware);

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(RsfwReader.Read(firmware.bytes), gpio);
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsTrue(gpio.DigitalRead(13));
        }

        [Test]
        public void VirtualMachineKeepsBlinkCadenceAcrossLargeTimeStep()
        {
            var firmware = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/RSMA/Firmware/blink.rsfw.bytes");
            Assert.IsNotNull(firmware);

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(RsfwReader.Read(firmware.bytes), gpio);
            var transitions = 0;
            gpio.OutputChanged += (_, __) => transitions++;

            machine.Advance(0);
            machine.Advance(1250);

            Assert.AreEqual(1250.0, machine.SimulatedTimeMilliseconds);
            Assert.AreEqual(3, transitions);
            Assert.IsTrue(gpio.DigitalRead(13));
            Assert.AreEqual(1500.0, machine.WakeTimeMilliseconds);
        }

        [Test]
        public void VirtualMachineExecutesGpioBlinkAndResumesAfterWait()
        {
            var package = MakeBlinkPackage();
            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            var transitions = 0;
            gpio.OutputChanged += (_, __) => transitions++;

            machine.Advance(0);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(3u, machine.ProgramCounter);
            Assert.IsTrue(gpio.DigitalRead(13));
            Assert.AreEqual(500.0, machine.WakeTimeMilliseconds);

            machine.Advance(499);
            Assert.AreEqual(3u, machine.ProgramCounter);
            Assert.IsTrue(gpio.DigitalRead(13));

            machine.Advance(1);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(5u, machine.ProgramCounter);
            Assert.IsFalse(gpio.DigitalRead(13));
            Assert.AreEqual(2, transitions);

            machine.Advance(500);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(3u, machine.ProgramCounter);
            Assert.IsTrue(gpio.DigitalRead(13));
            Assert.AreEqual(3, transitions);
        }

        [Test]
        public void VirtualMachineRejectsUnmappedGpio()
        {
            var package = new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "test",
                    FlashSize = 1024,
                    RamSize = 256,
                    EntryPointName = "reset"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 1, 0, 13, 1 }
            });

            var machine = new VirtualMachine(package, new VirtualGpioBank());
            Assert.Throws<VirtualMachineFaultException>(() => machine.Advance(0));
            Assert.AreEqual(VirtualExecutionState.Faulted, machine.State);
        }

        [Test]
        public void VirtualMachineHaltsAtHaltInstruction()
        {
            var package = MakeBlinkPackage();
            package.Bytecode.Instructions.Clear();
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });

            var machine = new VirtualMachine(package, new VirtualGpioBank());
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
        }

        [Test]
        public void GpioAdapterUsesExistingRSMAGpioWithoutTouchingUnmappedPins()
        {
            var host = new GameObject("RSMAGPIO adapter integration test");
            try
            {
                var gpioType = Type.GetType("RSMAGPIO, Assembly-CSharp", true);
                var portType = Type.GetType("GPIOPort, Assembly-CSharp", true);
                var pinType = Type.GetType("GPIOPin, Assembly-CSharp", true);
                var adapterType = Type.GetType("RSMAGpioHalAdapter, Assembly-CSharp", true);
                var gpio = host.AddComponent(gpioType);
                var ports = (IList)gpioType.GetMethod("GetPortList").Invoke(gpio, null);
                var port = Activator.CreateInstance(portType);
                portType.GetField("name").SetValue(port, "PC");
                var pins = (IList)portType.GetField("pins").GetValue(port);
                var ledPin = CreateLegacyPin(pinType, "13", 0.0f);
                var neighborPin = CreateLegacyPin(pinType, "12", 0.75f);
                pins.Add(ledPin);
                pins.Add(neighborPin);
                ports.Add(port);

                var adapter = adapterType.GetMethod("CreateStm32F103Default")
                    .Invoke(null, new[] { gpio });
                var pinMode = adapterType.GetMethod("PinMode");
                var digitalWrite = adapterType.GetMethod("DigitalWrite");
                var outputMode = (VirtualPinMode)Enum.Parse(typeof(VirtualPinMode), "Output");
                var inputMode = (VirtualPinMode)Enum.Parse(typeof(VirtualPinMode), "Input");

                pinMode.Invoke(adapter, new object[] { (byte)13, outputMode });
                digitalWrite.Invoke(adapter, new object[] { (byte)13, true });
                Assert.AreEqual(1.0f, pinType.GetField("value").GetValue(ledPin));
                Assert.AreEqual(0.75f, pinType.GetField("value").GetValue(neighborPin));

                adapterType.GetMethod("ApplySafeState").Invoke(adapter, null);
                Assert.AreEqual(0.0f, pinType.GetField("value").GetValue(ledPin));
                Assert.AreEqual(0.75f, pinType.GetField("value").GetValue(neighborPin));

                pinMode.Invoke(adapter, new object[] { (byte)13, inputMode });
                var invocation = Assert.Throws<TargetInvocationException>(() =>
                    digitalWrite.Invoke(adapter, new object[] { (byte)13, true }));
                Assert.IsInstanceOf<InvalidOperationException>(invocation.InnerException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object CreateLegacyPin(Type pinType, string name, float value)
        {
            var pin = Activator.CreateInstance(pinType);
            pinType.GetField("name").SetValue(pin, name);
            pinType.GetField("value").SetValue(pin, value);
            pinType.GetField("pinMode").SetValue(pin, Enum.ToObject(
                pinType.GetField("pinMode").FieldType, 0));
            pinType.GetField("outputDefaulValue").SetValue(pin, value);
            return pin;
        }

        private static RsfwPackage MakeBlinkPackage()
        {
            var package = new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "RSMA-STM32F103",
                    FlashSize = 65536,
                    RamSize = 20480,
                    EntryPointName = "reset"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
            package.Manifest.RequiredPeripherals.Add("GPIO");
            package.Manifest.RequiredPeripherals.Add("SYSTICK");
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 1, 0, 13, 1 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 2, 0, 13, 1 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Wait,
                Operands = new byte[] { 0xF4, 0x01, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 2, 0, 13, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Wait,
                Operands = new byte[] { 0xF4, 0x01, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Jump,
                Operands = new byte[] { 1, 0, 0, 0 }
            });
            return package;
        }
    }
}
