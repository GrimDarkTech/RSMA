using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TopicHeader
    {
        public long timestamp;      // Метка времени
        public uint sequence;       // Порядковый номер
        public int payloadSize;     // Размер бинарного тела (в байтах)
        public int topicTypeId;     // Stable ID; legacy raw camera frames currently use ID 2.

        public const int WireSize = 20;

        public static TopicHeader DecodeV1(byte[] bytes)
        {
            if (bytes == null || bytes.Length != WireSize)
                throw new FormatException("uDTP v1 header must be exactly 20 bytes.");

            var header = new TopicHeader
            {
                timestamp = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0, 8)),
                sequence = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)),
                payloadSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4)),
                topicTypeId = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4))
            };
            if (header.payloadSize < 0)
                throw new FormatException("uDTP payloadSize cannot be negative.");
            return header;
        }

        public byte[] EncodeV1()
        {
            var bytes = new byte[WireSize];
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0, 8), timestamp);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), sequence);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), payloadSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16, 4), topicTypeId);
            return bytes;
        }
    }
}