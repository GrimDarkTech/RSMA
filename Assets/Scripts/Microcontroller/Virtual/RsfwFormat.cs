using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum RsfwSectionType : uint
    {
        Manifest = 1,
        Bytecode = 2,
        ConstantPool = 3,
        MemoryImage = 4,
        BoardRequirements = 5,
        Symbols = 6,
        Debug = 7
    }

    [Flags]
    public enum RsfwSectionFlags : uint
    {
        None = 0,
        Required = 1,
        ReadOnly = 2,
        DebugOnly = 4
    }

    public enum RsfwOpcode : byte
    {
        Nop = 0x00,
        Halt = 0x01,
        ConstI32 = 0x02,
        Load = 0x03,
        Store = 0x04,
        AddI32 = 0x05,
        SubI32 = 0x06,
        Jump = 0x07,
        JumpIf = 0x08,
        Call = 0x09,
        Return = 0x0A,
        ReadTime = 0x0B,
        Wait = 0x0C,
        HalCall = 0x0D,
        AttachIrq = 0x0E,
        EnableIrq = 0x0F,
        DisableIrq = 0x10,
        ReturnIrq = 0x11,
        CmpEqI32 = 0x12,
        CmpNeI32 = 0x13,
        CmpLtI32 = 0x14,
        CmpLeI32 = 0x15,
        CmpGtI32 = 0x16,
        CmpGeI32 = 0x17,
        GlobalLoad = 0x18,
        GlobalStore = 0x19,
        Yield = 0x1A
    }

    public enum RsmaHalService : ushort
    {
        PinMode = 1,
        DigitalWrite = 2,
        DigitalRead = 3,
        DelayMs = 4,
        Millis = 5,
        ConfigurePin = 6,
        PwmWrite = 7,
        UartBegin = 8,
        UartAvailable = 9,
        UartRead = 10,
        UartWrite = 11,
        AnalogRead = 12
    }

    public sealed class RsfwFormatException : Exception
    {
        public RsfwFormatException(string message) : base(message) { }
    }

    public sealed class RsfwManifest
    {
        public string BoardId { get; set; }
        public uint FlashSize { get; set; }
        public uint RamSize { get; set; }
        public string EntryPointName { get; set; }
        public List<string> RequiredPeripherals { get; } = new List<string>();
    }

    public sealed class RsfwInstruction
    {
        public RsfwOpcode Opcode { get; set; }
        public byte[] Operands { get; set; }
    }

    public sealed class RsfwBytecode
    {
        public uint EntryPoint { get; set; }
        public List<RsfwInstruction> Instructions { get; } = new List<RsfwInstruction>();
    }

    public sealed class RsfwSection
    {
        public RsfwSectionType Type { get; set; }
        public RsfwSectionFlags Flags { get; set; }
        public byte[] Data { get; set; }
    }

    public sealed class RsfwPackage
    {
        public uint Flags { get; set; }
        public RsfwManifest Manifest { get; set; }
        public RsfwBytecode Bytecode { get; set; }
        public List<RsfwSection> AdditionalSections { get; } = new List<RsfwSection>();
    }
}
