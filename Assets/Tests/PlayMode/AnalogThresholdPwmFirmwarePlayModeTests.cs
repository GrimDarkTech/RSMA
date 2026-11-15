using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class AnalogThresholdPwmFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator SourceBuiltAnalogSensorControlsPwmActuator()
        {
            var firmware = LoadFirmware("analog_threshold_pwm");
            Assert.IsNotNull(firmware, "Analog threshold/PWM RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "ADC");
            CollectionAssert.Contains(package.Manifest.RequiredPeripherals, "PWM");

            var gpio = new VirtualGpioBank();
            var sensor = new VirtualAnalogSensorModel(
                VirtualAnalogWaveform.Ramp,
                rampStart: 0.0,
                rampEnd: 1.0,
                periodMilliseconds: 20.0,
                samplePeriodMilliseconds: 10.0);
            gpio.RegisterAnalogSensor(10, sensor.Sample);
            var machine = new VirtualMachine(package, gpio);
            var trace = new System.Collections.Generic.List<VirtualTraceEvent>();
            machine.TraceRecorded += trace.Add;

            machine.Advance(0);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(10.0, machine.WakeTimeMilliseconds);

            machine.Advance(10);
            Assert.AreEqual(1f, gpio.GetOutputValue(13), 0.0001f);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.AreEqual(20.0, machine.WakeTimeMilliseconds);

            machine.Advance(490);
            Assert.AreEqual(0f, gpio.GetOutputValue(13), 0.0001f);
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            Assert.IsTrue(trace.Exists(item => item.Kind == VirtualTraceEventKind.AnalogSample &&
                item.Channel == 10 && item.Value == 0));
            Assert.IsTrue(trace.Exists(item => item.Kind == VirtualTraceEventKind.PwmOutput &&
                item.Channel == 13 && item.Value == 255));

            yield return null;
        }

        private static UnityEngine.TextAsset LoadFirmware(string assetName)
        {
            var assets = UnityEngine.Resources.LoadAll<UnityEngine.TextAsset>("RSMA/Firmware");
            foreach (var asset in assets)
            {
                if (asset.name == assetName || asset.name == assetName + ".rsfw")
                    return asset;
            }

            Assert.Fail("No firmware TextAsset named '" + assetName + "' was found");
            return null;
        }
    }
}