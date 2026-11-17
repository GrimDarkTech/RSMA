using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RSMA.Tests
{
    public sealed class SbcSceneWiringTests
    {
        [Test]
        public void SbcSceneContainsConfiguredMcuLedAndGpioBoundary()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestScenes/SBC.unity", OpenSceneMode.Additive);
            try
            {
                var mcu = FindComponent<RSMAVirtualMicrocontroller>(scene, "RSMAVirtualMicrocontroller");
                var gpioPort = FindComponent<RSMA.SBC.SbcGpioPort>(scene, "SbcGpioPort");
                var leds = Object.FindObjectsByType<RSMALED>(FindObjectsSortMode.None);
                Assert.IsNotNull(mcu);
                Assert.IsNotNull(gpioPort);
                Assert.IsNotNull(gpioPort.VirtualMcu);
                Assert.AreSame(mcu, gpioPort.VirtualMcu);
                Assert.IsNotNull(mcu.GetComponent<RSMAGPIO>());
                Assert.IsTrue(leds.Length > 0, "SBC scene must contain the test LED.");

                var led = System.Array.Find(leds, candidate => candidate.gameObject.name == "LED");
                Assert.IsNotNull(led);
                Assert.AreEqual(13, GetPrivateByte(led, "virtualPin"));
                Assert.AreSame(mcu, GetPrivateObject(led, "virtualMicrocontroller"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SbcSceneBlinkFirmwareDrivesPinAndLedAcrossSimulatedTime()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestScenes/SBC.unity", OpenSceneMode.Additive);
            try
            {
                var mcu = FindComponent<RSMAVirtualMicrocontroller>(scene, "RSMAVirtualMicrocontroller");
                var gpio = mcu.GetComponent<RSMAGPIO>();
                var led = FindSceneObject<RSMALED>(scene, "LED");
                Assert.IsNotNull(mcu);
                Assert.IsNotNull(gpio);
                Assert.IsNotNull(led);
                Assert.AreEqual("blink.rsfw", ((TextAsset)GetPrivateObject(mcu, "firmware")).name);

                if (GetPrivateObject(mcu, "virtualMachine") == null)
                    InvokePrivate(mcu, "OnEnable");
                Assert.IsTrue(mcu.enabled, "Virtual MCU failed to initialize from the scene firmware.");
                mcu.AdvanceSimulation(0);
                SetPrivateField(led, "ledLight", led.GetComponent<Light>());
                InvokePrivate(led, "Update");
                Assert.AreEqual(1f, gpio.GetPin("PC", "13").value, 0.0001f);
                Assert.AreEqual(1f, mcu.GetPinValue(13), 0.0001f);
                Assert.IsTrue(led.GetComponent<Light>().enabled);

                mcu.AdvanceSimulation(500);
                InvokePrivate(led, "Update");
                Assert.AreEqual(0f, gpio.GetPin("PC", "13").value, 0.0001f);
                Assert.AreEqual(0f, mcu.GetPinValue(13), 0.0001f);
                Assert.IsFalse(led.GetComponent<Light>().enabled);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindComponent<T>(UnityEngine.SceneManagement.Scene scene, string typeName)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static T FindSceneObject<T>(UnityEngine.SceneManagement.Scene scene, string objectName)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<T>(true))
                {
                    if (candidate.gameObject.name == objectName)
                        return candidate;
                }
            }
            return null;
        }

        private static byte GetPrivateByte(object target, string fieldName)
        {
            return (byte)GetPrivateObject(target, fieldName);
        }

        private static object GetPrivateObject(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing private method '" + methodName + "'");
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field '" + fieldName + "'");
            field.SetValue(target, value);
        }
    }
}