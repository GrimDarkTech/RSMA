using System;
using System.Collections.Generic;

namespace RSMA.Microcontroller.Virtual
{
    public enum HitlMessageType : byte
    {
        Hello = 1,
        HelloAck = 2,
        Heartbeat = 3,
        HeartbeatAck = 4,
        Data = 5,
        SafeState = 6
    }

    public enum HitlConnectionState
    {
        Disconnected,
        Connected,
        SafeState
    }

    public sealed class HitlProtocolException : Exception
    {
        public HitlProtocolException(string message) : base(message) { }
    }

    public sealed class HitlFrame
    {
        public byte Version { get; set; } = 1;
        public HitlMessageType Type { get; set; }
        public ushort Flags { get; set; }
        public uint Sequence { get; set; }
        public ulong TimestampMilliseconds { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    public static class HitlFrameCodec
    {
        private const byte CurrentVersion = 1;
        private const int HeaderSize = 22;
        private const int ChecksumSize = 4;
        private const int MaxPayloadSize = ushort.MaxValue;
        private static readonly byte[] Magic = { (byte)'R', (byte)'S', (byte)'M', (byte)'H' };

        public static byte[] Encode(HitlFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (frame.Version != CurrentVersion)
                throw new HitlProtocolException("Unsupported HITL protocol version");
            if (!Enum.IsDefined(typeof(HitlMessageType), frame.Type))
                throw new HitlProtocolException("Unknown HITL message type");

            var payload = frame.Payload ?? Array.Empty<byte>();
            if (payload.Length > MaxPayloadSize)
                throw new ArgumentOutOfRangeException(nameof(frame.Payload));

            var bytes = new byte[HeaderSize + payload.Length + ChecksumSize];
            Array.Copy(Magic, 0, bytes, 0, Magic.Length);
            bytes[4] = frame.Version;
            bytes[5] = (byte)frame.Type;
            WriteU16(bytes, 6, frame.Flags);
            WriteU32(bytes, 8, frame.Sequence);
            WriteU64(bytes, 12, frame.TimestampMilliseconds);
            WriteU16(bytes, 20, (ushort)payload.Length);
            Array.Copy(payload, 0, bytes, HeaderSize, payload.Length);

            var checksum = HitlCrc32.Compute(bytes, 4, bytes.Length - 4 - ChecksumSize);
            WriteU32(bytes, bytes.Length - ChecksumSize, checksum);
            return bytes;
        }

        public static HitlFrame Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < HeaderSize + ChecksumSize)
                throw new HitlProtocolException("HITL frame is truncated");
            for (var index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] != Magic[index])
                    throw new HitlProtocolException("HITL frame magic mismatch");
            }

            var version = bytes[4];
            if (version != CurrentVersion)
                throw new HitlProtocolException("Unsupported HITL protocol version");
            var type = (HitlMessageType)bytes[5];
            if (!Enum.IsDefined(typeof(HitlMessageType), type))
                throw new HitlProtocolException("Unknown HITL message type");

            var payloadLength = ReadU16(bytes, 20);
            var expectedLength = HeaderSize + payloadLength + ChecksumSize;
            if (bytes.Length != expectedLength)
                throw new HitlProtocolException("HITL frame length mismatch");

            var expectedChecksum = ReadU32(bytes, bytes.Length - ChecksumSize);
            var actualChecksum = HitlCrc32.Compute(bytes, 4, bytes.Length - 4 - ChecksumSize);
            if (expectedChecksum != actualChecksum)
                throw new HitlProtocolException("HITL frame CRC mismatch");

