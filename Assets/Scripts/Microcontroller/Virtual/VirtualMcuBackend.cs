using System;
using System.Collections.Generic;
using System.Linq;

namespace RSMA.Microcontroller.Virtual
{
    public interface IVirtualBoardCapabilitySource
    {
        IEnumerable<byte> SupportedPins { get; }
        IEnumerable<byte> SupportedAnalogChannels { get; }
        IEnumerable<byte> SupportedSpiBuses { get; }
        IEnumerable<byte> SupportedI2cAddresses { get; }
        IEnumerable<byte> SupportedDmaChannels { get; }
        IEnumerable<byte> SupportedUartPorts { get; }
        IEnumerable<string> Constraints { get; }
    }

    public enum VirtualBackendLifecycleState
    {
        Created,
        Configured,
        Running,
        Stopped,
        SafeState,
        Faulted
    }

    public sealed class VirtualMcuBackendCapabilities
    {
        private readonly HashSet<string> peripherals;
        private readonly string[] peripheralList;

        internal VirtualMcuBackendCapabilities(string boardId, IEnumerable<string> peripherals,
            IVirtualBoardCapabilitySource source)
        {
            BoardId = boardId ?? throw new ArgumentNullException(nameof(boardId));
            this.peripherals = new HashSet<string>(peripherals ?? throw new ArgumentNullException(nameof(peripherals)),
                StringComparer.Ordinal);
            peripheralList = this.peripherals.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Pins = Snapshot(source?.SupportedPins);
            AnalogChannels = Snapshot(source?.SupportedAnalogChannels);
            SpiBuses = Snapshot(source?.SupportedSpiBuses);
            I2cAddresses = Snapshot(source?.SupportedI2cAddresses);
            DmaChannels = Snapshot(source?.SupportedDmaChannels);
            UartPorts = Snapshot(source?.SupportedUartPorts);
            Constraints = Array.AsReadOnly(source?.Constraints == null
                ? Array.Empty<string>() : new List<string>(source.Constraints).ToArray());
        }

        public string BoardId { get; }
        public IEnumerable<string> Peripherals => Array.AsReadOnly(peripheralList);
        public IList<byte> Pins { get; }
        public IList<byte> AnalogChannels { get; }
        public IList<byte> SpiBuses { get; }
        public IList<byte> I2cAddresses { get; }
        public IList<byte> DmaChannels { get; }
        public IList<byte> UartPorts { get; }
        public IList<string> Constraints { get; }

        public bool SupportsPeripheral(string peripheral)
        {
            return peripheral != null && peripherals.Contains(peripheral);
        }

        private static IList<byte> Snapshot(IEnumerable<byte> values)
        {
            var result = values == null ? Array.Empty<byte>() : new List<byte>(values).ToArray();
            Array.Sort(result);
            return Array.AsReadOnly(result);
        }
    }

    public sealed class VirtualBackendConfigurationException : Exception
    {
        public VirtualBackendConfigurationException(string message) : base(message) { }
    }

    public interface IMcuIoBackend
    {
        string BoardId { get; }
        VirtualMcuBackendCapabilities Capabilities { get; }
        VirtualBackendLifecycleState State { get; }
        string FaultMessage { get; }
        void Configure(RsfwPackage package);
        void Reset();
        void Start();
        void Stop();
        void SafeState();
    }

    public interface IVirtualBackendDiagnostics
    {
        IList<string> Diagnostics { get; }
    }

