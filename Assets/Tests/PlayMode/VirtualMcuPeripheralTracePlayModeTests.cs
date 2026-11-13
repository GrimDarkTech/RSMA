using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class VirtualMcuPeripheralTracePlayModeTests
    {
        [UnityTest]
        public IEnumerator BlinkButtonInterruptPwmUartEventsAreDispatchedInOrder()
        {
            var package = CreatePeripheralTracePackage();
            var gpio = new VirtualGpioBank();
            var uart = new VirtualUartBank();
            var trace = new List<string>();
            var transmitted = new List<byte>();
            var machine = new VirtualMachine(package, gpio, null, uart);

            gpio.InputEdge += input => trace.Add("button-edge:" + input.Edge);
            gpio.OutputChanged += (pin, level) =>
            {
                if (pin == 10)
                    trace.Add("blink:" + level);
                else if (pin == 7)
                    trace.Add("pwm");
            };
            machine.InterruptDispatched += request => trace.Add("interrupt:" + request.VirtualPin);
            uart.Transmitted += (port, value) =>
            {
                transmitted.Add(value);
                trace.Add("uart:" + port + ":" + value);
            };

            machine.Advance(0);

            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsTrue(gpio.DigitalRead(10), "Blink output should be high before the button edge");
            Assert.IsTrue(gpio.DigitalRead(13), "Pull-up should leave the button line high");

            var button = new VirtualButton(gpio, 13);
            button.PressAt(10);
            machine.Advance(50);

            CollectionAssert.AreEqual(new[]
            {
                "blink:True",
                "button-edge:Falling",
                "interrupt:13",
                "pwm",
                "uart:0:90"
            }, trace);
            CollectionAssert.AreEqual(new byte[] { 0x5A }, transmitted);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(0, machine.InterruptDepth);
            Assert.AreEqual(50.0, machine.SimulatedTimeMilliseconds);
            Assert.AreEqual(192f / 255f, gpio.GetOutputValue(7), 0.0001f);

            yield return null;
        }

        private static RsfwPackage CreatePeripheralTracePackage()
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
            package.Manifest.RequiredPeripherals.Add("IRQ");
            package.Manifest.RequiredPeripherals.Add("PWM");
            package.Manifest.RequiredPeripherals.Add("UART");

            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.ConfigurePin, 10, 1));
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.DigitalWrite, 10, 1));
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.ConfigurePin, 13, 2));
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.ConfigurePin, 7, 1));
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.UartBegin, 0, 4));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.AttachIrq,
                Operands = new byte[] { 13, (byte)VirtualGpioEdge.Falling, 9, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.EnableIrq,
                Operands = System.Array.Empty<byte>()
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Wait,
                Operands = new byte[] { 0xE8, 0x03, 0, 0 }
            });
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.Jump,
                Operands = new byte[] { 7, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.PwmWrite, 7, 192));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ConstI32,
                Operands = new byte[] { 0x5A, 0, 0, 0 }
            });
            package.Bytecode.Instructions.Add(HalCall(RsmaHalService.UartWrite, 0, 0));
            package.Bytecode.Instructions.Add(new RsfwInstruction
            {
                Opcode = RsfwOpcode.ReturnIrq,
                Operands = System.Array.Empty<byte>()
            });
            return package;
        }

        private static RsfwInstruction HalCall(RsmaHalService service, byte pin, byte value)
        {
            return new RsfwInstruction
            {
                Opcode = RsfwOpcode.HalCall,
                Operands = new byte[] { (byte)service, 0, pin, value }
            };
        }
    }
}