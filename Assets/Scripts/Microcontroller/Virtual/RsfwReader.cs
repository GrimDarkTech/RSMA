using System;
using System.Collections.Generic;
using System.IO;

namespace RSMA.Microcontroller.Virtual
{
    public static class RsfwReader
    {
        private const ushort FormatMajor = 1;
        private const ushort FormatMinor = 0;
        private const ushort VmVersion = 1;
        private const ushort HalVersion = 1;
        private const int HeaderSize = 64;
        private const int SectionEntrySize = 32;
        private const uint MaxSectionCount = 64;
        private const int MaxFirmwareSize = 256 * 1024 * 1024;
        private const uint KnownSectionFlags = 1u | 2u | 4u;
        private const uint MaxInstructionCount = 1000000;

        public static RsfwPackage Read(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaxFirmwareSize)
                throw new RsfwFormatException("Firmware package exceeds implementation size limit");

            var reader = new Reader(bytes);
            reader.ExpectMagic("RSFW");
            var header = ReadHeader(reader);
            ValidateHeader(header, bytes.Length);
            var headerForChecksum = Slice(bytes, 0, HeaderSize);
            WriteU32(headerForChecksum, 52, 0);
            if (Crc32.Compute(headerForChecksum) != header.HeaderChecksum)
                throw new RsfwFormatException("RSFW header checksum mismatch");
            var sections = ReadSections(bytes, header);

            var package = new RsfwPackage { Flags = header.Flags };
            var payload = new List<byte>();
            var hasManifest = false;
            var hasBytecode = false;

            foreach (var section in sections)
            {
                var data = Slice(bytes, checked((int)section.Offset), checked((int)section.Size));
                if (Crc32.Compute(data) != section.Checksum)
                    throw new RsfwFormatException("RSFW section checksum mismatch");
                if ((((uint)section.Flags) & ~KnownSectionFlags) != 0)
                    throw new RsfwFormatException("Section contains unknown flags");

                payload.AddRange(data);
                if (section.Type == RsfwSectionType.Manifest)
                {
                    if (hasManifest)
                        throw new RsfwFormatException("RSFW contains duplicate manifest section");
                    package.Manifest = ReadManifest(data);
                    hasManifest = true;
                }
                else if (section.Type == RsfwSectionType.Bytecode)
                {
                    if (hasBytecode)
                        throw new RsfwFormatException("RSFW contains duplicate bytecode section");
                    package.Bytecode = ReadBytecode(data);
                    hasBytecode = true;
                }
                else
                {
                    var sectionType = (uint)section.Type;
                    var knownSectionType = sectionType >= (uint)RsfwSectionType.ConstantPool &&
                        sectionType <= (uint)RsfwSectionType.Debug;
                    if ((((uint)section.Flags) & (uint)RsfwSectionFlags.Required) != 0 && !knownSectionType)
                        throw new RsfwFormatException("Firmware requires an unknown section type");

                    package.AdditionalSections.Add(new RsfwSection
                    {
                        Type = section.Type,
                        Flags = section.Flags,
                        Data = data
                    });
                }
            }

            if (Crc32.Compute(payload.ToArray()) != header.PayloadChecksum)
                throw new RsfwFormatException("RSFW payload checksum mismatch");
            if (!hasManifest || !hasBytecode)
                throw new RsfwFormatException("RSFW is missing a required section");
            if (header.EntryPointSection != (uint)RsfwSectionType.Bytecode ||
                header.EntryPoint != package.Bytecode.EntryPoint)
                throw new RsfwFormatException("RSFW entry point metadata mismatch");

            ValidatePackage(package);
            return package;
        }

