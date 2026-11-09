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

                var inputAdapter = (IVirtualButtonHost)adapter;
                var irqMachine = new VirtualMachine(MakeIrqPackage(13), (IVirtualMcuHal)adapter);
                new VirtualButton(inputAdapter, 13).PressAt(10);
                irqMachine.Advance(50);
                Assert.AreEqual(VirtualExecutionState.Waiting, irqMachine.State);
                Assert.AreEqual(0, irqMachine.InterruptDepth);
                irqMachine.Advance(50);
                Assert.AreEqual(VirtualExecutionState.Halted, irqMachine.State);
                Assert.IsFalse((bool)adapterType.GetMethod("DigitalRead").Invoke(adapter, new object[] { (byte)13 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Stm32BoardProfileContainsTypedPc13Definition()
        {
            var profile = RsmaBoardProfiles.CreateStm32F103();
            var definition = profile.GetPin(13);

            Assert.AreEqual(RsmaBoardProfiles.Stm32F103BoardId, profile.BoardId);
            Assert.AreEqual("PC", definition.PortName);
            Assert.AreEqual("13", definition.PinName);
            Assert.AreEqual(VirtualPinMode.Input, definition.ResetMode);
            Assert.AreEqual(VirtualPinPullMode.None, definition.ResetPullMode);
            Assert.AreEqual(VirtualPinOutputType.PushPull, definition.OutputType);
            Assert.IsFalse(definition.ResetLatchLevel);
        }

        [Test]
        public void BoardProfileRejectsDuplicateVirtualAndPhysicalMappings()
        {
            Assert.Throws<BoardProfileException>(() => new VirtualBoardProfile(
                "test",
                new[]
                {
                    new VirtualPinDefinition(1, "PA", "1"),
                    new VirtualPinDefinition(1, "PA", "2")
                }));

            Assert.Throws<BoardProfileException>(() => new VirtualBoardProfile(
                "test",
                new[]
                {
                    new VirtualPinDefinition(1, "PA", "1"),
                    new VirtualPinDefinition(2, "PA", "1")
                }));
        }

        [Test]
        public void VirtualGpioPreservesTypedStateAndReportsMissingPin()
        {
            var gpio = new VirtualGpioBank();
            gpio.ConfigurePin(7, VirtualPinMode.Output, VirtualPinPullMode.PullDown,
                VirtualPinOutputType.OpenDrain);
            gpio.DigitalWrite(7, true);
            var state = gpio.GetState(7);

            Assert.AreEqual(VirtualPinMode.Output, state.Mode);
            Assert.AreEqual(VirtualPinPullMode.PullDown, state.PullMode);
            Assert.AreEqual(VirtualPinOutputType.OpenDrain, state.OutputType);
            Assert.IsTrue(state.LatchLevel);
            Assert.IsTrue(state.LineLevel);
            Assert.Throws<InvalidOperationException>(() => gpio.GetState(8));
        }

        [Test]
        public void VirtualButtonUsesActiveLowEdgesAndStableTimestampFifo()
        {
            var gpio = new VirtualGpioBank();
            gpio.ConfigurePin(7, VirtualPinMode.Input, VirtualPinPullMode.PullUp,
                VirtualPinOutputType.PushPull);
            Assert.IsTrue(gpio.DigitalRead(7));

            var events = new System.Collections.Generic.List<VirtualGpioInputEvent>();
            gpio.InputEdge += events.Add;
            var button = new VirtualButton(gpio, 7);
            button.PressAt(10);
            button.PressAt(10);
            button.ReleaseAt(20);
            gpio.AdvanceInputTo(20);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(VirtualGpioEdge.Falling, events[0].Edge);
            Assert.AreEqual(10.0, events[0].TimestampMilliseconds);
            Assert.AreEqual(VirtualGpioEdge.Rising, events[1].Edge);
            Assert.AreEqual(20.0, events[1].TimestampMilliseconds);
            Assert.IsTrue(gpio.DigitalRead(7));
        }

        [Test]
        public void VirtualInputEventsAreOrderedByTimestampThenInsertionSequence()
        {
            var gpio = new VirtualGpioBank();
            gpio.ConfigurePin(7, VirtualPinMode.Input, VirtualPinPullMode.None,
                VirtualPinOutputType.PushPull);
            var timestamps = new System.Collections.Generic.List<double>();
            gpio.InputEdge += input => timestamps.Add(input.TimestampMilliseconds);
            gpio.ScheduleInputLevel(7, 20, true);
            gpio.ScheduleInputLevel(7, 10, true);
            gpio.ScheduleInputLevel(7, 10, false);
            gpio.AdvanceInputTo(20);

            Assert.AreEqual(3, timestamps.Count);
            Assert.AreEqual(10.0, timestamps[0]);
            Assert.AreEqual(10.0, timestamps[1]);
            Assert.AreEqual(20.0, timestamps[2]);
        }

        [Test]
        public void VirtualInterruptControllerDispatchesDeferredFifoAndReturnsToWait()
        {
            var gpio = new VirtualGpioBank();
            gpio.ConfigurePin(7, VirtualPinMode.Input, VirtualPinPullMode.PullUp,
                VirtualPinOutputType.PushPull);
            var controller = new VirtualInterruptController(gpio);
            var package = MakeIrqPackage();
            var machine = new VirtualMachine(package, gpio, controller);
            new VirtualButton(gpio, 7).PressAt(10);

            machine.Advance(50);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(0, machine.InterruptDepth);
            machine.Advance(50);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
        }

        [Test]
        public void VirtualInterruptControllerRetainsEdgesWhileDisabled()
        {
            var gpio = new VirtualGpioBank();
            gpio.ConfigurePin(7, VirtualPinMode.Input, VirtualPinPullMode.PullUp,
                VirtualPinOutputType.PushPull);
            var controller = new VirtualInterruptController(gpio);
            controller.Attach(7, (byte)VirtualGpioEdge.Falling, 0);
            new VirtualButton(gpio, 7).PressAt(1);
            gpio.AdvanceInputTo(1);

            Assert.IsTrue(controller.HasPendingInterrupt);
            Assert.IsFalse(controller.TryDequeue(out _));
            controller.Enable();
            Assert.IsTrue(controller.TryDequeue(out var request));
            Assert.AreEqual(VirtualGpioEdge.Falling, request.Edge);
            Assert.AreEqual(1.0, request.TimestampMilliseconds);
        }

        [Test]
        public void VirtualMachineExecutesPackedConfigurePinHalCall()
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
                // mode=Output, pull=PullDown, output type=OpenDrain
                Operands = new byte[] { 6, 0, 7, 13 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 2, 0, 7, 1 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            machine.Advance(0);

            var state = gpio.GetState(7);
            Assert.AreEqual(VirtualPinMode.Output, state.Mode);
            Assert.AreEqual(VirtualPinPullMode.PullDown, state.PullMode);
            Assert.AreEqual(VirtualPinOutputType.OpenDrain, state.OutputType);
            Assert.IsTrue(state.LatchLevel);
            Assert.IsTrue(state.LineLevel);
            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
        }

        [Test]
        public void VirtualMachinePwmHalCallPreservesNormalizedDutyCycle()
        {
            var package = MakeBlinkPackage();
            package.Bytecode.Instructions.Clear();
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 1, 0, 7, 1 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 7, 0, 7, 64 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Halted, machine.State);
            Assert.AreEqual(64f / 255f, gpio.GetOutputValue(7), 0.0001f);
        }

        [TestCase(0, 0f, false)]
        [TestCase(128, 128f / 255f, true)]
        [TestCase(255, 1f, true)]
        public void VirtualMachinePwmHalCallHandlesDutyCycleBoundaries(
            byte dutyCycle, float expectedValue, bool expectedDigitalLevel)
        {
            var package = MakeBlinkPackage();
            package.Bytecode.Instructions.Clear();
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 1, 0, 7, 1 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 7, 0, 7, dutyCycle }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(package, gpio);
            machine.Advance(0);

            Assert.AreEqual(expectedValue, gpio.GetOutputValue(7), 0.0001f);
            Assert.AreEqual(expectedDigitalLevel, gpio.DigitalRead(7));
        }

        [Test]
        public void VirtualMachineRejectsPwmWriteToInputPin()
        {
            var package = MakeBlinkPackage();
            package.Bytecode.Instructions.Clear();
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 1, 0, 7, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 7, 0, 7, 128 }
            });

            var machine = new VirtualMachine(package, new VirtualGpioBank());
            Assert.Throws<VirtualMachineFaultException>(() => machine.Advance(0));
        }

        [Test]
        public void PwmMotorTestFixtureRampsAndReversesConfiguredPins()
        {
            var firmware = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/RSMA/Firmware/pwm_motor_test.rsfw.bytes");
            Assert.IsNotNull(firmware, "PWM motor test firmware was not imported as TextAsset");

            var gpio = new VirtualGpioBank();
            var machine = new VirtualMachine(RsfwReader.Read(firmware.bytes), gpio);
            machine.Advance(0);

            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(10).Mode);
            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(11).Mode);
            Assert.AreEqual(VirtualPinMode.Output, gpio.GetState(13).Mode);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);

            machine.Advance(500);
            Assert.AreEqual(64f / 255f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsTrue(gpio.DigitalRead(10));
            Assert.IsFalse(gpio.DigitalRead(11));

            machine.Advance(4500);
            Assert.AreEqual(64f / 255f, gpio.GetOutputValue(13), 0.0001f);
            Assert.IsFalse(gpio.DigitalRead(10));
            Assert.IsTrue(gpio.DigitalRead(11));
        }

        [Test]
        public void GpioAdapterWritesPwmDutyAsNormalizedLegacyPinValue()
        {
            var host = new GameObject("RSMAGPIO PWM adapter test");
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
                pins.Add(CreateLegacyPin(pinType, "7", 0f));
                ports.Add(port);

                var bindingType = Type.GetType("VirtualPinBinding, Assembly-CSharp", true);
                var binding = Activator.CreateInstance(bindingType);
                bindingType.GetField("virtualPin").SetValue(binding, (byte)7);
                var connectedPin = Activator.CreateInstance(bindingType.GetField("gpioPin").FieldType);
                connectedPin.GetType().GetField("portName").SetValue(connectedPin, "PC");
                connectedPin.GetType().GetField("pinName").SetValue(connectedPin, "7");
                bindingType.GetField("gpioPin").SetValue(binding, connectedPin);
                bindingType.GetField("resetMode").SetValue(binding,
                    Enum.ToObject(bindingType.GetField("resetMode").FieldType, 0));
                var bindingArray = Array.CreateInstance(bindingType, 1);
                bindingArray.SetValue(binding, 0);

                var adapter = adapterType.GetConstructor(new[] { gpioType, typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(bindingType) })
                    .Invoke(new object[] { gpio, bindingArray });
                adapterType.GetMethod("PwmWrite").Invoke(adapter, new object[] { (byte)7, (byte)64 });
                var dutyValue = adapterType.GetMethod("ReadPinValue").Invoke(adapter, new object[] { (byte)7 });

                Assert.AreEqual(64f / 255f, (float)dutyValue, 0.0001f);
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

        private static RsfwPackage MakeIrqPackage(byte pin = 7)
        {
            var package = new RsfwPackage
            {
                Manifest = new RsfwManifest
                {
                    BoardId = "test",
                    FlashSize = 1,
                    RamSize = 1,
                    EntryPointName = "reset"
                },
                Bytecode = new RsfwBytecode { EntryPoint = 0 }
            };
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { 6, 0, pin, 2 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.AttachIrq,
                Operands = new byte[] { pin, 2, 5, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.EnableIrq,
                Operands = Array.Empty<byte>()
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Wait,
                Operands = new byte[] { 100, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Jump,
                Operands = new byte[] { 8, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ConstI32,
                Operands = new byte[] { 42, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Store,
                Operands = new byte[] { 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ReturnIrq,
                Operands = Array.Empty<byte>()
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Halt,
                Operands = Array.Empty<byte>()
            });
            return package;
        }
    }
}
