using System;
using System.Buffers.Binary;
using RSMA.uDTP.Topics;
using UnityEngine;
using Pose = RSMA.uDTP.Topics.Pose;

namespace RSMA.uDTP.CDR
{
    /// <summary>
    /// Explicit little-endian codecs for the uDTP v1 topic types currently
    /// covered by shared cross-language test vectors.
    /// </summary>
    public static class UdtpV1Codec
    {
        public const int RobotVelocityTypeId = 1001;
        public const int PoseTypeId = 1002;
        public const int LaserScan128TypeId = 1003;

        public static bool TryGetTypeId(Type type, out int typeId)
        {
            if (type == typeof(RobotVelocity)) typeId = RobotVelocityTypeId;
            else if (type == typeof(Pose)) typeId = PoseTypeId;
            else if (type == typeof(LaserScan128)) typeId = LaserScan128TypeId;
            else
            {
                typeId = 0;
                return false;
            }
            return true;
        }

        public static bool IsKnownTypeId(int typeId) =>
            typeId == RobotVelocityTypeId || typeId == PoseTypeId || typeId == LaserScan128TypeId;

        public static int GetPayloadSize(int typeId)
        {
            switch (typeId)
            {
                case RobotVelocityTypeId: return 16;
                case PoseTypeId: return 40;
                case LaserScan128TypeId: return 540;
                default: throw new FormatException($"Unknown uDTP v1 type ID {typeId}.");
            }
        }

        public static byte[] Encode<T>(in T value) where T : struct
        {
            if (typeof(T) == typeof(RobotVelocity))
            {
                var item = (RobotVelocity)(object)value;
                var bytes = new byte[16];
                WriteInt64(bytes, 0, item.timestamp);
                WriteSingle(bytes, 8, item.linearVelocity);
                WriteSingle(bytes, 12, item.angularVelocity);
                return bytes;
            }

            if (typeof(T) == typeof(Pose))
            {
                var item = (Pose)(object)value;
                var bytes = new byte[40];
                WriteInt64(bytes, 0, item.timestamp);
                WriteSingle(bytes, 8, item.position.x);
                WriteSingle(bytes, 12, item.position.y);
                WriteSingle(bytes, 16, item.position.z);
                WriteSingle(bytes, 20, item.rotation.x);
                WriteSingle(bytes, 24, item.rotation.y);
                WriteSingle(bytes, 28, item.rotation.z);
                WriteSingle(bytes, 32, item.rotation.w);
                // bytes 36..39 are pack=8 tail padding and are canonical zero.
                return bytes;
            }

            if (typeof(T) == typeof(LaserScan128))
            {
                var item = (LaserScan128)(object)value;
                var bytes = new byte[540];
                for (var index = 0; index < 128; index++)
                    WriteSingle(bytes, index * 4, item.GetRange(index));
                WriteSingle(bytes, 512, item.angleMin);
                WriteSingle(bytes, 516, item.angleMax);
                WriteSingle(bytes, 520, item.angleIncrement);
                WriteSingle(bytes, 524, item.rangeMin);
                WriteSingle(bytes, 528, item.rangeMax);
                WriteInt64(bytes, 532, item.timestamp);
                return bytes;
            }

            throw new NotSupportedException($"No uDTP v1 codec for {typeof(T).FullName}.");
        }

        public static bool TryDecode(int typeId, byte[] payload, out object value)
        {
            value = null;
            if (payload == null || payload.Length != GetPayloadSize(typeId))
                return false;

            if (typeId == RobotVelocityTypeId)
            {
                value = new RobotVelocity
                {
                    timestamp = ReadInt64(payload, 0),
                    linearVelocity = ReadSingle(payload, 8),
                    angularVelocity = ReadSingle(payload, 12)
                };
                return true;
            }

            if (typeId == PoseTypeId)
            {
                if (ReadInt32(payload, 36) != 0)
                    return false;
                value = new Pose
                {
                    timestamp = ReadInt64(payload, 0),
                    position = new Vector3(
                        ReadSingle(payload, 8),
                        ReadSingle(payload, 12),
                        ReadSingle(payload, 16)),
                    rotation = new Quaternion(
                        ReadSingle(payload, 20),
                        ReadSingle(payload, 24),
                        ReadSingle(payload, 28),
                        ReadSingle(payload, 32))
                };
                return true;
            }

            if (typeId == LaserScan128TypeId)
            {
                var item = new LaserScan128
                {
                    angleMin = ReadSingle(payload, 512),
                    angleMax = ReadSingle(payload, 516),
                    angleIncrement = ReadSingle(payload, 520),
                    rangeMin = ReadSingle(payload, 524),
                    rangeMax = ReadSingle(payload, 528),
                    timestamp = ReadInt64(payload, 532)
                };
                for (var index = 0; index < 128; index++)
                    item.SetRange(index, ReadSingle(payload, index * 4));
                value = item;
                return true;
            }

            return false;
        }

        private static void WriteInt64(byte[] destination, int offset, long value) =>
            BinaryPrimitives.WriteInt64LittleEndian(destination.AsSpan(offset, 8), value);

        private static void WriteSingle(byte[] destination, int offset, float value) =>
            BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset, 4), BitConverter.ToInt32(BitConverter.GetBytes(value), 0));

        private static long ReadInt64(byte[] source, int offset) =>
            BinaryPrimitives.ReadInt64LittleEndian(source.AsSpan(offset, 8));

        private static int ReadInt32(byte[] source, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, 4));

        private static float ReadSingle(byte[] source, int offset) =>
            BitConverter.ToSingle(BitConverter.GetBytes(ReadInt32(source, offset)), 0);
    }
}