        public static RsfwPackage Read(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return Read(memory.ToArray());
            }
        }

        private static Header ReadHeader(Reader reader)
        {
            var header = new Header
            {
                HeaderSize = reader.U16(),
                FormatMajor = reader.U16(),
                FormatMinor = reader.U16(),
                VmVersion = reader.U16(),
                HalVersion = reader.U16()
            };
            if (reader.U16() != 0)
                throw new RsfwFormatException("RSFW header reserved fields must be zero");

            header.Flags = reader.U32();
            header.SectionCount = reader.U32();
            header.SectionTableOffset = reader.U64();
            header.EntryPoint = reader.U32();
            header.EntryPointSection = reader.U32();
            header.TotalSize = reader.U64();
            header.PayloadChecksum = reader.U32();
            header.HeaderChecksum = reader.U32();
            if (reader.U64() != 0)
                throw new RsfwFormatException("RSFW header reserved fields must be zero");
            return header;
        }

        private static void ValidateHeader(Header header, int actualSize)
        {
            if (header.HeaderSize != HeaderSize)
                throw new RsfwFormatException("Unsupported RSFW header size");
            if (header.FormatMajor != FormatMajor || header.FormatMinor > FormatMinor)
                throw new RsfwFormatException("Unsupported RSFW format version");
            if (header.VmVersion != VmVersion || header.HalVersion != HalVersion)
                throw new RsfwFormatException("Unsupported RSFW VM or HAL version");
            if (header.SectionCount == 0 || header.SectionCount > MaxSectionCount)
                throw new RsfwFormatException("Invalid RSFW section count");
            if (header.TotalSize != (ulong)actualSize)
                throw new RsfwFormatException("RSFW total size does not match file size");
        }

        private static List<SectionView> ReadSections(byte[] bytes, Header header)
        {
            var tableOffset = checked((int)header.SectionTableOffset);
            var tableSize = checked((int)(header.SectionCount * SectionEntrySize));
            if (tableOffset < HeaderSize || tableOffset > bytes.Length - tableSize)
                throw new RsfwFormatException("Section table is outside the firmware");

            var sections = new List<SectionView>((int)header.SectionCount);
            for (var i = 0; i < header.SectionCount; ++i)
            {
                var offset = checked(tableOffset + (int)i * SectionEntrySize);
                var view = new SectionView
                {
                    Type = (RsfwSectionType)ReadU32(bytes, offset),
                    Flags = (RsfwSectionFlags)ReadU32(bytes, offset + 4),
                    Offset = ReadU64(bytes, offset + 8),
                    Size = ReadU64(bytes, offset + 16),
                    Checksum = ReadU32(bytes, offset + 24)
                };
                if (ReadU32(bytes, offset + 28) != 0)
                    throw new RsfwFormatException("Section reserved field must be zero");
                if (view.Offset > (ulong)bytes.Length || view.Size > (ulong)bytes.Length - view.Offset)
                    throw new RsfwFormatException("Section payload is outside the firmware");
                if (view.Offset < (ulong)(tableOffset + tableSize))
                    throw new RsfwFormatException("Section payload overlaps the section table");
                sections.Add(view);
            }

            var ranges = new List<SectionView>(sections);
            ranges.Sort((left, right) => left.Offset.CompareTo(right.Offset));
            for (var i = 1; i < ranges.Count; ++i)
            {
                var previousEnd = ranges[i - 1].Offset + ranges[i - 1].Size;
                if (ranges[i].Offset < previousEnd)
                    throw new RsfwFormatException("Firmware sections overlap");
            }
            return sections;
        }

        private static RsfwManifest ReadManifest(byte[] data)
        {
            var reader = new Reader(data);
            reader.ExpectMagic("MAN1");
            if (reader.U16() != 1)
                throw new RsfwFormatException("Unsupported manifest version");
            reader.U16();

            var manifest = new RsfwManifest
            {
                FlashSize = reader.U32(),
                RamSize = reader.U32()
            };
            var boardSize = reader.U16();
            var entrySize = reader.U16();
            var peripheralCount = reader.U16();
            reader.U16();
            manifest.BoardId = reader.String(boardSize);
            manifest.EntryPointName = reader.String(entrySize);
            for (var i = 0; i < peripheralCount; ++i)
                manifest.RequiredPeripherals.Add(reader.String(reader.U16()));
            reader.End();
            return manifest;
        }

        private static RsfwBytecode ReadBytecode(byte[] data)
        {
            var reader = new Reader(data);
            reader.ExpectMagic("BCOD");
            if (reader.U16() != 1)
                throw new RsfwFormatException("Unsupported bytecode version");
            reader.U16();

            var bytecode = new RsfwBytecode
            {
                EntryPoint = reader.U32()
            };
            var count = reader.U32();
            if (count > MaxInstructionCount || count > (uint)(data.Length - reader.Position) / 8u)
                throw new RsfwFormatException("Bytecode instruction count exceeds available data");

            for (var i = 0u; i < count; ++i)
            {
                var opcode = (RsfwOpcode)reader.Byte();
                reader.Byte();
                reader.U16();
                var operandSize = reader.U32();
                bytecode.Instructions.Add(new RsfwInstruction
                {
                    Opcode = opcode,
                    Operands = reader.Bytes(checked((int)operandSize))
                });
            }
            reader.End();
            return bytecode;
        }

        private static void ValidatePackage(RsfwPackage package)
        {
            if (package.Manifest == null || string.IsNullOrEmpty(package.Manifest.BoardId) ||
                package.Manifest.FlashSize == 0 || package.Manifest.RamSize == 0 ||
                string.IsNullOrEmpty(package.Manifest.EntryPointName))
                throw new RsfwFormatException("Manifest contains invalid required fields");
            if (package.Bytecode == null || package.Bytecode.Instructions.Count == 0 ||
                package.Bytecode.EntryPoint >= package.Bytecode.Instructions.Count)
                throw new RsfwFormatException("Bytecode entry point is outside instruction list");

            for (var i = 0; i < package.Bytecode.Instructions.Count; ++i)
            {
                var instruction = package.Bytecode.Instructions[i];
                switch (instruction.Opcode)
                {
                    case RsfwOpcode.Nop:
                    case RsfwOpcode.Halt:
                    case RsfwOpcode.AddI32:
                    case RsfwOpcode.SubI32:
                    case RsfwOpcode.CmpEqI32:
                    case RsfwOpcode.CmpNeI32:
                    case RsfwOpcode.CmpLtI32:
                    case RsfwOpcode.CmpLeI32:
                    case RsfwOpcode.CmpGtI32:
                    case RsfwOpcode.CmpGeI32:
                    case RsfwOpcode.Return:
                    case RsfwOpcode.ReadTime:
                    case RsfwOpcode.EnableIrq:
                    case RsfwOpcode.DisableIrq:
                    case RsfwOpcode.ReturnIrq:
                    case RsfwOpcode.Yield:
                        RequireOperands(instruction, 0);
                        break;
                    case RsfwOpcode.AttachIrq:
                        RequireOperands(instruction, 6);
                        if ((instruction.Operands[1] & 0x03) == 0 || (instruction.Operands[1] & 0xFC) != 0 ||
                            ReadU32(instruction.Operands, 2) >= package.Bytecode.Instructions.Count)
                            throw new RsfwFormatException("ATTACH_IRQ contains an invalid edge mask or target");
                        break;
                    case RsfwOpcode.ConstI32:
                        RequireOperands(instruction, 4);
                        break;
                    case RsfwOpcode.Load:
                    case RsfwOpcode.Store:
                        RequireOperands(instruction, 2);
                        if (ReadU16(instruction.Operands, 0) >= 256)
                            throw new RsfwFormatException("LOAD/STORE slot index exceeds VM memory");
                        break;
                    case RsfwOpcode.GlobalLoad:
                    case RsfwOpcode.GlobalStore:
                        RequireOperands(instruction, 2);
                        if (ReadU16(instruction.Operands, 0) >= 256)
                            throw new RsfwFormatException("GLOBAL_LOAD/GLOBAL_STORE index exceeds VM memory");
                        break;
                    case RsfwOpcode.Jump:
                    case RsfwOpcode.JumpIf:
                    case RsfwOpcode.Call:
                    case RsfwOpcode.Wait:
                        RequireOperands(instruction, 4);
                        break;
                    case RsfwOpcode.HalCall:
                        RequireOperands(instruction, 4);
                        ValidateHalCall(instruction.Operands);
                        break;
                    default:
                        throw new RsfwFormatException("Bytecode contains an unknown opcode");
                }

                if (instruction.Opcode == RsfwOpcode.Jump || instruction.Opcode == RsfwOpcode.JumpIf ||
                    instruction.Opcode == RsfwOpcode.Call)
                {
                    var target = ReadU32(instruction.Operands, 0);
                    if (target >= package.Bytecode.Instructions.Count)
                        throw new RsfwFormatException("Branch target is outside instruction list");
                }
            }
        }

        private static void RequireOperands(RsfwInstruction instruction, int size)
        {
            if (instruction.Operands == null || instruction.Operands.Length != size)
                throw new RsfwFormatException(instruction.Opcode + " has an invalid operand size");
        }

        private static void ValidateHalCall(byte[] operands)
        {
            var service = ReadU16(operands, 0);
            var value = operands[3];
            if (service < (ushort)RsmaHalService.PinMode || service > (ushort)RsmaHalService.AnalogRead)
                throw new RsfwFormatException("HAL_CALL contains an unknown service ID");
            if (service == (ushort)RsmaHalService.PinMode && value > (byte)VirtualPinMode.Output)
                throw new RsfwFormatException("HAL PinMode value must be Input(0) or Output(1)");
            if (service == (ushort)RsmaHalService.DigitalWrite && value > 1)
                throw new RsfwFormatException("HAL DigitalWrite value must be 0 or 1");
            if (service == (ushort)RsmaHalService.ConfigurePin &&
                ((value & 0xF0) != 0 || ((value >> 1) & 0x03) == 0x03))
                throw new RsfwFormatException("HAL ConfigurePin contains invalid configuration bits");
            if (service >= (ushort)RsmaHalService.UartBegin && service <= (ushort)RsmaHalService.UartWrite)
            {
                if (operands[2] >= VirtualUartBank.PortCount)
                    throw new RsfwFormatException("HAL UART port is outside the supported range");
                if (service == (ushort)RsmaHalService.UartBegin && value >= VirtualUartBank.BaudRateCount)
                    throw new RsfwFormatException("HAL UART baud rate preset is unsupported");
                if (service != (ushort)RsmaHalService.UartBegin && value != 0)
                    throw new RsfwFormatException("HAL UART reserved value must be zero");
            }
            if (service == (ushort)RsmaHalService.AnalogRead && value != 0)
                throw new RsfwFormatException("HAL AnalogRead reserved value must be zero");
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 2)
                throw new RsfwFormatException("Unexpected end of firmware");
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 4)
                throw new RsfwFormatException("Unexpected end of firmware");
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            if (offset < 0 || offset > data.Length - 4)
                throw new RsfwFormatException("Unexpected end of firmware");
            for (var i = 0; i < 4; ++i)
                data[offset + i] = (byte)(value >> (8 * i));
        }

        private static ulong ReadU64(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 8)
                throw new RsfwFormatException("Unexpected end of firmware");
            ulong value = 0;
            for (var i = 0; i < 8; ++i)
                value |= (ulong)data[offset + i] << (8 * i);
            return value;
        }

        private sealed class Header
        {
            public ushort HeaderSize;
            public ushort FormatMajor;
            public ushort FormatMinor;
            public ushort VmVersion;
            public ushort HalVersion;
            public uint Flags;
            public uint SectionCount;
            public ulong SectionTableOffset;
            public uint EntryPoint;
            public uint EntryPointSection;
            public ulong TotalSize;
            public uint PayloadChecksum;
            public uint HeaderChecksum;
        }

        private sealed class SectionView
        {
            public RsfwSectionType Type;
            public RsfwSectionFlags Flags;
            public ulong Offset;
            public ulong Size;
            public uint Checksum;
        }

        private sealed class Reader
        {
            private readonly byte[] data;
            private int position;

            public Reader(byte[] data) { this.data = data; }
            public int Position => position;

            public byte Byte()
            {
                Ensure(1);
                return data[position++];
            }

            public ushort U16()
            {
                var a = Byte();
                var b = Byte();
                return (ushort)(a | b << 8);
            }

            public uint U32()
            {
                var a = Byte();
                var b = Byte();
                var c = Byte();
                var d = Byte();
                return (uint)(a | b << 8 | c << 16 | d << 24);
            }

            public ulong U64()
            {
                ulong value = 0;
                for (var i = 0; i < 8; ++i)
                    value |= (ulong)Byte() << (8 * i);
                return value;
            }

            public byte[] Bytes(int count)
            {
                if (count < 0)
                    throw new RsfwFormatException("Negative byte count");
                Ensure(count);
                var result = new byte[count];
                Buffer.BlockCopy(data, position, result, 0, count);
                position += count;
                return result;
            }

            public string String(int count)
            {
                return System.Text.Encoding.UTF8.GetString(Bytes(count));
            }

            public void ExpectMagic(string expected)
            {
                var actual = System.Text.Encoding.ASCII.GetString(Bytes(expected.Length));
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new RsfwFormatException("Invalid " + expected + " magic");
            }

            public void End()
            {
                if (position != data.Length)
                    throw new RsfwFormatException("Section contains trailing bytes");
            }

            private void Ensure(int count)
            {
                if (count > data.Length - position)
                    throw new RsfwFormatException("Unexpected end of firmware section");
            }
        }
    }

    internal static class Crc32
    {
        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (var i = 0; i < data.Length; ++i)
            {
                crc ^= data[i];
                for (var bit = 0; bit < 8; ++bit)
                {
                    var mask = (uint)-(int)(crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }
            return ~crc;
        }
    }
}
