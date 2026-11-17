using System;
using NUnit.Framework;
using RSMA.Microcontroller.Virtual;
using RSMA.SBC;
using UnityEngine;

namespace RSMA.Tests
{
    public sealed class SbcGpioPortTests
    {
        [Test]
        public void SbcDriveReachesAttachedLineBackend()
        {
            var mcuObject = new GameObject("SBC GPIO MCU");
            var portObject = new GameObject("SBC GPIO port");
            try
            {
                var port = portObject.AddComponent<SbcGpioPort>();
                SetPrivateField(port, "pinBindings", new System.Collections.Generic.List<SbcGpioPinBinding>
                {
                    new SbcGpioPinBinding { logicalPin = 17, virtualMcuPin = 13 }
                });
                InvokePrivate(port, "RebuildBindingIndex");
                var backend = new FakeBackend();
                port.AttachBackend(backend);
                port.Write(17, true);
                Assert.IsTrue(backend.Drive);
                Assert.IsTrue(port.Read(17));
                port.Release(17);
                Assert.IsFalse(backend.HasDrive);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mcuObject);
                UnityEngine.Object.DestroyImmediate(portObject);
            }
        }

        private sealed class FakeBackend : ISbcGpioBackend
        {
            public bool Drive { get; private set; }
            public bool HasDrive { get; private set; }

            public void SetExternalDrive(byte virtualPin, bool level)
            {
                Drive = level;
                HasDrive = true;
            }

            public void ClearExternalDrive(byte virtualPin) => HasDrive = false;

            public VirtualGpioElectricalState GetElectricalState(byte virtualPin)
            {
                return new VirtualGpioElectricalState { VirtualPin = virtualPin, LineLevel = HasDrive && Drive };
            }
        }

        [Test]
        public void UnmappedSbcPinIsRejected()
        {
            var gameObject = new GameObject("SBC GPIO port");
            try
            {
                var port = gameObject.AddComponent<SbcGpioPort>();
                Assert.Throws<InvalidOperationException>(() => port.ConfigureInput(17));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SbcLogicalPinDrivesAndReadsVirtualMcuElectricalLine()
        {
            var gpioObject = new GameObject("SBC GPIO shared line");
            var portObject = new GameObject("SBC GPIO port");
            try
            {
                var gpio = gpioObject.AddComponent<RSMAGPIO>();
                var portList = gpio.GetPortList();
                var gpioPort = new GPIOPort { name = "PC" };
                gpioPort.pins.Add(new GPIOPin
                {
                    name = "13",
                    value = RSMAGPIO.Low,
                    pinMode = PinModes.Input,
                    pinPullMode = PinPullModes.NoPullUpDown,
                    pinOutputMode = PinOutputModes.PushPull,
                    outputDefaulValue = RSMAGPIO.Low
                });
                portList.Add(gpioPort);

                var profile = new VirtualBoardProfile("SBC-GPIO-TEST", new[]
                {
                    new VirtualPinDefinition(13, "PC", "13",
                        VirtualPinMode.Input, VirtualPinPullMode.None,
                        VirtualPinOutputType.PushPull, false,
                        VirtualPinCapabilities.BasicGpio | VirtualPinCapabilities.OpenDrain)
                });
                var adapter = new RSMAGpioHalAdapter(gpio, profile);
                var backend = new VirtualMcuBackend(profile, adapter);

                var port = portObject.AddComponent<SbcGpioPort>();
                SetPrivateField(port, "pinBindings", new System.Collections.Generic.List<SbcGpioPinBinding>
                {
                    new SbcGpioPinBinding { logicalPin = 17, virtualMcuPin = 13 }
                });
                InvokePrivate(port, "RebuildBindingIndex");
                port.AttachBackend(new BackendAdapter(backend));

                adapter.ConfigurePin(13, VirtualPinMode.Input, VirtualPinPullMode.None, VirtualPinOutputType.PushPull);

                port.Write(17, true);
                Assert.IsTrue(port.Read(17));
                Assert.IsTrue(backend.DigitalRead(13));

                port.Release(17);
                Assert.IsFalse(backend.DigitalRead(13));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gpioObject);
                UnityEngine.Object.DestroyImmediate(portObject);
            }
        }

        private sealed class BackendAdapter : ISbcGpioBackend
        {
            private readonly VirtualMcuBackend _backend;

            public BackendAdapter(VirtualMcuBackend backend) { _backend = backend; }
            public void SetExternalDrive(byte virtualPin, bool level) => _backend.SetExternalDrive(virtualPin, level);
            public void ClearExternalDrive(byte virtualPin) => _backend.ClearExternalDrive(virtualPin);
            public VirtualGpioElectricalState GetElectricalState(byte virtualPin) => _backend.GetElectricalState(virtualPin);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Invoke(target, null);
        }
    }
}