            var payload = new byte[payloadLength];
            Array.Copy(bytes, HeaderSize, payload, 0, payloadLength);
            return new HitlFrame
            {
                Version = version,
                Type = type,
                Flags = ReadU16(bytes, 6),
                Sequence = ReadU32(bytes, 8),
                TimestampMilliseconds = ReadU64(bytes, 12),
                Payload = payload
            };
        }

        internal static bool IsMagic(IList<byte> bytes, int offset)
        {
            if (offset < 0 || bytes.Count - offset < Magic.Length)
                return false;
            for (var index = 0; index < Magic.Length; index++)
            {
                if (bytes[offset + index] != Magic[index])
                    return false;
            }
            return true;
        }

        private static void WriteU16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteU32(byte[] bytes, int offset, uint value)
        {
            for (var index = 0; index < 4; index++)
                bytes[offset + index] = (byte)(value >> (index * 8));
        }

        private static void WriteU64(byte[] bytes, int offset, ulong value)
        {
            for (var index = 0; index < 8; index++)
                bytes[offset + index] = (byte)(value >> (index * 8));
        }

        private static ushort ReadU16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | bytes[offset + 1] << 8);
        }

        private static uint ReadU32(byte[] bytes, int offset)
        {
            uint value = 0;
            for (var index = 0; index < 4; index++)
                value |= (uint)bytes[offset + index] << (index * 8);
            return value;
        }

        private static ulong ReadU64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (var index = 0; index < 8; index++)
                value |= (ulong)bytes[offset + index] << (index * 8);
            return value;
        }
    }

    public static class HitlCrc32
    {
        public static uint Compute(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || count < 0 || offset > bytes.Length - count)
                throw new ArgumentOutOfRangeException(nameof(count));

            var crc = 0xFFFFFFFFu;
            for (var index = offset; index < offset + count; index++)
            {
                crc ^= bytes[index];
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }

    public sealed class HitlFrameParser
    {
        private const int HeaderSize = 22;
        private const int ChecksumSize = 4;
        private readonly List<byte> buffer = new List<byte>();

        public IList<HitlFrame> Feed(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            buffer.AddRange(bytes);
            var frames = new List<HitlFrame>();

            while (true)
            {
                var magicOffset = FindMagic();
                if (magicOffset < 0)
                {
                    KeepPotentialMagicPrefix();
                    break;
                }
                if (magicOffset > 0)
                    buffer.RemoveRange(0, magicOffset);
                if (buffer.Count < HeaderSize)
                    break;

                var payloadLength = buffer[20] | buffer[21] << 8;
                var frameLength = HeaderSize + payloadLength + ChecksumSize;
                if (buffer.Count < frameLength)
                    break;

                var frameBytes = buffer.GetRange(0, frameLength).ToArray();
                buffer.RemoveRange(0, frameLength);
                frames.Add(HitlFrameCodec.Decode(frameBytes));
            }
            return frames;
        }

        private int FindMagic()
        {
            for (var index = 0; index <= buffer.Count - 4; index++)
            {
                if (HitlFrameCodec.IsMagic(buffer, index))
                    return index;
            }
            return -1;
        }

        private void KeepPotentialMagicPrefix()
        {
            var keep = 0;
            for (var length = Math.Min(3, buffer.Count); length > 0; length--)
            {
                var start = buffer.Count - length;
                var matches = true;
                for (var index = 0; index < length; index++)
                {
                    if (buffer[start + index] != (byte)"RSMH"[index])
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                {
                    keep = length;
                    break;
                }
            }
            if (buffer.Count > keep)
                buffer.RemoveRange(0, buffer.Count - keep);
        }
    }

    public sealed class HitlSession
    {
        private readonly long heartbeatTimeoutMilliseconds;
        private readonly Action safeStateAction;
        private uint nextSequence;
        private uint lastInboundSequence;
        private bool hasInboundSequence;
        private long lastHeartbeatTimestampMilliseconds;

        public HitlSession(long heartbeatTimeoutMilliseconds, Action safeStateAction)
        {
            if (heartbeatTimeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(heartbeatTimeoutMilliseconds));
            this.heartbeatTimeoutMilliseconds = heartbeatTimeoutMilliseconds;
            this.safeStateAction = safeStateAction ?? throw new ArgumentNullException(nameof(safeStateAction));
            State = HitlConnectionState.Disconnected;
        }

        public HitlConnectionState State { get; private set; }
        public long LastHeartbeatTimestampMilliseconds => lastHeartbeatTimestampMilliseconds;

        public void Connect(long timestampMilliseconds)
        {
            ValidateTimestamp(timestampMilliseconds);
            State = HitlConnectionState.Connected;
            nextSequence = 0;
            hasInboundSequence = false;
            lastHeartbeatTimestampMilliseconds = timestampMilliseconds;
        }

        public void Disconnect()
        {
            if (State == HitlConnectionState.Connected)
                ApplySafeState();
            State = HitlConnectionState.Disconnected;
        }

        public HitlFrame CreateHeartbeat(long timestampMilliseconds)
        {
            EnsureConnected();
            ValidateTimestamp(timestampMilliseconds);
            return new HitlFrame
            {
                Type = HitlMessageType.Heartbeat,
                Sequence = nextSequence++,
                TimestampMilliseconds = (ulong)timestampMilliseconds
            };
        }

        public HitlFrame CreateData(long timestampMilliseconds, byte[] payload)
        {
            EnsureConnected();
            ValidateTimestamp(timestampMilliseconds);
            return new HitlFrame
            {
                Type = HitlMessageType.Data,
                Sequence = nextSequence++,
                TimestampMilliseconds = (ulong)timestampMilliseconds,
                Payload = payload ?? throw new ArgumentNullException(nameof(payload))
            };
        }

        public bool AcceptInbound(HitlFrame frame, long timestampMilliseconds)
        {
            EnsureConnected();
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            ValidateTimestamp(timestampMilliseconds);
            if (hasInboundSequence && frame.Sequence <= lastInboundSequence)
                return false;

            hasInboundSequence = true;
            lastInboundSequence = frame.Sequence;
            if (frame.Type == HitlMessageType.Heartbeat || frame.Type == HitlMessageType.HeartbeatAck)
                lastHeartbeatTimestampMilliseconds = timestampMilliseconds;
            return true;
        }

        public void Tick(long timestampMilliseconds)
        {
            if (State == HitlConnectionState.SafeState)
                return;
            EnsureConnected();
            ValidateTimestamp(timestampMilliseconds);
            if (timestampMilliseconds - lastHeartbeatTimestampMilliseconds >= heartbeatTimeoutMilliseconds)
                ApplySafeState();
        }

        private void ApplySafeState()
        {
            if (State == HitlConnectionState.SafeState)
                return;
            State = HitlConnectionState.SafeState;
            safeStateAction();
        }

        private void EnsureConnected()
        {
            if (State != HitlConnectionState.Connected)
                throw new InvalidOperationException("HITL session is not connected");
        }

        private static void ValidateTimestamp(long timestampMilliseconds)
        {
            if (timestampMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timestampMilliseconds));
        }
    }
}