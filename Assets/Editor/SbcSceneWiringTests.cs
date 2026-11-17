using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    }
}