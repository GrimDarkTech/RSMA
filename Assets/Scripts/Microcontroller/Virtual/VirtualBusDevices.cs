using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum VirtualSpiMode : byte
    {
        Mode0 = 0,
        Mode1 = 1,
        Mode2 = 2,
        Mode3 = 3
    }

    public enum VirtualBitOrder : byte
    {
        MsbFirst = 0,
        LsbFirst = 1
    }

    public sealed class VirtualBusDiagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public string Bus { get; }
        public byte Route { get; }
        public double TimestampMilliseconds { get; }

        public VirtualBusDiagnostic(string code, string message, string bus, byte route,
            double timestampMilliseconds)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Route = route;
            TimestampMilliseconds = timestampMilliseconds;
        }
    }

    public sealed class VirtualBusTransactionResult
    {
        public bool Success { get; }
        public bool Acknowledged { get; }
        public bool TimedOut { get; }
        public byte[] Data { get; }
        public string Error { get; }
        public double DurationMilliseconds { get; }

        private VirtualBusTransactionResult(bool success, bool acknowledged, bool timedOut,
            byte[] data, string error, double durationMilliseconds)
        {
            Success = success;
            Acknowledged = acknowledged;
            TimedOut = timedOut;
            Data = data ?? Array.Empty<byte>();
            Error = error;
            DurationMilliseconds = durationMilliseconds;
        }

        public static VirtualBusTransactionResult Ok(byte[] data, double durationMilliseconds)
        {
            return new VirtualBusTransactionResult(true, true, false,
                (byte[])data.Clone(), null, durationMilliseconds);
        }

        public static VirtualBusTransactionResult Nack(string error, double durationMilliseconds)
        {
            return new VirtualBusTransactionResult(false, false, false,
                Array.Empty<byte>(), error, durationMilliseconds);
        }

        public static VirtualBusTransactionResult Timeout(string error, double durationMilliseconds)
        {
            return new VirtualBusTransactionResult(false, false, true,
                Array.Empty<byte>(), error, durationMilliseconds);
        }
    }

    public sealed class VirtualI2cDevice
    {
        private readonly byte[] registers = new byte[256];
        private readonly byte[] resetRegisters = new byte[256];

        public VirtualI2cDevice(byte address)
        {
            if (address > 0x7F)
                throw new ArgumentOutOfRangeException(nameof(address), "I2C address must be 7-bit");
            Address = address;
        }

        public byte Address { get; }
        public bool Present { get; set; } = true;
        public bool Acknowledge { get; set; } = true;
        public double ClockStretchMilliseconds { get; set; }

        public void SetRegister(byte address, byte value)
        {
            registers[address] = value;
            resetRegisters[address] = value;
        }

        public byte GetRegister(byte address) => registers[address];

        internal byte[] Read(byte registerAddress, int length)
        {
            if (length < 0 || length > 256)
                throw new ArgumentOutOfRangeException(nameof(length));
            var data = new byte[length];
            for (var i = 0; i < length; i++)
                data[i] = registers[(registerAddress + i) & 0xFF];
            return data;
        }

        internal void Write(byte registerAddress, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length > 256)
                throw new ArgumentOutOfRangeException(nameof(data));
            for (var i = 0; i < data.Length; i++)
                registers[(registerAddress + i) & 0xFF] = data[i];
        }

        internal void Reset()
        {
            Array.Copy(resetRegisters, registers, registers.Length);
        }
    }

    public sealed class VirtualSpiDevice
    {
        private readonly byte[] registers = new byte[128];
        private readonly byte[] resetRegisters = new byte[128];

        public VirtualSpiDevice(byte bus, byte chipSelect)
        {
            Bus = bus;
            ChipSelect = chipSelect;
        }

        public byte Bus { get; }
        public byte ChipSelect { get; }
        public bool Present { get; set; } = true;
        public bool Acknowledge { get; set; } = true;

        public void SetRegister(byte address, byte value)
        {
            if (address >= registers.Length)
                throw new ArgumentOutOfRangeException(nameof(address), "SPI register address must be 0..127");
            registers[address] = value;
            resetRegisters[address] = value;
        }

        public byte GetRegister(byte address) => registers[address & 0x7F];

        internal byte[] Transfer(byte[] transmit, VirtualSpiMode mode, VirtualBitOrder bitOrder)
        {
            if (transmit == null || transmit.Length == 0)
                throw new ArgumentException("SPI transaction must contain at least one byte", nameof(transmit));
            var receive = new byte[transmit.Length];
            receive[0] = 0;
            var command = transmit[0];
            var read = (command & 0x80) != 0;
            var address = (byte)(command & 0x7F);
            for (var i = 1; i < transmit.Length; i++)
            {
                if (read)
                    receive[i] = registers[(address + i - 1) & 0x7F];
                else
                {
                    registers[(address + i - 1) & 0x7F] = transmit[i];
                    receive[i] = 0;
                }
                if (bitOrder == VirtualBitOrder.LsbFirst)
                    receive[i] = ReverseBits(receive[i]);
            }
            return receive;
        }

        internal void Reset()
        {
            Array.Copy(resetRegisters, registers, registers.Length);
        }

        private static byte ReverseBits(byte value)
        {
            var result = 0;
            for (var i = 0; i < 8; i++)
                result = (result << 1) | ((value >> i) & 1);
            return (byte)result;
        }
    }

    public interface IVirtualRegisterBusHal
    {
        VirtualBusTransactionResult I2cTransaction(byte address, byte registerAddress,
            byte[] writeData, int readLength, bool repeatedStart, int speedHz, double timeoutMilliseconds);
        VirtualBusTransactionResult SpiTransaction(byte bus, byte chipSelect, byte[] transmit,
            VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds);
        VirtualBusTransactionResult I2cReadRegister(byte address, byte registerAddress, int length,
            int speedHz, double timeoutMilliseconds);
        VirtualBusTransactionResult I2cWriteRegister(byte address, byte registerAddress, byte[] data,
            int speedHz, double timeoutMilliseconds);
        VirtualBusTransactionResult SpiReadRegister(byte bus, byte chipSelect, byte registerAddress,
            int length, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds);
        VirtualBusTransactionResult SpiWriteRegister(byte bus, byte chipSelect, byte registerAddress,
            byte[] data, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds);
    }

    public interface IVirtualBusDiagnostics
    {
        event Action<VirtualBusDiagnostic> BusDiagnosticRaised;
        IList<VirtualBusDiagnostic> BusDiagnostics { get; }
    }

    public sealed class VirtualBusBank : IVirtualSpiHal, IVirtualI2cHal,
        IVirtualRegisterBusHal, IVirtualBusDiagnostics, IVirtualPeripheralLifecycle
    {
        private readonly Dictionary<byte, VirtualI2cDevice> i2cDevices =
            new Dictionary<byte, VirtualI2cDevice>();
        private readonly Dictionary<int, VirtualSpiDevice> spiDevices =
            new Dictionary<int, VirtualSpiDevice>();
        private readonly List<VirtualBusDiagnostic> diagnostics = new List<VirtualBusDiagnostic>();
        private double simulatedTimeMilliseconds;

        public event Action<VirtualBusDiagnostic> BusDiagnosticRaised;
        public IList<VirtualBusDiagnostic> BusDiagnostics => diagnostics.AsReadOnly();
        public int DefaultI2cSpeedHz { get; set; } = 100000;
        public int DefaultSpiSpeedHz { get; set; } = 1000000;
        public double SimulatedTimeMilliseconds => simulatedTimeMilliseconds;

        public void RegisterI2cDevice(VirtualI2cDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            if (i2cDevices.ContainsKey(device.Address))
                throw new InvalidOperationException("I2C address is already mapped");
            i2cDevices.Add(device.Address, device);
        }

        public void RegisterSpiDevice(VirtualSpiDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            var key = MakeSpiKey(device.Bus, device.ChipSelect);
            if (spiDevices.ContainsKey(key))
                throw new InvalidOperationException("SPI bus/chip-select is already mapped");
            spiDevices.Add(key, device);
        }

        public void AdvanceTo(double timestampMilliseconds)
        {
            if (double.IsNaN(timestampMilliseconds) || double.IsInfinity(timestampMilliseconds) ||
                timestampMilliseconds < simulatedTimeMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
            simulatedTimeMilliseconds = timestampMilliseconds;
        }

        public byte SpiTransfer(byte bus, byte value)
        {
            return spiDevices.TryGetValue(MakeSpiKey(bus, 0), out var device) && device.Present
                ? device.Transfer(new[] { value }, VirtualSpiMode.Mode0, VirtualBitOrder.MsbFirst)[0]
                : (byte)0;
        }

        public byte I2cTransfer(byte address, byte value)
        {
            if (address > 0x7F)
                throw new InvalidOperationException("I2C address must be 7-bit");
            return i2cDevices.TryGetValue(address, out var device) && device.Present
                ? device.Read(value, 1)[0]
                : (byte)0;
        }

        public VirtualBusTransactionResult I2cReadRegister(byte address, byte registerAddress, int length,
            int speedHz, double timeoutMilliseconds)
        {
            return I2cTransaction(address, registerAddress, null, length, true, speedHz, timeoutMilliseconds);
        }

        public VirtualBusTransactionResult I2cWriteRegister(byte address, byte registerAddress, byte[] data,
            int speedHz, double timeoutMilliseconds)
        {
            return I2cTransaction(address, registerAddress, data, 0, false, speedHz, timeoutMilliseconds);
        }

        public VirtualBusTransactionResult I2cTransaction(byte address, byte registerAddress,
            byte[] writeData, int readLength, bool repeatedStart, int speedHz, double timeoutMilliseconds)
        {
            if (address > 0x7F)
                return Nack("I2C address must be 7-bit", address);
            if (readLength < 0 || readLength > 256 || (writeData != null && writeData.Length > 256))
                return Nack("I2C transaction length is outside 0..256", address);
            var device = i2cDevices.TryGetValue(address, out var mapped) ? mapped : null;
            var effectiveSpeed = speedHz > 0 ? speedHz : DefaultI2cSpeedHz;
            var byteCount = 2 + (writeData?.Length ?? 0) + (readLength == 0 ? 0 : readLength + 1);
            var duration = byteCount * 9.0 * 1000.0 / effectiveSpeed + (device?.ClockStretchMilliseconds ?? 0);
            if (timeoutMilliseconds >= 0 && duration > timeoutMilliseconds)
                return Timeout("I2C transaction exceeded timeout", address, duration);
            if (device == null || !device.Present || !device.Acknowledge)
                return Nack("I2C device did not acknowledge", address, duration);
            if (writeData != null && writeData.Length != 0)
                device.Write(registerAddress, writeData);
            var data = readLength == 0 ? Array.Empty<byte>() : device.Read(registerAddress, readLength);
            simulatedTimeMilliseconds += duration;
            return VirtualBusTransactionResult.Ok(data, duration);
        }

        public VirtualBusTransactionResult SpiReadRegister(byte bus, byte chipSelect, byte registerAddress,
            int length, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            var transmit = new byte[length + 1];
            transmit[0] = (byte)(registerAddress | 0x80);
            var result = SpiTransaction(bus, chipSelect, transmit, mode, bitOrder, speedHz, timeoutMilliseconds);
            if (!result.Success)
                return result;
            var payload = new byte[length];
            Array.Copy(result.Data, 1, payload, 0, length);
            return VirtualBusTransactionResult.Ok(payload, result.DurationMilliseconds);
        }

        public VirtualBusTransactionResult SpiWriteRegister(byte bus, byte chipSelect, byte registerAddress,
            byte[] data, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var transmit = new byte[data.Length + 1];
            transmit[0] = (byte)(registerAddress & 0x7F);
            Array.Copy(data, 0, transmit, 1, data.Length);
            return SpiTransaction(bus, chipSelect, transmit, mode, bitOrder, speedHz, timeoutMilliseconds);
        }

        public VirtualBusTransactionResult SpiTransaction(byte bus, byte chipSelect, byte[] transmit,
            VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            if (transmit == null || transmit.Length == 0)
                return Nack("SPI transaction must contain at least one byte", chipSelect);
            var effectiveSpeed = speedHz > 0 ? speedHz : DefaultSpiSpeedHz;
            var duration = transmit.Length * 8.0 * 1000.0 / effectiveSpeed;
            if (timeoutMilliseconds >= 0 && duration > timeoutMilliseconds)
                return Timeout("SPI transaction exceeded timeout", chipSelect, duration);
            if (!spiDevices.TryGetValue(MakeSpiKey(bus, chipSelect), out var device) ||
                !device.Present || !device.Acknowledge)
                return Nack("SPI device did not acknowledge", chipSelect, duration);
            var data = device.Transfer(transmit, mode, bitOrder);
            simulatedTimeMilliseconds += duration;
            return VirtualBusTransactionResult.Ok(data, duration);
        }

        public void ResetPeripheralState()
        {
            simulatedTimeMilliseconds = 0;
            diagnostics.Clear();
            foreach (var device in i2cDevices.Values)
                device.Reset();
            foreach (var device in spiDevices.Values)
                device.Reset();
        }

        public void ApplySafeState() { }

        private VirtualBusTransactionResult Nack(string message, byte route, double duration = 0)
        {
            RaiseDiagnostic(message.StartsWith("I2C", StringComparison.Ordinal) ? "I2C_NACK" : "SPI_NACK",
                message, message.StartsWith("I2C", StringComparison.Ordinal) ? "I2C" : "SPI", route);
            return VirtualBusTransactionResult.Nack(message, duration);
        }

        private VirtualBusTransactionResult Timeout(string message, byte route, double duration)
        {
            var bus = message.StartsWith("I2C", StringComparison.Ordinal) ? "I2C" : "SPI";
            RaiseDiagnostic(bus + "_TIMEOUT", message, bus, route);
            return VirtualBusTransactionResult.Timeout(message, duration);
        }

        private void RaiseDiagnostic(string code, string message, string bus, byte route)
        {
            var diagnostic = new VirtualBusDiagnostic(code, message, bus, route, simulatedTimeMilliseconds);
            diagnostics.Add(diagnostic);
            BusDiagnosticRaised?.Invoke(diagnostic);
        }

        private static int MakeSpiKey(byte bus, byte chipSelect) => (bus << 8) | chipSelect;
    }
}