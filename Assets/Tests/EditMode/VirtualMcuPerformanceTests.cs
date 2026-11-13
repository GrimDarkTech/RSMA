using System;
using NUnit.Framework;
using RSMA.Microcontroller.Virtual;
using Unity.PerformanceTesting;

namespace RSMA.Microcontroller.Virtual.Tests
{
    public sealed class VirtualMcuPerformanceTests
    {
        private const int OperationsPerWorkload = 8192;

        [Test]
        public void ProfilerCountsInstructionsHalCallsAndPeakStackDepths()
        {
            var package = CreatePackage();
            package.Bytecode.Instructions.Add(ConstI32(42));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Call,
                Operands = new byte[] { 3, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });
            package.Bytecode.Instructions.Add(DigitalWrite());
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Return,
                Operands = Array.Empty<byte>()
            });

            var profiler = new VirtualMachineProfiler();
            var machine = new VirtualMachine(package, new CountingHal(), null, null, profiler);
            machine.Advance(0);

            Assert.AreEqual(5, profiler.InstructionsExecuted);
            Assert.AreEqual(1, profiler.HalCallsExecuted);
            Assert.AreEqual(1, profiler.MaximumValueStackDepth);
            Assert.AreEqual(1, profiler.MaximumCallStackDepth);
            Assert.AreEqual(0, profiler.MaximumInterruptStackDepth);

            machine.Reset();
            Assert.AreEqual(0, profiler.InstructionsExecuted);
            Assert.AreEqual(0, profiler.HalCallsExecuted);
            Assert.AreEqual(0, profiler.MaximumValueStackDepth);
            Assert.AreEqual(0, profiler.MaximumCallStackDepth);
            Assert.AreEqual(0, profiler.MaximumInterruptStackDepth);
        }

        [Test, Performance]
        public void VmComputeAdvanceWithoutProfiling()
        {
            MeasureWorkload(CreateComputePackage(), false);
        }

        [Test, Performance]
        public void VmComputeAdvanceWithProfiling()
        {
            MeasureWorkload(CreateComputePackage(), true);
        }

        [Test, Performance]
        public void VmHalDispatchAdvanceWithoutProfiling()
        {
            MeasureWorkload(CreateHalPackage(), false);
        }

        [Test, Performance]
        public void VmHalDispatchAdvanceWithProfiling()
        {
            MeasureWorkload(CreateHalPackage(), true);
        }

        private static void MeasureWorkload(RsfwPackage package, bool enableProfiling)
        {
            var profiler = enableProfiling ? new VirtualMachineProfiler() : null;
            var machine = new VirtualMachine(package, new CountingHal(), null, null, profiler);

            Measure.Method(() => machine.Advance(0))
                .SampleGroup(new SampleGroup("Advance", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(12)
                .IterationsPerMeasurement(4)
                .SetUp(machine.Reset)
                .Run();

            if (profiler == null)
                return;

            Measure.Custom(new SampleGroup("InstructionsPerAdvance", SampleUnit.Undefined),
                profiler.InstructionsExecuted);
            Measure.Custom(new SampleGroup("HalCallsPerAdvance", SampleUnit.Undefined),
                profiler.HalCallsExecuted);
            Measure.Custom(new SampleGroup("MaxValueStackDepth", SampleUnit.Undefined),
                profiler.MaximumValueStackDepth);
            Measure.Custom(new SampleGroup("MaxCallStackDepth", SampleUnit.Undefined),
                profiler.MaximumCallStackDepth);
            Measure.Custom(new SampleGroup("MaxInterruptStackDepth", SampleUnit.Undefined),
                profiler.MaximumInterruptStackDepth);
        }

        private static RsfwPackage CreateComputePackage()
        {
            var package = CreatePackage();
            for (var i = 0; i < OperationsPerWorkload; i++)
            {
                package.Bytecode.Instructions.Add(ConstI32(i));
                package.Bytecode.Instructions.Add(new RsfwInstruction
                {
                    Opcode = RsfwOpcode.GlobalStore,
                    Operands = new byte[] { 0, 0 }
                });
            }
            AddHalt(package);
            return package;
        }

        private static RsfwPackage CreateHalPackage()
        {
            var package = CreatePackage();
            for (var i = 0; i < OperationsPerWorkload; i++)
            {
                package.Bytecode.Instructions.Add(ConstI32(i & 1));
                package.Bytecode.Instructions.Add(DigitalWrite());
            }
            AddHalt(package);
            return package;
        }

        private static RsfwPackage CreatePackage()
        {
            return new RsfwPackage
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
        }

        private static void AddHalt(RsfwPackage package)
        {
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });
        }

        private static RsfwInstruction ConstI32(int value)
        {
            var bits = unchecked((uint)value);
            return new RsfwInstruction
            {
                Opcode = RsfwOpcode.ConstI32,
                Operands = new byte[]
                {
                    (byte)bits,
                    (byte)(bits >> 8),
                    (byte)(bits >> 16),
                    (byte)(bits >> 24)
                }
            };
        }

        private static RsfwInstruction DigitalWrite()
        {
            return new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { (byte)RsmaHalService.DigitalWrite, 0, 13, 1 }
            };
        }

        private sealed class CountingHal : IVirtualMcuHal
        {
            public void PinMode(byte pin, VirtualPinMode mode) { }

            public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
                VirtualPinOutputType outputType) { }

            public void DigitalWrite(byte pin, bool value) { }
            public bool DigitalRead(byte pin) => false;
        }
    }
}