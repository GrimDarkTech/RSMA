using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.PlayMode.Tests
{
    public sealed class VirtualBusPlayModeTests
    {
        [UnityTest]
        public IEnumerator RegisterLevelI2cAndSpiDevicesExposeObservableTransactions()
        {
            var bus = new VirtualBusBank();
            var i2c = new VirtualI2cDevice(0x48);
            i2c.SetRegister(0x10, 0x42);
            var spi = new VirtualSpiDevice(1, 3);
            spi.SetRegister(0x12, 0xA5);
            bus.RegisterI2cDevice(i2c);
            bus.RegisterSpiDevice(spi);

            var i2cRead = bus.I2cReadRegister(0x48, 0x10, 1, 400000, -1);
            var spiRead = bus.SpiReadRegister(1, 3, 0x12, 1, VirtualSpiMode.Mode3,
                VirtualBitOrder.MsbFirst, 2000000, -1);

            Assert.IsTrue(i2cRead.Success);
            Assert.IsTrue(spiRead.Success);
            CollectionAssert.AreEqual(new byte[] { 0x42 }, i2cRead.Data);
            CollectionAssert.AreEqual(new byte[] { 0xA5 }, spiRead.Data);
            Assert.Greater(bus.SimulatedTimeMilliseconds, 0.0);

            i2c.Acknowledge = false;
            Assert.IsFalse(bus.I2cReadRegister(0x48, 0x10, 1, 100000, -1).Success);
            Assert.IsNotEmpty(bus.BusDiagnostics);

            yield return null;
        }
    }
}