    /// <summary>
    /// Composes the peripheral contracts consumed by the VM into one backend.
    /// Existing banks remain the owners of their state; this class only routes
    /// calls, combines input clocks and performs package preflight validation.
    /// </summary>
    public sealed class VirtualMcuBackend : IMcuIoBackend, IVirtualBackendDiagnostics,
        IVirtualMcuHal, IVirtualPinValueSource, IVirtualInputSource,
        IVirtualGpioEdgeSource, IVirtualButtonHost, IVirtualUartInputSource, IVirtualPwmHal,
        IVirtualPwmFrequencyHal,
        IVirtualPwmOutputSource,
        IVirtualAdcHal, IVirtualAnalogInputSource, IVirtualSpiHal, IVirtualI2cHal,
        IVirtualDmaHal, IVirtualAsyncDmaHal, IVirtualEepromHal, IVirtualUartHal,
        IVirtualUartOutputSource, IVirtualUartTransportInput, IVirtualUartDiagnostics,
        IVirtualUartOutputScheduler, IVirtualRegisterBusHal, IVirtualBusDiagnostics,
        IVirtualDmaDiagnostics,
        IVirtualElectricalModel
    {
        private readonly IVirtualMcuHal gpio;
        private readonly List<object> components = new List<object>();
        private readonly VirtualBoardProfile boardProfile;
        private RsfwPackage configuredPackage;
        public VirtualSchedulingPolicy SchedulingPolicy { get; }

        public VirtualMcuBackend(string boardId, IVirtualMcuHal gpio,
            params object[] peripheralBackends)
            : this(boardId, gpio, null, null, peripheralBackends)
        {
        }

        public VirtualMcuBackend(VirtualBoardProfile boardProfile, IVirtualMcuHal gpio,
            params object[] peripheralBackends)
            : this(
                boardProfile == null ? null : boardProfile.BoardId,
                gpio,
                new VirtualBoardProfileCapabilitySource(boardProfile),
                null,
                peripheralBackends)
        {
        }

        public VirtualMcuBackend(VirtualBoardProfile boardProfile, IVirtualMcuHal gpio,
            VirtualSchedulingPolicy schedulingPolicy, params object[] peripheralBackends)
            : this(
                boardProfile == null ? null : boardProfile.BoardId,
                gpio,
                new VirtualBoardProfileCapabilitySource(boardProfile),
                schedulingPolicy,
                peripheralBackends)
        {
        }

        private VirtualMcuBackend(string boardId, IVirtualMcuHal gpio,
            IVirtualBoardCapabilitySource capabilitySource,
            VirtualSchedulingPolicy schedulingPolicy, object[] peripheralBackends)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                throw new ArgumentException("Backend board ID is required", nameof(boardId));
            if (gpio == null)
                throw new ArgumentNullException(nameof(gpio));
            this.gpio = gpio;
            boardProfile = (capabilitySource as VirtualBoardProfileCapabilitySource)?.Profile;
            SchedulingPolicy = schedulingPolicy?.Clone() ?? new VirtualSchedulingPolicy();
            components.Add(gpio);
            if (capabilitySource is VirtualBoardProfileCapabilitySource profileSource &&
                gpio is IVirtualBoardProfileConsumer profileConsumer)
                profileConsumer.ApplyBoardProfile(profileSource.Profile);
            if (peripheralBackends != null)
            {
                foreach (var backend in peripheralBackends)
                {
                    if (backend == null)
                        throw new ArgumentException("Peripheral backend collection contains null", nameof(peripheralBackends));
                    if (!components.Contains(backend))
                        components.Add(backend);
                }
            }

            capabilitySource = capabilitySource ?? gpio as IVirtualBoardCapabilitySource;
            Capabilities = new VirtualMcuBackendCapabilities(boardId, DiscoverPeripherals(), capabilitySource);
            State = VirtualBackendLifecycleState.Created;
            ValidateProviderCardinality();
            ConnectInputEvents();
        }

        public string BoardId => Capabilities.BoardId;
        public VirtualMcuBackendCapabilities Capabilities { get; }
        public VirtualBackendLifecycleState State { get; private set; }
        public RsfwPackage ConfiguredPackage => configuredPackage;
        public string FaultMessage { get; private set; }
        public IList<string> Diagnostics { get; } = new List<string>();
        public IList<VirtualSchedulingDiagnostic> SchedulingDiagnostics { get; } =
            new List<VirtualSchedulingDiagnostic>();
        public ulong DroppedInterruptCount { get; private set; }
        public IList<string> ElectricalDiagnostics =>
            gpio is IVirtualElectricalModel electrical ? electrical.ElectricalDiagnostics :
                new List<string> { "GPIO provider does not expose electrical diagnostics" };

        public void SetExternalDrive(byte virtualPin, bool level)
        {
            Require<IVirtualElectricalModel>().SetExternalDrive(virtualPin, level);
        }

        public void ClearExternalDrive(byte virtualPin)
        {
            Require<IVirtualElectricalModel>().ClearExternalDrive(virtualPin);
        }

        public VirtualGpioElectricalState GetElectricalState(byte virtualPin)
        {
            return Require<IVirtualElectricalModel>().GetElectricalState(virtualPin);
        }

        public event Action<VirtualGpioInputEvent> InputEdge;
        public event Action<byte, byte> Received;
        public event Action<VirtualUartDiagnostic> DiagnosticRaised;
        public event Action<VirtualBusDiagnostic> BusDiagnosticRaised;
        public event Action<VirtualDmaDiagnostic> DmaDiagnosticRaised;
        event Action<VirtualDmaDiagnostic> IVirtualDmaDiagnostics.DiagnosticRaised
        {
            add { DmaDiagnosticRaised += value; }
            remove { DmaDiagnosticRaised -= value; }
        }
        public event Action<byte, HitlFrame> PacketReceived;
        public event Action<byte, byte> Transmitted;

        public double? NextInputTimestampMilliseconds
        {
            get
            {
                double? result = null;
                foreach (var component in components)
                {
                    var source = component as IVirtualInputSource;
                    if (source == null || !source.NextInputTimestampMilliseconds.HasValue)
                        continue;
                    if (!result.HasValue || source.NextInputTimestampMilliseconds.Value < result.Value)
                        result = source.NextInputTimestampMilliseconds.Value;
                }
                return result;
            }
        }

