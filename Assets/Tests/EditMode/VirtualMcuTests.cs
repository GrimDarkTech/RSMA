using System;
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
