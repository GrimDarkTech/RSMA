using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Tests
{
    public sealed class PwmMotorDriverPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator EnterPlayMode()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator ExitPlayMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PwmFirmwareDrivesMotorDriverOutputThroughVirtualMcu()
        {
            var mcuObject = new GameObject("PWM motor test virtual MCU");
            var gpioObject = new GameObject("PWM motor test GPIO");
            var driverObject = new GameObject("PWM motor test driver");
            try
            {
                mcuObject.SetActive(false);
                var gpio = gpioObject.AddComponent<RSMAGPIO>();
                AddPin(gpio, "PC", "10");
                AddPin(gpio, "PC", "11");
                AddPin(gpio, "PC", "13");

                var mcu = mcuObject.AddComponent<RSMAVirtualMicrocontroller>();
                var firmwareField = typeof(RSMAVirtualMicrocontroller).GetField(
                    "firmware", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var gpioField = typeof(RSMAVirtualMicrocontroller).GetField(
                    "gpio", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var bindingsField = typeof(RSMAVirtualMicrocontroller).GetField(
                    "pinBindings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var firmware = LoadFirmware("pwm_motor_test");
                Assert.IsNotNull(firmware, "PWM motor RSFW TextAsset was not loaded from Resources");
                firmwareField.SetValue(mcu, firmware);
                gpioField.SetValue(mcu, gpio);
                bindingsField.SetValue(mcu, new System.Collections.Generic.List<VirtualPinBinding>
                {
                    Binding(10), Binding(11), Binding(13)
                });
                mcuObject.SetActive(true);

                var driver = driverObject.AddComponent<RSMAMotorDriver>();
                var driverMcuField = typeof(RSMAMotorDriver).GetField(
                    "virtualMicrocontroller", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var pin1Field = typeof(RSMAMotorDriver).GetField(
                    "virtualPin1", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var pin2Field = typeof(RSMAMotorDriver).GetField(
                    "virtualPin2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var pwmField = typeof(RSMAMotorDriver).GetField(
                    "virtualPwmPin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                driverMcuField.SetValue(driver, mcu);
                pin1Field.SetValue(driver, (byte)10);
                pin2Field.SetValue(driver, (byte)11);
                pwmField.SetValue(driver, (byte)13);

                AdvanceSimulation(mcu, 0);
                UpdateDriver(driver);

                Assert.AreEqual(VirtualExecutionState.Waiting, mcu.ExecutionState);
                Assert.AreEqual(0f, driver.GetOutput(), 0.0001f);

                AdvanceSimulation(mcu, 500);
                UpdateDriver(driver);
                Assert.AreEqual(64f / 255f, driver.GetOutput(), 0.0001f);

                AdvanceSimulation(mcu, 4000);
                UpdateDriver(driver);
                Assert.AreEqual(0f, driver.GetOutput(), 0.0001f);

                AdvanceSimulation(mcu, 500);
                UpdateDriver(driver);
                Assert.AreEqual(-64f / 255f, driver.GetOutput(), 0.0001f);
            }
            finally
            {
                Object.Destroy(mcuObject);
                Object.Destroy(gpioObject);
                Object.Destroy(driverObject);
            }

            yield return null;
        }

        private static VirtualPinBinding Binding(byte virtualPin)
        {
            return new VirtualPinBinding
            {
                virtualPin = virtualPin,
                gpioPin = new ConnectedPin { portName = "PC", pinName = virtualPin.ToString() },
                pullMode = PinPullModes.PullUp,
                outputMode = PinOutputModes.PushPull,
                resetMode = PinModes.Input
            };
        }

        private static TextAsset LoadFirmware(string assetName)
        {
            var assets = Resources.LoadAll<TextAsset>("RSMA/Firmware");
            foreach (var asset in assets)
            {
                if (asset.name == assetName || asset.name == assetName + ".rsfw")
                    return asset;
            }
            return null;
        }

        private static void AdvanceSimulation(RSMAVirtualMicrocontroller mcu, double milliseconds)
        {
            var machineField = typeof(RSMAVirtualMicrocontroller).GetField(
                "virtualMachine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var machine = machineField.GetValue(mcu);
            var advance = machine.GetType().GetMethod("Advance");
            advance.Invoke(machine, new object[] { milliseconds });
        }

        private static void UpdateDriver(RSMAMotorDriver driver)
        {
            var update = typeof(RSMAMotorDriver).GetMethod(
                "Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            update.Invoke(driver, null);
        }

        private static void AddPin(RSMAGPIO gpio, string portName, string pinName)
        {
            var port = gpio.GetPortList().Find(candidate => candidate.name == portName);
            if (port == null)
            {
                port = new GPIOPort { name = portName };
                gpio.GetPortList().Add(port);
            }
            port.pins.Add(new GPIOPin
            {
                name = pinName,
                pinMode = PinModes.Input,
                pinPullMode = PinPullModes.NoPullUpDown,
                pinOutputMode = PinOutputModes.PushPull,
                value = RSMAGPIO.Low,
                outputDefaulValue = RSMAGPIO.Low
            });
        }
    }
}