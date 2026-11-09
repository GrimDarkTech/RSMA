using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class UartEchoFirmwarePlayModeTests
    {
        [UnityTest]
        public IEnumerator UartEchoFirmwareReceivesAndTransmitsBytes()
        {
            var firmware = LoadFirmware("uart_echo");
            Assert.IsNotNull(firmware, "UART echo RSFW TextAsset was not loaded from Resources");

            var package = RsfwReader.Read(firmware.bytes);
            Assert.AreEqual("RSMA-STM32F103", package.Manifest.BoardId);
            Assert.Contains("UART", package.Manifest.RequiredPeripherals);

            var uart = new VirtualUartBank();
            var transmitted = new System.Collections.Generic.List<byte>();
            uart.Transmitted += (_, value) => transmitted.Add(value);
            var machine = new VirtualMachine(package, new VirtualGpioBank(), null, uart);
            machine.Advance(0);

            Assert.AreEqual(115200, uart.GetBaudRate(0));
            Assert.AreEqual(0, uart.Available(0));

            uart.InjectReceived(0, new byte[] { 0x5A });
            machine.Advance(1);

            CollectionAssert.AreEqual(new byte[] { 0x5A }, transmitted);
            Assert.AreEqual(0, uart.Available(0));
            Assert.AreEqual(VirtualExecutionState.Waiting, machine.State);
            yield return null;
        }

        private static TextAsset LoadFirmware(string assetName)
        {
            var assets = Resources.LoadAll<TextAsset>("RSMA/Firmware");
            foreach (var asset in assets)
            {
                if (asset.name == assetName || asset.name == assetName + ".rsfw")
                    return asset;
            }

            var importedNames = new System.Text.StringBuilder();
            foreach (var asset in assets)
            {
                if (importedNames.Length != 0)
                    importedNames.Append(", ");
                importedNames.Append(asset.name);
            }
            Assert.Fail("No firmware TextAsset named '" + assetName + "' was found. Imported resources: " + importedNames);
            return null;
        }
    }
}