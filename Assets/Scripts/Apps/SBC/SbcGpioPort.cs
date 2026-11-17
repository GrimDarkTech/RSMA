using System;
using System.Collections.Generic;
using RSMA.Microcontroller.Virtual;
using UnityEngine;

namespace RSMA.SBC
{
    public interface ISbcGpioBackend
    {
        void SetExternalDrive(byte virtualPin, bool level);
        void ClearExternalDrive(byte virtualPin);
        VirtualGpioElectricalState GetElectricalState(byte virtualPin);
    }

    public sealed class VirtualMcuSbcGpioBackend : ISbcGpioBackend
    {
        private readonly RSMAVirtualMicrocontroller _microcontroller;

        public VirtualMcuSbcGpioBackend(RSMAVirtualMicrocontroller microcontroller)
        {
            _microcontroller = microcontroller ?? throw new ArgumentNullException(nameof(microcontroller));
        }

        public void SetExternalDrive(byte virtualPin, bool level) => _microcontroller.SetExternalDrive(virtualPin, level);
        public void ClearExternalDrive(byte virtualPin) => _microcontroller.ClearExternalDrive(virtualPin);
        public VirtualGpioElectricalState GetElectricalState(byte virtualPin) => _microcontroller.GetElectricalState(virtualPin);
    }

    [Serializable]
    public sealed class SbcGpioPinBinding
    {
        [Min(0)]
        public int logicalPin;

        [Min(0)]
        public int virtualMcuPin;
    }

    /// <summary>
    /// SBC-side GPIO boundary. It drives external inputs of a connected virtual MCU
    /// and reads the resulting electrical line state from that MCU backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SbcGpioPort : MonoBehaviour
    {
        [SerializeField]
        private RSMAVirtualMicrocontroller virtualMcu;

        [SerializeField]
        private List<SbcGpioPinBinding> pinBindings = new List<SbcGpioPinBinding>();

        private readonly Dictionary<int, bool?> _drives = new Dictionary<int, bool?>();
        private readonly Dictionary<int, int> _virtualPins = new Dictionary<int, int>();
        private ISbcGpioBackend _backend;

        public RSMAVirtualMicrocontroller VirtualMcu => virtualMcu;
        public IReadOnlyList<SbcGpioPinBinding> PinBindings => pinBindings;

        private void Awake()
        {
            if (virtualMcu != null)
                _backend = new VirtualMcuSbcGpioBackend(virtualMcu);
            RebuildBindingIndex();
        }

        private void OnValidate()
        {
            if (pinBindings == null)
                pinBindings = new List<SbcGpioPinBinding>();
            RebuildBindingIndex();
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        public void AttachVirtualMcu(RSMAVirtualMicrocontroller mcu)
        {
            if (mcu == null)
                throw new ArgumentNullException(nameof(mcu));
            virtualMcu = mcu;
            _backend = new VirtualMcuSbcGpioBackend(mcu);
            RebuildBindingIndex();
            ReapplyDrives();
        }

        public void AttachBackend(ISbcGpioBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            ReapplyDrives();
        }

        public void ConfigureInput(int logicalPin)
        {
            ResolveVirtualPin(logicalPin);
            Release(logicalPin);
        }

        public void Write(int logicalPin, bool level)
        {
            var virtualPin = ResolveVirtualPin(logicalPin);
            _drives[logicalPin] = level;
            RequireBackend().SetExternalDrive((byte)virtualPin, level);
        }

        public void Release(int logicalPin)
        {
            var virtualPin = ResolveVirtualPin(logicalPin);
            _drives[logicalPin] = null;
            RequireBackend().ClearExternalDrive((byte)virtualPin);
        }

        public bool Read(int logicalPin)
        {
            return GetElectricalState(logicalPin).LineLevel;
        }

        public VirtualGpioElectricalState GetElectricalState(int logicalPin)
        {
            var virtualPin = ResolveVirtualPin(logicalPin);
            return RequireBackend().GetElectricalState((byte)virtualPin);
        }

        public void ReleaseAll()
        {
            if (_backend == null)
                return;
            foreach (var logicalPin in _virtualPins.Keys)
            {
                if (_drives.TryGetValue(logicalPin, out var drive) && drive.HasValue)
                    _backend.ClearExternalDrive((byte)_virtualPins[logicalPin]);
            }
            _drives.Clear();
        }

        private int ResolveVirtualPin(int logicalPin)
        {
            RequireBackend();
            if (logicalPin < 0 || !_virtualPins.TryGetValue(logicalPin, out var virtualPin))
                throw new ArgumentOutOfRangeException(nameof(logicalPin), logicalPin,
                    "SBC GPIO logical pin is not mapped to the connected MCU.");
            if (virtualPin > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(logicalPin), "Virtual MCU pin must fit in a byte.");
            return virtualPin;
        }

        private ISbcGpioBackend RequireBackend()
        {
            if (_backend == null)
                throw new InvalidOperationException("SBC GPIO has no connected line backend.");
            return _backend;
        }

        private void RebuildBindingIndex()
        {
            _virtualPins.Clear();
            if (pinBindings == null)
                return;
            foreach (var binding in pinBindings)
            {
                if (binding == null)
                    continue;
                if (binding.logicalPin < 0 || binding.virtualMcuPin < 0 || binding.virtualMcuPin > byte.MaxValue)
                    continue;
                if (_virtualPins.ContainsKey(binding.logicalPin))
                    throw new InvalidOperationException($"Duplicate SBC GPIO logical pin {binding.logicalPin}.");
                _virtualPins.Add(binding.logicalPin, binding.virtualMcuPin);
            }
        }

        private void ReapplyDrives()
        {
            foreach (var pair in _drives)
            {
                if (pair.Value.HasValue)
                    Write(pair.Key, pair.Value.Value);
            }
        }
    }
}