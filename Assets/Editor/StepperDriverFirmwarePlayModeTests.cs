using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Tests
{
    public sealed class StepperDriverFirmwarePlayModeTests
    {
        [Test]
        public void GpioWritePinOverloadsOnlyWriteOutputPins()
        {
            var gpioObject = new GameObject("GPIO WritePin test");
            try
            {
                var gpio = gpioObject.AddComponent<RSMAGPIO>();
                AddPin(gpio, "PA", "0");
                AddPin(gpio, "PA", "1");
                gpio.GetPin("PA", "1").pinMode = PinModes.Output;

                LogAssert.Expect(LogType.Warning, "GPIO GPIO WritePin test can't set pin state, 0 mode is Input");
                gpio.WritePin("PA", "0", 0.75f);
                LogAssert.Expect(LogType.Warning, "GPIO GPIO WritePin test can't set pin state, 0 mode is Input");
                gpio.WritePin(0, "0", 0.75f);
                LogAssert.Expect(LogType.Warning, "GPIO GPIO WritePin test can't set pin state, 0 mode is Input");
                gpio.WritePin("PA", 0, 0.75f);
                LogAssert.Expect(LogType.Warning, "GPIO GPIO WritePin test can't set pin state, 0 mode is Input");
                gpio.WritePin(0, 0, 0.75f);

                Assert.AreEqual(RSMAGPIO.Low, gpio.GetPin("PA", "0").value);

                gpio.WritePin("PA", "1", 0.25f);
                gpio.WritePin(0, "1", 0.5f);
                gpio.WritePin("PA", 1, 0.75f);
                gpio.WritePin(0, 1, 1f);

                Assert.AreEqual(1f, gpio.GetPin("PA", "1").value);
                Assert.DoesNotThrow(() => gpio.WritePin(-1, 0, 1f));
                Assert.DoesNotThrow(() => gpio.WritePin(0, -1, 1f));
                Assert.DoesNotThrow(() => gpio.WritePin("PA", -1, 1f));
                Assert.DoesNotThrow(() => gpio.WritePin(0, "missing", 1f));
            }
            finally
            {
                Object.DestroyImmediate(gpioObject);
            }
        }

        [Test]
        public void L298nStepperPrefabKeepsLegacyFourChannelWiring()
        {
            var prefab = LoadPrefab("Assets/Prefabs/Drivers/L298n-Stepper.prefab");
            var driver = prefab.GetComponentInChildren<StepperDriver>(true);

            Assert.IsNotNull(driver, "L298n-Stepper prefab must contain a StepperDriver");
            AssertPin(driver.channel1Pin, "PB", "13");
            AssertPin(driver.channel2Pin, "PB", "14");
            AssertPin(driver.channel3Pin, "PA", "10");
            AssertPin(driver.channel4Pin, "PA", "11");
        }

        [Test]
        public void StepperMotorPrefabKeepsPhysicalMotorConfiguration()
        {
            var prefab = LoadPrefab("Assets/Prefabs/Motors/us17hs4401s.prefab");
            var motor = prefab.GetComponent<StepperMotor>();

            Assert.IsNotNull(motor, "us17hs4401s prefab must contain a StepperMotor");
            Assert.AreEqual(1.8f, motor.stepAngle, 0.0001f);
            Assert.AreEqual(Vector3.forward, motor.motorAxis);
            Assert.IsNotNull(prefab.GetComponent<Rigidbody>(),
                "Stepper motor prefab must keep its required Rigidbody");
        }

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
            var previousSimulationMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                mcuObject.SetActive(false);
                var gpio = gpioObject.AddComponent<RSMAGPIO>();
                AddPin(gpio, "PB", "13");
                AddPin(gpio, "PB", "14");
                AddPin(gpio, "PA", "10");
                AddPin(gpio, "PA", "11");

                var mcu = mcuObject.AddComponent<RSMAVirtualMicrocontroller>();
                SetPrivateField(mcu, "firmware", LoadFirmware("stepper_motor_test"));
                SetPrivateField(mcu, "gpio", gpio);
                SetPrivateField(mcu, "pinBindings", new List<VirtualPinBinding>
                {
                    Binding(10, "PB", "13"),
                    Binding(11, "PB", "14"),
                    Binding(12, "PA", "10"),
                    Binding(13, "PA", "11")
                });
                mcuObject.SetActive(true);

                var driver = driverObject.AddComponent<StepperDriver>();
                driver.channel1Pin = new ConnectedPin { portName = "PB", pinName = "13" };
                driver.channel2Pin = new ConnectedPin { portName = "PB", pinName = "14" };
                driver.channel3Pin = new ConnectedPin { portName = "PA", pinName = "10" };
                driver.channel4Pin = new ConnectedPin { portName = "PA", pinName = "11" };
                driver.connectMicrocontroller = gpio;
                SetPrivateField(driver, "virtualMicrocontroller", mcu);
                SetPrivateField(driver, "virtualChannel1Pin", (byte)10);
                SetPrivateField(driver, "virtualChannel2Pin", (byte)11);
                SetPrivateField(driver, "virtualChannel3Pin", (byte)12);
                SetPrivateField(driver, "virtualChannel4Pin", (byte)13);

                var motorBody = motorObject.AddComponent<Rigidbody>();
                motorBody.useGravity = false;
                motorBody.constraints = RigidbodyConstraints.FreezePosition |
                                       RigidbodyConstraints.FreezeRotationX |
                                       RigidbodyConstraints.FreezeRotationY;
                var rotorBody = rotorObject.AddComponent<Rigidbody>();
                rotorBody.isKinematic = true;
                var motor = motorObject.AddComponent<StepperMotor>();
                motor.connectedBody = rotorObject;
                motor.motorDriver = driver;
                motor.motorAxis = Vector3.forward;
                motor.stepAngle = 1.8f;
                motor.torque = 500f;
                motor.holdingTorque = 5f;
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
                AssertGpio(gpio, "PB", "13", 1f);
                AssertGpio(gpio, "PB", "14", 1f);
                AssertGpio(gpio, "PA", "10", 0f);
                AssertGpio(gpio, "PA", "11", 0f);
                AdvanceAndAssert(mcu, driver, motor, 100, 2, 3.6f);
                AdvanceAndAssert(mcu, driver, motor, 100, 3, 5.4f);
                AdvanceAndAssert(mcu, driver, motor, 100, 4, 7.2f);
                AdvanceAndAssert(mcu, driver, motor, 100, 0, 7.2f);
                Assert.Greater(Mathf.Abs(motor.GetComponent<HingeJoint>().angle), 0.1f,
                    "Stepper phases should produce physical rotation, not only a new spring target");

                AdvanceAndAssert(mcu, driver, motor, 100, 4, 7.2f);
                AdvanceAndAssert(mcu, driver, motor, 100, 3, 5.4f);
                AdvanceAndAssert(mcu, driver, motor, 100, 2, 3.6f);
                AdvanceAndAssert(mcu, driver, motor, 100, 1, 1.8f);
                mcuObject.SetActive(false);
                UpdateDriver(driver);
                Assert.AreEqual(0, driver.output, "Disabling the MCU should clear the active stepper phase");
                AssertAllStepperPinsLow(gpio);
            }
            finally
            {
                Object.Destroy(mcuObject);
                Object.Destroy(gpioObject);
                Object.Destroy(driverObject);
                Object.Destroy(motorObject);
                Object.Destroy(rotorObject);
                Physics.simulationMode = previousSimulationMode;
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
            for (var index = 0; index < 10; index++)
                Physics.Simulate(Time.fixedDeltaTime);
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

        private static VirtualPinBinding Binding(byte virtualPin, string portName, string pinName)
        {
            return new VirtualPinBinding
            {
                virtualPin = virtualPin,
                gpioPin = new ConnectedPin { portName = portName, pinName = pinName },
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

        private static void AssertGpio(RSMAGPIO gpio, string portName, string pinName, float expected)
        {
            Assert.AreEqual(expected, gpio.GetPin(portName, pinName).value, 0.0001f,
                "Unexpected GPIO level at " + portName + pinName);
        }

        private static void AssertAllStepperPinsLow(RSMAGPIO gpio)
        {
            AssertGpio(gpio, "PB", "13", RSMAGPIO.Low);
            AssertGpio(gpio, "PB", "14", RSMAGPIO.Low);
            AssertGpio(gpio, "PA", "10", RSMAGPIO.Low);
            AssertGpio(gpio, "PA", "11", RSMAGPIO.Low);
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

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "Prefab was not found: " + path);
            return prefab;
        }

        private static void AssertPin(ConnectedPin actual, string expectedPort, string expectedPin)
        {
            Assert.AreEqual(expectedPort, actual.portName);
            Assert.AreEqual(expectedPin, actual.pinName);
        }
    }
}