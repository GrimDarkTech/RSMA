using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Tests
{
    public sealed class StepperDriverFirmwarePlayModeTests
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
        public IEnumerator StepperFirmwareDrivesFourPhaseDriverSequence()
        {
            var mcuObject = new GameObject("Stepper test virtual MCU");
            var gpioObject = new GameObject("Stepper test GPIO");
            var driverObject = new GameObject("Stepper test driver");
            var motorObject = new GameObject("Stepper test motor");
            var rotorObject = new GameObject("Stepper test rotor");
            try
            {
                mcuObject.SetActive(false);
                var gpio = gpioObject.AddComponent<RSMAGPIO>();
                AddPin(gpio, "PC", "10");
                AddPin(gpio, "PC", "11");
                AddPin(gpio, "PC", "12");
                AddPin(gpio, "PC", "13");

                var mcu = mcuObject.AddComponent<RSMAVirtualMicrocontroller>();
                SetPrivateField(mcu, "firmware", LoadFirmware("stepper_motor_test"));
                SetPrivateField(mcu, "gpio", gpio);
                SetPrivateField(mcu, "pinBindings", new List<VirtualPinBinding>
                {
                    Binding(10), Binding(11), Binding(12), Binding(13)
                });
                mcuObject.SetActive(true);

                var driver = driverObject.AddComponent<StepperDriver>();
                SetPrivateField(driver, "virtualMicrocontroller", mcu);
                SetPrivateField(driver, "virtualChannel1Pin", (byte)10);
                SetPrivateField(driver, "virtualChannel2Pin", (byte)11);
                SetPrivateField(driver, "virtualChannel3Pin", (byte)12);
                SetPrivateField(driver, "virtualChannel4Pin", (byte)13);

                motorObject.AddComponent<Rigidbody>();
                rotorObject.AddComponent<Rigidbody>();
                var motor = motorObject.AddComponent<StepperMotor>();
                motor.connectedBody = rotorObject;
                motor.motorDriver = driver;
                motor.motorAxis = Vector3.forward;
                motor.stepAngle = 1.8f;
                typeof(StepperMotor).GetMethod(
                    "Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(motor, null);

                AdvanceSimulation(mcu, 0);
                UpdateDriver(driver);
                UpdateMotor(motor);
                Assert.AreEqual(VirtualExecutionState.Waiting, mcu.ExecutionState);
                Assert.AreEqual(0, driver.output);
                Assert.AreEqual(0f, motor.GetComponent<HingeJoint>().spring.targetPosition, 0.0001f);

                AdvanceAndAssert(mcu, driver, motor, 100, 1, 1.8f);
                AdvanceAndAssert(mcu, driver, motor, 100, 2, 3.6f);
                AdvanceAndAssert(mcu, driver, motor, 100, 3, 5.4f);
                AdvanceAndAssert(mcu, driver, motor, 100, 4, 7.2f);
                AdvanceAndAssert(mcu, driver, motor, 100, 0, 7.2f);

                AdvanceAndAssert(mcu, driver, motor, 100, 4, 7.2f);
                AdvanceAndAssert(mcu, driver, motor, 100, 3, 5.4f);
                AdvanceAndAssert(mcu, driver, motor, 100, 2, 3.6f);
                AdvanceAndAssert(mcu, driver, motor, 100, 1, 1.8f);
                AdvanceAndAssert(mcu, driver, motor, 100, 0, 1.8f);
            }
            finally
            {
                Object.Destroy(mcuObject);
                Object.Destroy(gpioObject);
                Object.Destroy(driverObject);
                Object.Destroy(motorObject);
                Object.Destroy(rotorObject);
            }

            yield return null;
        }

        private static void AdvanceAndAssert(
            RSMAVirtualMicrocontroller mcu,
            StepperDriver driver,
            StepperMotor motor,
            double milliseconds,
            int expectedOutput,
            float expectedTargetPosition)
        {
            AdvanceSimulation(mcu, milliseconds);
            UpdateDriver(driver);
            UpdateMotor(motor);
            Assert.AreEqual(expectedOutput, driver.output);
            Assert.AreEqual(expectedTargetPosition,
                motor.GetComponent<HingeJoint>().spring.targetPosition, 0.0001f);
        }

        private static void AdvanceSimulation(RSMAVirtualMicrocontroller mcu, double milliseconds)
        {
            var machine = GetPrivateField<object>(mcu, "virtualMachine");
            machine.GetType().GetMethod("Advance").Invoke(machine, new object[] { milliseconds });
        }

        private static void UpdateDriver(StepperDriver driver)
        {
            typeof(StepperDriver).GetMethod(
                "Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(driver, null);
        }

        private static void UpdateMotor(StepperMotor motor)
        {
            typeof(StepperMotor).GetMethod(
                "Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(motor, null);
        }

        private static VirtualPinBinding Binding(byte virtualPin)
        {
            return new VirtualPinBinding
            {
                virtualPin = virtualPin,
                gpioPin = new ConnectedPin { portName = "PC", pinName = virtualPin.ToString() },
                pullMode = PinPullModes.NoPullUpDown,
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
            Assert.Fail("Stepper firmware was not loaded from Resources: " + assetName);
            return null;
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(
                fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}