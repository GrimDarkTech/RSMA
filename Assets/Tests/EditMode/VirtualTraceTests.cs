using System;
using System.Collections.Generic;
using NUnit.Framework;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.Tests
{
    public sealed class VirtualTraceTests
    {
        [Test]
        public void TraceBufferIsBoundedAndKeepsMonotonicSequence()
        {
            var trace = new VirtualTraceBuffer(2);
            trace.Record(VirtualTraceEventKind.TimeAdvanced, 0);
            trace.Record(VirtualTraceEventKind.AnalogSample, 1, 4, 100);
            trace.Record(VirtualTraceEventKind.PwmOutput, 2, 13, 255);

            Assert.AreEqual(2, trace.Count);
            Assert.AreEqual(1, trace.DroppedEventCount);
            Assert.AreEqual(1UL, trace[0].Sequence);
            Assert.AreEqual(2UL, trace[1].Sequence);
            Assert.AreEqual(VirtualTraceEventKind.AnalogSample, trace[0].Kind);
        }

        [Test]
        public void VirtualMachineTraceContainsAnalogDecisionAndPwmOutput()
        {
            var package = MakeAnalogPwmPackage();
            var gpio = new VirtualGpioBank();
            gpio.SetAnalogInput(4, 2048);
            var machine = new VirtualMachine(package, gpio);
            var observed = new List<VirtualTraceEvent>();
            machine.TraceRecorded += observed.Add;

            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            Assert.AreEqual(1f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsTrue(observed.Count >= 3);
            Assert.AreEqual(VirtualTraceEventKind.AnalogSample,
                observed.Find(trace => trace.Kind == VirtualTraceEventKind.AnalogSample).Kind);
            Assert.AreEqual(2048, observed.Find(trace => trace.Kind == VirtualTraceEventKind.AnalogSample).Value);
            Assert.AreEqual(255, observed.Find(trace => trace.Kind == VirtualTraceEventKind.PwmOutput).Value);
            Assert.IsNotNull(observed.Find(trace => trace.Kind == VirtualTraceEventKind.HalCall &&
                trace.AuxiliaryValue == (int)RsmaHalService.AnalogRead));
            Assert.IsNotNull(observed.Find(trace => trace.Kind == VirtualTraceEventKind.StateTransition &&
                trace.AuxiliaryValue == (int)VirtualExecutionState.Halted));

            for (var i = 1; i < observed.Count; i++)
            {
                Assert.Greater(observed[i].Sequence, observed[i - 1].Sequence);
                Assert.GreaterOrEqual(observed[i].TimestampMilliseconds,
                    observed[i - 1].TimestampMilliseconds);
            }
        }

        [Test]
        public void VirtualMachineFaultIsRecordedAndTraceCanBeCleared()
        {
            var package = new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "test",
                    FlashSize = 1,
                    RamSize = 1,
                    EntryPointName = "main"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = (RsfwOpcode)0xFF,
                Operands = Array.Empty<byte>()
            });

            var machine = new VirtualMachine(package, new VirtualGpioBank());
            Assert.Throws<VirtualMachineFaultException>(() => machine.Advance(0));
            Assert.AreEqual(VirtualExecutionState.Faulted, machine.State);
            Assert.IsNotNull(machine.GetTraceSnapshot().Find(trace => trace.Kind == VirtualTraceEventKind.Fault));

            machine.ClearTrace();
            Assert.AreEqual(0, machine.Trace.Count);
        }

        [Test]
        public void InvalidAdcMappingAndPwmDutyProduceFaultTrace()
        {
            var adcPackage = MakeAnalogReadPackage(99);
            var adcMachine = new VirtualMachine(adcPackage, new StrictAdcHal());
            Assert.Throws<VirtualMachineFaultException>(() => adcMachine.Advance(0));
            Assert.IsNotNull(adcMachine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.HalCall &&
                trace.AuxiliaryValue == (int)RsmaHalService.AnalogRead));
            Assert.IsNotNull(adcMachine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.Fault));
            Assert.IsNotNull(adcMachine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.RejectedMapping));

            var pwmMachine = new VirtualMachine(MakeDynamicPwmPackage(256), new VirtualGpioBank());
            Assert.Throws<VirtualMachineFaultException>(() => pwmMachine.Advance(0));
            Assert.IsNotNull(pwmMachine.GetTraceSnapshot().Find(trace =>
                trace.Kind == VirtualTraceEventKind.Fault &&
                trace.Message.Contains("PWM duty")));
        }

        private static RsfwPackage MakeAnalogPwmPackage()
        {
            var package = new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "test",
                    FlashSize = 1,
                    RamSize = 1,
                    EntryPointName = "main"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
            package.Manifest.RequiredPeripherals.Add("GPIO");
            package.Manifest.RequiredPeripherals.Add("ADC");
            package.Manifest.RequiredPeripherals.Add("PWM");
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.PinMode, 13, 1));
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.AnalogRead, 4, 0));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ConstI32,
                Operands = new byte[] { 255, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { (byte)RsmaHalService.AnalogWrite, 0, 13, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });
            return package;
        }

        private static RsfwPackage MakeAnalogReadPackage(byte channel)
        {
            var package = MakePackage();
            package.Manifest.RequiredPeripherals.Add("ADC");
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.AnalogRead, channel, 0));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });
            return package;
        }

        private static RsfwPackage MakeDynamicPwmPackage(int dutyCycle)
        {
            var package = MakePackage();
            package.Manifest.RequiredPeripherals.Add("GPIO");
            package.Manifest.RequiredPeripherals.Add("PWM");
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.PinMode, 13, 1));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ConstI32,
                Operands = BitConverter.GetBytes(dutyCycle)
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { (byte)RsmaHalService.AnalogWrite, 0, 13, 0 }
            });
            return package;
        }

        private static RsfwPackage MakePackage()
        {
            return new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "test",
                    FlashSize = 1,
                    RamSize = 1,
                    EntryPointName = "main"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
        }

        private static RsfwInstruction HalCall(RsmaHalService service, byte channel, byte value)
        {
            return new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { (byte)service, 0, channel, value }
            };
        }

        private sealed class StrictAdcHal : IVirtualMcuHal, IVirtualAdcHal
        {
            public void PinMode(byte pin, VirtualPinMode mode) { }
            public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
                VirtualPinOutputType outputType) { }
            public void DigitalWrite(byte pin, bool value) { }
            public bool DigitalRead(byte pin) { return false; }

            public ushort AnalogRead(byte channel)
            {
                throw new InvalidOperationException("ADC channel is not mapped: " + channel);
            }
        }
    }
}