        public double? NextDmaCompletionTimeMilliseconds
        {
            get { return Has<IVirtualAsyncDmaHal>()
                ? Require<IVirtualAsyncDmaHal>().NextDmaCompletionTimeMilliseconds : null; }
        }

        public void Configure(RsfwPackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));
            if (State == VirtualBackendLifecycleState.Running)
                throw new InvalidOperationException("Cannot configure a running virtual backend");
            if (package.Manifest == null)
                throw new VirtualBackendConfigurationException("Firmware manifest is missing");
            if (package.Bytecode == null)
                throw new VirtualBackendConfigurationException("Firmware bytecode is missing");
            if (!string.Equals(package.Manifest.BoardId, BoardId, StringComparison.Ordinal))
                throw new VirtualBackendConfigurationException("Firmware board '" + package.Manifest.BoardId +
                    "' does not match backend board '" + BoardId + "'");

            var requiredPeripherals = GetRequiredPeripherals(package);
            foreach (var peripheral in requiredPeripherals)
            {
                if (!Capabilities.SupportsPeripheral(peripheral))
                    throw new VirtualBackendConfigurationException("Backend does not provide required peripheral '" +
                        peripheral + "'");
            }
            ValidatePackagePinRoutes(package);
            foreach (var route in package.Manifest.RequiredRoutes)
            {
                if (!SupportsRoute(route))
                    throw new VirtualBackendConfigurationException("Board '" + BoardId +
                        "' does not provide required route '" + route + "'");
            }

            configuredPackage = package;
            FaultMessage = null;
            Diagnostics.Clear();
            State = VirtualBackendLifecycleState.Configured;
        }

        public void Start()
        {
            EnsureConfigured();
            if (State == VirtualBackendLifecycleState.SafeState || State == VirtualBackendLifecycleState.Faulted)
                throw new InvalidOperationException("Cannot start a backend in " + State + " state");
            State = VirtualBackendLifecycleState.Running;
        }

        public void Stop()
        {
            if (State == VirtualBackendLifecycleState.Running || State == VirtualBackendLifecycleState.Configured)
            {
                ApplyLifecycleState(safeState: true);
                State = VirtualBackendLifecycleState.Stopped;
            }
        }

        public void SafeState()
        {
            ApplyLifecycleState(safeState: true);
            if (State != VirtualBackendLifecycleState.Faulted)
                State = VirtualBackendLifecycleState.SafeState;
        }

        public void Reset()
        {
            if (configuredPackage == null)
                throw new InvalidOperationException("Virtual backend has not been configured");
            ApplyLifecycleState(safeState: false);
            FaultMessage = null;
            Diagnostics.Clear();
            State = configuredPackage == null
                ? VirtualBackendLifecycleState.Created : VirtualBackendLifecycleState.Configured;
        }

        public void MarkFaulted(string message = null)
        {
            FaultMessage = message;
            State = VirtualBackendLifecycleState.Faulted;
            ApplyLifecycleState(safeState: true);
        }

        public void PinMode(byte pin, VirtualPinMode mode) { Require<IVirtualMcuHal>().PinMode(pin, mode); }
        public void ConfigurePin(byte pin, VirtualPinMode mode, VirtualPinPullMode pullMode,
            VirtualPinOutputType outputType) { Require<IVirtualMcuHal>().ConfigurePin(pin, mode, pullMode, outputType); }
        public void DigitalWrite(byte pin, bool value) { Require<IVirtualMcuHal>().DigitalWrite(pin, value); }
        public bool DigitalRead(byte pin) { return Require<IVirtualMcuHal>().DigitalRead(pin); }
        public void PwmWrite(byte pin, byte dutyCycle) { Require<IVirtualPwmHal>().PwmWrite(pin, dutyCycle); }
        public void PwmWriteFrequency(byte pin, byte dutyCycle, int frequencyHz)
        {
            Require<IVirtualPwmFrequencyHal>().PwmWriteFrequency(pin, dutyCycle, frequencyHz);
        }
        public VirtualPwmOutputState GetPwmOutput(byte pin)
        {
            return Require<IVirtualPwmOutputSource>().GetPwmOutput(pin);
        }
        public void SetPwmUpdateTime(byte pin, double timestampMilliseconds)
        {
            Require<IVirtualPwmOutputSource>().SetPwmUpdateTime(pin, timestampMilliseconds);
        }
        public ushort AnalogRead(byte channel) { return Require<IVirtualAdcHal>().AnalogRead(channel); }
        public bool SupportsAnalogChannel(byte channel) { return Require<IVirtualAnalogInputSource>().SupportsAnalogChannel(channel); }
        public void SetAnalogInput(byte channel, ushort value) { Require<IVirtualAnalogInputSource>().SetAnalogInput(channel, value); }
        public void ClearAnalogInput(byte channel) { Require<IVirtualAnalogInputSource>().ClearAnalogInput(channel); }
        public void RegisterAnalogSensor(byte channel, Func<double, ushort> sampler) { Require<IVirtualAnalogInputSource>().RegisterAnalogSensor(channel, sampler); }
        public void UnregisterAnalogSensor(byte channel, Func<double, ushort> sampler) { Require<IVirtualAnalogInputSource>().UnregisterAnalogSensor(channel, sampler); }
        public void AdvanceAnalogTo(double timeMilliseconds) { Require<IVirtualAnalogInputSource>().AdvanceAnalogTo(timeMilliseconds); }
        public byte SpiTransfer(byte bus, byte value) { return Require<IVirtualSpiHal>().SpiTransfer(bus, value); }
        public byte I2cTransfer(byte address, byte value) { return Require<IVirtualI2cHal>().I2cTransfer(address, value); }
        public VirtualBusTransactionResult I2cTransaction(byte address, byte registerAddress,
            byte[] writeData, int readLength, bool repeatedStart, int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().I2cTransaction(address, registerAddress, writeData,
                readLength, repeatedStart, speedHz, timeoutMilliseconds);
        }
        public VirtualBusTransactionResult SpiTransaction(byte bus, byte chipSelect, byte[] transmit,
            VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().SpiTransaction(bus, chipSelect, transmit, mode,
                bitOrder, speedHz, timeoutMilliseconds);
        }
        public VirtualBusTransactionResult I2cReadRegister(byte address, byte registerAddress, int length,
            int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().I2cReadRegister(address, registerAddress, length,
                speedHz, timeoutMilliseconds);
        }
        public VirtualBusTransactionResult I2cWriteRegister(byte address, byte registerAddress, byte[] data,
            int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().I2cWriteRegister(address, registerAddress, data,
                speedHz, timeoutMilliseconds);
        }
        public VirtualBusTransactionResult SpiReadRegister(byte bus, byte chipSelect, byte registerAddress,
            int length, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().SpiReadRegister(bus, chipSelect, registerAddress, length,
                mode, bitOrder, speedHz, timeoutMilliseconds);
        }
        public VirtualBusTransactionResult SpiWriteRegister(byte bus, byte chipSelect, byte registerAddress,
            byte[] data, VirtualSpiMode mode, VirtualBitOrder bitOrder, int speedHz, double timeoutMilliseconds)
        {
            return Require<IVirtualRegisterBusHal>().SpiWriteRegister(bus, chipSelect, registerAddress, data,
                mode, bitOrder, speedHz, timeoutMilliseconds);
        }
        public IList<VirtualBusDiagnostic> BusDiagnostics => Has<IVirtualBusDiagnostics>()
            ? Require<IVirtualBusDiagnostics>().BusDiagnostics : new List<VirtualBusDiagnostic>();
        public byte DmaTransfer(byte channel, byte value) { return Require<IVirtualDmaHal>().DmaTransfer(channel, value); }
        public void DmaBufferWrite(byte buffer, byte offset, byte value) { Require<IVirtualAsyncDmaHal>().DmaBufferWrite(buffer, offset, value); }
        public byte DmaBufferRead(byte buffer, byte offset) { return Require<IVirtualAsyncDmaHal>().DmaBufferRead(buffer, offset); }
        public int DmaSubmit(byte channel, byte buffer, int length) { return Require<IVirtualAsyncDmaHal>().DmaSubmit(channel, buffer, length); }
        public int DmaStatus(int ticket) { return Require<IVirtualAsyncDmaHal>().DmaStatus(ticket); }
        public byte DmaReadByte(int ticket, byte offset) { return Require<IVirtualAsyncDmaHal>().DmaReadByte(ticket, offset); }
        public bool DmaRelease(int ticket) { return Require<IVirtualAsyncDmaHal>().DmaRelease(ticket); }
        public void AdvanceDmaTo(double timeMilliseconds)
        {
            if (Has<IVirtualAsyncDmaHal>())
                Require<IVirtualAsyncDmaHal>().AdvanceDmaTo(timeMilliseconds);
        }
        public void ResetDma()
        {
            if (Has<IVirtualAsyncDmaHal>())
                Require<IVirtualAsyncDmaHal>().ResetDma();
        }
        public IList<VirtualDmaDiagnostic> DmaDiagnostics => Has<IVirtualDmaDiagnostics>()
            ? Require<IVirtualDmaDiagnostics>().DmaDiagnostics : new List<VirtualDmaDiagnostic>();
        public byte EepromRead(byte address) { return Require<IVirtualEepromHal>().EepromRead(address); }
        public void EepromWrite(byte address, byte value) { Require<IVirtualEepromHal>().EepromWrite(address, value); }
        public void Begin(byte port, byte baudRatePreset) { Require<IVirtualUartHal>().Begin(port, baudRatePreset); }
        public int Available(byte port) { return Require<IVirtualUartHal>().Available(port); }
        public byte Read(byte port) { return Require<IVirtualUartHal>().Read(port); }
        public void Write(byte port, byte value) { Require<IVirtualUartHal>().Write(port, value); }
        public double? NextOutputTimestampMilliseconds => Has<IVirtualUartOutputScheduler>()
            ? Require<IVirtualUartOutputScheduler>().NextOutputTimestampMilliseconds : null;
        public void AdvanceOutputTo(double timeMilliseconds)
        {
            if (Has<IVirtualUartOutputScheduler>())
                Require<IVirtualUartOutputScheduler>().AdvanceOutputTo(timeMilliseconds);
        }
        public int GetTransmitQueueCount(byte port)
        {
            return Require<IVirtualUartOutputScheduler>().GetTransmitQueueCount(port);
        }
        public IList<VirtualUartDiagnostic> UartDiagnostics => Has<IVirtualUartDiagnostics>()
            ? Require<IVirtualUartDiagnostics>().UartDiagnostics : new List<VirtualUartDiagnostic>();
        public ulong DroppedReceiveByteCount => Has<IVirtualUartDiagnostics>()
            ? Require<IVirtualUartDiagnostics>().DroppedReceiveByteCount : 0UL;
        public ulong DroppedTransmitByteCount => Has<IVirtualUartDiagnostics>()
            ? Require<IVirtualUartDiagnostics>().DroppedTransmitByteCount : 0UL;
        public int PacketAvailable(byte port) { return Require<IVirtualUartHal>().PacketAvailable(port); }
        public byte PacketRead(byte port) { return Require<IVirtualUartHal>().PacketRead(port); }
        public byte PacketType(byte port) { return Require<IVirtualUartHal>().PacketType(port); }
        public bool HasPacket(byte port) { return Require<IVirtualUartHal>().HasPacket(port); }
        public void CompletePacket(byte port) { Require<IVirtualUartHal>().CompletePacket(port); }

        public void AdvanceInputTo(double timeMilliseconds)
        {
            foreach (var component in components)
            {
                var source = component as IVirtualInputSource;
                if (source != null)
                    source.AdvanceInputTo(timeMilliseconds);
            }
        }

        public void ScheduleReceived(byte port, double timeMilliseconds, byte[] data)
        {
            Require<IVirtualUartInputSource>().ScheduleReceived(port, timeMilliseconds, data);
        }

        public void InjectReceived(byte port, byte[] data)
        {
            var transportInput = Require<IVirtualUartTransportInput>();
            transportInput.InjectReceived(port, data);
        }

        public void InjectTransportBytes(byte port, byte[] data)
        {
            Require<IVirtualUartTransportInput>().InjectTransportBytes(port, data);
        }

        public float ReadPinValue(byte pin)
        {
            var valueSource = gpio as IVirtualPinValueSource;
            if (valueSource == null)
                throw new InvalidOperationException("GPIO provider does not expose output values");
            return valueSource.ReadPinValue(pin);
        }

        public VirtualPinMode GetMode(byte pin)
        {
            var buttonHost = gpio as IVirtualButtonHost;
            if (buttonHost == null)
                throw new VirtualMachineFaultException("GPIO component does not expose input scheduling");
            return buttonHost.GetMode(pin);
        }

        public void ScheduleInputLevel(byte pin, double timeMilliseconds, bool level)
        {
            var buttonHost = gpio as IVirtualButtonHost;
            if (buttonHost == null)
                throw new VirtualMachineFaultException("GPIO component does not expose input scheduling");
            buttonHost.ScheduleInputLevel(pin, timeMilliseconds, level);
        }

        private IEnumerable<string> DiscoverPeripherals()
        {
            var result = new HashSet<string>(StringComparer.Ordinal) { "GPIO", "SYSTICK" };
            if (Has<IVirtualPwmHal>())
            {
                result.Add("PWM");
                // Older RSFW manifests name the timer capability explicitly.
                // The current runtime contract exposes timer output through
                // IVirtualPwmHal, so TIM is an alias, not a second provider.
                result.Add("TIM");
            }
            if (Has<IVirtualAdcHal>()) result.Add("ADC");
            if (Has<IVirtualSpiHal>()) result.Add("SPI");
            if (Has<IVirtualI2cHal>()) result.Add("I2C");
            if (Has<IVirtualDmaHal>()) result.Add("DMA");
            if (Has<IVirtualAsyncDmaHal>()) result.Add("DMA_ASYNC");
            if (Has<IVirtualEepromHal>()) result.Add("EEPROM");
            if (Has<IVirtualUartHal>()) result.Add("UART");
            if (Has<IVirtualGpioEdgeSource>()) result.Add("IRQ");
            return result;
        }

        private void ConnectInputEvents()
        {
            foreach (var component in components)
            {
                var gpio = component as IVirtualGpioEdgeSource;
                if (gpio != null) gpio.InputEdge += input => InputEdge?.Invoke(input);
                var uart = component as IVirtualUartInputSource;
                if (uart != null)
                {
                    uart.Received += (port, value) => Received?.Invoke(port, value);
                    uart.PacketReceived += (port, frame) => PacketReceived?.Invoke(port, frame);
                }
                var uartOutput = component as IVirtualUartOutputSource;
                if (uartOutput != null)
                    uartOutput.Transmitted += (port, value) => Transmitted?.Invoke(port, value);
                var uartDiagnostics = component as IVirtualUartDiagnostics;
                if (uartDiagnostics != null)
                    uartDiagnostics.DiagnosticRaised += diagnostic => DiagnosticRaised?.Invoke(diagnostic);
                var busDiagnostics = component as IVirtualBusDiagnostics;
                if (busDiagnostics != null)
                    busDiagnostics.BusDiagnosticRaised += diagnostic => BusDiagnosticRaised?.Invoke(diagnostic);
                var dmaDiagnostics = component as IVirtualDmaDiagnostics;
                if (dmaDiagnostics != null)
                    dmaDiagnostics.DiagnosticRaised += diagnostic => DmaDiagnosticRaised?.Invoke(diagnostic);
            }
        }

        public void RecordSchedulingDiagnostic(VirtualSchedulingDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return;
            SchedulingDiagnostics.Add(diagnostic);
            Diagnostics.Add(diagnostic.Code + ": " + diagnostic.Message);
            if (diagnostic.Code == "IRQ_QUEUE_OVERFLOW" || diagnostic.Code == "IRQ_MASKED")
                DroppedInterruptCount++;
        }

        private bool Has<T>() where T : class
        {
            foreach (var component in components)
                if (component is T) return true;
            return false;
        }

        private T Require<T>() where T : class
        {
            if (typeof(T) == typeof(IVirtualSpiHal) || typeof(T) == typeof(IVirtualI2cHal) ||
                typeof(T) == typeof(IVirtualDmaHal) || typeof(T) == typeof(IVirtualAsyncDmaHal) ||
                typeof(T) == typeof(IVirtualEepromHal))
            {
                for (var index = components.Count - 1; index > 0; index--)
                {
                    var provider = components[index] as T;
                    if (provider != null && !(components[index] is IVirtualMcuHal))
                        return provider;
                }
            }
            foreach (var component in components)
            {
                var provider = component as T;
                if (provider != null)
                    return provider;
            }
            throw new VirtualMachineFaultException("Virtual backend does not provide " + typeof(T).Name);
        }

        private void ValidateProviderCardinality()
        {
            ValidateProviderCardinality<IVirtualUartHal>();
            ValidateProviderCardinality<IVirtualUartInputSource>();
            ValidateProviderCardinality<IVirtualUartTransportInput>();
            ValidatePeripheralProviders<IVirtualSpiHal>();
            ValidatePeripheralProviders<IVirtualI2cHal>();
            ValidatePeripheralProviders<IVirtualDmaHal>();
            ValidatePeripheralProviders<IVirtualAsyncDmaHal>();
            ValidatePeripheralProviders<IVirtualEepromHal>();
        }

        private void ValidatePeripheralProviders<T>() where T : class
        {
            object provider = null;
            for (var index = 1; index < components.Count; index++)
            {
                var component = components[index];
                if (!(component is T) || component is IVirtualMcuHal)
                    continue;
                if (provider != null && !ReferenceEquals(provider, component))
                    throw new VirtualBackendConfigurationException("Multiple peripheral providers implement " +
                        typeof(T).Name);
                provider = component;
            }
        }

        private void ValidateProviderCardinality<T>() where T : class
        {
            object provider = null;
            foreach (var component in components)
            {
                if (!(component is T))
                    continue;
                if (ReferenceEquals(component, gpio))
                    continue;
                if (provider != null && !ReferenceEquals(provider, component))
                    throw new VirtualBackendConfigurationException("Multiple providers implement " + typeof(T).Name);
                provider = component;
            }
        }

        private void ApplyLifecycleState(bool safeState)
        {
            foreach (var component in components)
            {
                var lifecycle = component as IVirtualPeripheralLifecycle;
                if (lifecycle == null)
                    continue;
                try
                {
                    if (safeState)
                        lifecycle.ApplySafeState();
                    else
                        lifecycle.ResetPeripheralState();
                }
                catch (Exception exception)
                {
                    Diagnostics.Add(component.GetType().Name + ": " + exception.Message);
                    if (safeState)
                        FaultMessage = string.IsNullOrEmpty(FaultMessage)
                            ? exception.Message : FaultMessage + "; " + exception.Message;
                    else
                        throw new VirtualBackendConfigurationException("Peripheral reset failed: " +
                            component.GetType().Name + ": " + exception.Message);
                }
            }
        }

        private IEnumerable<string> GetRequiredPeripherals(RsfwPackage package)
        {
            var required = new HashSet<string>(package.Manifest.RequiredPeripherals, StringComparer.Ordinal);
            foreach (var instruction in package.Bytecode.Instructions)
            {
                if (instruction.Opcode == RsfwOpcode.AttachIrq)
                    required.Add("IRQ");
                if (instruction.Opcode != RsfwOpcode.HalCall || instruction.Operands == null ||
                    instruction.Operands.Length < 4)
                    continue;

                var service = (RsmaHalService)(instruction.Operands[0] | instruction.Operands[1] << 8);
                switch (service)
                {
                    case RsmaHalService.PinMode:
                    case RsmaHalService.DigitalWrite:
                    case RsmaHalService.DigitalRead:
                    case RsmaHalService.ConfigurePin:
                        required.Add("GPIO");
                        break;
                    case RsmaHalService.PwmWrite:
                    case RsmaHalService.PwmWriteFrequency:
                    case RsmaHalService.AnalogWrite:
                        required.Add("PWM");
                        break;
                    case RsmaHalService.AnalogRead:
                        required.Add("ADC");
                        break;
                    case RsmaHalService.UartBegin:
                    case RsmaHalService.UartAvailable:
                    case RsmaHalService.UartRead:
                    case RsmaHalService.UartWrite:
                    case RsmaHalService.UartPacketAvailable:
                    case RsmaHalService.UartPacketRead:
                    case RsmaHalService.UartPacketType:
                        required.Add("UART");
                        break;
                    case RsmaHalService.SpiTransfer:
                    case RsmaHalService.SpiReadRegister:
                    case RsmaHalService.SpiWriteRegister:
                        required.Add("SPI");
                        break;
                    case RsmaHalService.I2cTransfer:
                    case RsmaHalService.I2cReadRegister:
                    case RsmaHalService.I2cWriteRegister:
                        required.Add("I2C");
                        break;
                    case RsmaHalService.DmaTransfer:
                        required.Add("DMA");
                        break;
                    case RsmaHalService.DmaBufferWrite:
                    case RsmaHalService.DmaBufferRead:
                    case RsmaHalService.DmaSubmit:
                    case RsmaHalService.DmaStatus:
                    case RsmaHalService.DmaReadByte:
                    case RsmaHalService.DmaRelease:
                        required.Add("DMA");
                        required.Add("DMA_ASYNC");
                        break;
                    case RsmaHalService.EepromRead:
                    case RsmaHalService.EepromWrite:
                        required.Add("EEPROM");
                        break;
                }
            }
            return required;
        }

        private void ValidatePackagePinRoutes(RsfwPackage package)
        {
            if (boardProfile == null)
                return;
            foreach (var instruction in package.Bytecode.Instructions)
            {
                if (instruction.Opcode == RsfwOpcode.AttachIrq)
                {
                    EnsureMappedPin(instruction.Operands[0]);
                    var irqDefinition = boardProfile.GetPin(instruction.Operands[0]);
                    RequirePinCapability(irqDefinition, VirtualPinCapabilities.ExternalInterrupt,
                        "external IRQ");
                    RequireAlternate(irqDefinition, "EXTI");
                    continue;
                }
                if (instruction.Opcode != RsfwOpcode.HalCall || instruction.Operands == null ||
                    instruction.Operands.Length < 4)
                    continue;
                var service = (RsmaHalService)(instruction.Operands[0] | instruction.Operands[1] << 8);
                var pin = instruction.Operands[2];
                var value = instruction.Operands[3];
                var pinService = service == RsmaHalService.PinMode ||
                    service == RsmaHalService.DigitalWrite || service == RsmaHalService.DigitalRead ||
                    service == RsmaHalService.ConfigurePin || service == RsmaHalService.PwmWrite ||
                    service == RsmaHalService.PwmWriteFrequency || service == RsmaHalService.AnalogWrite ||
                    service == RsmaHalService.AnalogRead;
                if (!pinService)
                {
                    if (service == RsmaHalService.SpiTransfer)
                        RequireBoardAlternate("SPI");
                    else if (service == RsmaHalService.I2cTransfer)
                        RequireBoardAlternate("I2C");
                    continue;
                }
                EnsureMappedPin(pin);
                var definition = boardProfile.GetPin(pin);
                switch (service)
                {
                    case RsmaHalService.PinMode:
                        RequirePinCapability(definition,
                            value == (byte)VirtualPinMode.Input
                                ? VirtualPinCapabilities.DigitalInput
                                : VirtualPinCapabilities.DigitalOutput,
                            "GPIO mode");
                        break;
                    case RsmaHalService.DigitalRead:
                        RequirePinCapability(definition, VirtualPinCapabilities.DigitalInput, "digital input");
                        break;
                    case RsmaHalService.DigitalWrite:
                        RequirePinCapability(definition, VirtualPinCapabilities.DigitalOutput, "digital output");
                        break;
                    case RsmaHalService.ConfigurePin:
                        RequirePinCapability(definition,
                            (value & 0x01) != 0 ? VirtualPinCapabilities.DigitalOutput :
                                VirtualPinCapabilities.DigitalInput, "GPIO configuration");
                        var pull = (value >> 1) & 0x03;
                        if (pull == (byte)VirtualPinPullMode.PullUp)
                            RequirePinCapability(definition, VirtualPinCapabilities.PullUp, "pull-up");
                        if (pull == (byte)VirtualPinPullMode.PullDown)
                            RequirePinCapability(definition, VirtualPinCapabilities.PullDown, "pull-down");
                        if (((value >> 3) & 0x01) != 0)
                            RequirePinCapability(definition, VirtualPinCapabilities.OpenDrain, "open-drain");
                        break;
                    case RsmaHalService.PwmWrite:
                    case RsmaHalService.AnalogWrite:
                        RequirePinCapability(definition, VirtualPinCapabilities.Pwm, "PWM");
                        RequireAlternate(definition, "PWM");
                        break;
                    case RsmaHalService.AnalogRead:
                        RequirePinCapability(definition, VirtualPinCapabilities.Analog, "ADC");
                        RequireAlternate(definition, "ADC");
                        break;
                }
            }
        }

        private bool SupportsRoute(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
                return false;
            var separator = requirement.IndexOf('@');
            var name = separator < 0 ? null : requirement.Substring(0, separator);
            var route = separator < 0 ? requirement : requirement.Substring(separator + 1);
            foreach (var definition in boardProfile.PinDefinitions)
            {
                foreach (var alternate in definition.AlternateFunctions)
                {
                    if ((name == null || string.Equals(alternate.Name, name, StringComparison.Ordinal)) &&
                        string.Equals(alternate.Route, route, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private void RequireBoardAlternate(string function)
        {
            foreach (var definition in boardProfile.PinDefinitions)
            {
                if (definition.SupportsAlternateFunction(function))
                    return;
            }
            throw new VirtualBackendConfigurationException("Board '" + boardProfile.BoardId +
                "' has no " + function + " alternate-function route");
        }

        private void EnsureMappedPin(byte pin)
        {
            var source = gpio as IVirtualBoardCapabilitySource;
            if (source != null && !source.SupportedPins.Contains(pin))
                throw new VirtualBackendConfigurationException("Board pin " + pin +
                    " is present in the profile but is not mapped by the active GPIO provider");
        }

        private static void RequirePinCapability(VirtualPinDefinition definition,
            VirtualPinCapabilities capability, string operation)
        {
            if (!definition.Supports(capability))
                throw new VirtualBackendConfigurationException("Board pin " + definition.VirtualPin +
                    " does not support " + operation);
        }

        private static void RequireAlternate(VirtualPinDefinition definition, string function)
        {
            if (!definition.SupportsAlternateFunction(function))
                throw new VirtualBackendConfigurationException("Board pin " + definition.VirtualPin +
                    " has no " + function + " alternate-function route");
        }

        private sealed class VirtualBoardProfileCapabilitySource : IVirtualBoardCapabilitySource
        {
            private readonly VirtualBoardProfile profile;

            public VirtualBoardProfileCapabilitySource(VirtualBoardProfile profile)
            {
                this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            }

            public VirtualBoardProfile Profile => profile;

            public IEnumerable<byte> SupportedPins => profile.PinDefinitions
                .Select(definition => definition.VirtualPin);
            public IEnumerable<byte> SupportedAnalogChannels => profile.PinDefinitions
                .Where(definition => definition.Supports(VirtualPinCapabilities.Analog))
                .Select(definition => definition.VirtualPin);
            public IEnumerable<byte> SupportedSpiBuses => Array.Empty<byte>();
            public IEnumerable<byte> SupportedI2cAddresses => Array.Empty<byte>();
            public IEnumerable<byte> SupportedDmaChannels => Array.Empty<byte>();
            public IEnumerable<byte> SupportedUartPorts => Array.Empty<byte>();
            public IEnumerable<string> Constraints => new[]
            {
                "Pin capabilities are limited to the supplied VirtualBoardProfile"
            };
        }

        private void EnsureConfigured()
        {
            if (configuredPackage == null || State == VirtualBackendLifecycleState.Created)
                throw new InvalidOperationException("Virtual backend has not been configured");
        }
    }
}