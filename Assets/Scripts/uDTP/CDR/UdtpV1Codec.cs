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
        public const int OpaqueTypeId = 0;
        public const int LegacyCameraTypeId = 2;
        public const int RobotVelocityTypeId = 1001;
        public const int PoseTypeId = 1002;
        public const int LaserScan128TypeId = 1003;
        public const int Float32TypeId = 1004;
        public const int HILSensorTypeId = 1005;
        public const int HILGPSTypeId = 1006;
        public const int HILStateQuaternionTypeId = 1007;
        public const int HILOpticalFlowTypeId = 1008;
        public const int LaserScan256TypeId = 1009;
        public const int ActuatorInputsTypeId = 1010;
        public const int ArmCommandTypeId = 1011;
        public const int FlightModeCommandTypeId = 1012;
        public const int MotorInputTypeId = 1013;
        public const int RCChannelsInputTypeId = 1014;
        public const int TrajectoryPointTypeId = 1015;

        public static bool TryGetTypeId(Type type, out int typeId)
        {
            if (type == typeof(RobotVelocity)) typeId = RobotVelocityTypeId;
            else if (type == typeof(Pose)) typeId = PoseTypeId;
            else if (type == typeof(LaserScan128)) typeId = LaserScan128TypeId;
            else if (type == typeof(Float32)) typeId = Float32TypeId;
            else if (type == typeof(HILSensor)) typeId = HILSensorTypeId;
            else if (type == typeof(HILGPS)) typeId = HILGPSTypeId;
            else if (type == typeof(HILStateQuaternion)) typeId = HILStateQuaternionTypeId;
            else if (type == typeof(HILOpticalFlow)) typeId = HILOpticalFlowTypeId;
            else if (type == typeof(LaserScan256)) typeId = LaserScan256TypeId;
            else if (type == typeof(ActuatorInputs)) typeId = ActuatorInputsTypeId;
            else if (type == typeof(ArmCommand)) typeId = ArmCommandTypeId;
            else if (type == typeof(FlightModeCommand)) typeId = FlightModeCommandTypeId;
            else if (type == typeof(MotorInput)) typeId = MotorInputTypeId;
            else if (type == typeof(RCChannelsInput)) typeId = RCChannelsInputTypeId;
            else if (type == typeof(TrajectoryPoint)) typeId = TrajectoryPointTypeId;
            else
            {
                typeId = 0;
                return false;
            }
            return true;
        }

        public static bool IsKnownTypeId(int typeId) =>
            typeId >= RobotVelocityTypeId && typeId <= TrajectoryPointTypeId;

        public static bool IsOpaqueTypeId(int typeId) =>
            typeId == OpaqueTypeId || typeId == LegacyCameraTypeId;

        public static int GetPayloadSize(int typeId)
        {
            switch (typeId)
            {
                case RobotVelocityTypeId: return 16;
                case PoseTypeId: return 40;
                case LaserScan128TypeId: return 540;
                case Float32TypeId: return 16;
                case HILSensorTypeId: return 56;
                case HILGPSTypeId: return 40;
                case HILStateQuaternionTypeId: return 56;
                case HILOpticalFlowTypeId: return 64;
                case LaserScan256TypeId: return 1052;
                case ActuatorInputsTypeId: return 76;
                case ArmCommandTypeId: return 16;
                case FlightModeCommandTypeId: return 16;
                case MotorInputTypeId: return 16;
                case RCChannelsInputTypeId: return 48;
                case TrajectoryPointTypeId: return 24;
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

            if (typeof(T) == typeof(Float32))
            {
                var item = (Float32)(object)value;
                var bytes = new byte[16];
                WriteSingle(bytes, 0, item.value);
                WriteInt64(bytes, 8, item.timestamp);
                return bytes;
            }

            if (typeof(T) == typeof(HILSensor))
            {
                var item = (HILSensor)(object)value;
                var bytes = new byte[56];
                WriteInt64(bytes, 0, item.timestamp);
                var fields = new[] { item.accel_x, item.accel_y, item.accel_z, item.gyro_x, item.gyro_y, item.gyro_z, item.mag_x, item.mag_y, item.mag_z, item.abs_pressure, item.pressure_alt, item.temperature };
                for (var i = 0; i < fields.Length; i++) WriteSingle(bytes, 8 + i * 4, fields[i]);
                return bytes;
            }

            if (typeof(T) == typeof(HILGPS))
            {
                var item = (HILGPS)(object)value;
                var bytes = new byte[40];
                WriteInt64(bytes, 0, item.timestamp); bytes[8] = item.fix_type;
                WriteInt32(bytes, 12, item.lat); WriteInt32(bytes, 16, item.lon); WriteInt32(bytes, 20, item.alt);
                WriteUInt16(bytes, 24, item.eph); WriteUInt16(bytes, 26, item.epv); WriteUInt16(bytes, 28, item.vel);
                WriteInt16(bytes, 30, item.vn); WriteInt16(bytes, 32, item.ve); WriteInt16(bytes, 34, item.vd);
                WriteUInt16(bytes, 36, item.cog); bytes[38] = item.satellites_visible;
                return bytes;
            }

            if (typeof(T) == typeof(HILStateQuaternion))
            {
                var item = (HILStateQuaternion)(object)value;
                var bytes = new byte[56];
                WriteInt64(bytes, 0, item.timestamp);
                for (var i = 0; i < 4; i++) WriteSingle(bytes, 8 + i * 4, item.GetOrientation(i));
                WriteSingle(bytes, 24, item.rollspeed); WriteSingle(bytes, 28, item.yawspeed); WriteSingle(bytes, 32, item.pitchspeed);
                WriteInt32(bytes, 36, item.lat); WriteInt32(bytes, 40, item.lon); WriteInt32(bytes, 44, item.alt);
                WriteInt16(bytes, 48, item.vn); WriteInt16(bytes, 50, item.ve); WriteInt16(bytes, 52, item.vd);
                return bytes;
            }

            if (typeof(T) == typeof(HILOpticalFlow))
            {
                var item = (HILOpticalFlow)(object)value;
                var bytes = new byte[64];
                WriteInt64(bytes, 0, item.timestamp); WriteUInt64(bytes, 8, item.time_usec); WriteUInt16(bytes, 16, item.sensor_id);
                WriteSingle(bytes, 20, item.integration_time_us); WriteSingle(bytes, 24, item.integrated_x); WriteSingle(bytes, 28, item.integrated_y);
                WriteSingle(bytes, 32, item.integrated_xgyro); WriteSingle(bytes, 36, item.integrated_ygyro); WriteSingle(bytes, 40, item.integrated_zgyro);
                WriteUInt32(bytes, 44, item.temperature); bytes[48] = item.quality;
                WriteSingle(bytes, 52, item.time_delta_distance_us); WriteSingle(bytes, 56, item.distance);
                return bytes;
            }

            if (typeof(T) == typeof(LaserScan256))
            {
                var item = (LaserScan256)(object)value;
                var bytes = new byte[GetPayloadSize(LaserScan256TypeId)];
                for (var i = 0; i < 256; i++) WriteSingle(bytes, i * 4, item.GetRange(i));
                WriteSingle(bytes, 1024, item.angleMin); WriteSingle(bytes, 1028, item.angleMax); WriteSingle(bytes, 1032, item.angleIncrement);
                WriteSingle(bytes, 1036, item.rangeMin); WriteSingle(bytes, 1040, item.rangeMax); WriteInt64(bytes, 1044, item.timestamp);
                return bytes;
            }

            if (typeof(T) == typeof(ActuatorInputs))
            {
                var item = (ActuatorInputs)(object)value;
                var bytes = new byte[76];
                WriteInt64(bytes, 0, item.timestamp); WriteInt32(bytes, 8, item.size);
                for (var i = 0; i < 16; i++) WriteSingle(bytes, 12 + i * 4, item.GetInput(i));
                return bytes;
            }

            if (typeof(T) == typeof(ArmCommand))
            {
                var item = (ArmCommand)(object)value;
                var bytes = new byte[16]; WriteInt64(bytes, 0, item.timestamp); bytes[8] = item.arm; return bytes;
            }

            if (typeof(T) == typeof(FlightModeCommand))
            {
                var item = (FlightModeCommand)(object)value;
                var bytes = new byte[16]; WriteInt64(bytes, 0, item.timestamp); WriteInt32(bytes, 8, item.mode); WriteInt32(bytes, 12, item.sub_mode); return bytes;
            }

            if (typeof(T) == typeof(MotorInput))
            {
                var item = (MotorInput)(object)value;
                var bytes = new byte[16]; WriteInt64(bytes, 0, item.timestamp); WriteSingle(bytes, 8, item.input); return bytes;
            }

            if (typeof(T) == typeof(RCChannelsInput))
            {
                var item = (RCChannelsInput)(object)value;
                var bytes = new byte[48]; WriteInt64(bytes, 0, item.timestamp);
                for (var i = 0; i < 18; i++) WriteUInt16(bytes, 8 + i * 2, item.GetChannel(i));
                bytes[44] = item.chancount;
                return bytes;
            }

            if (typeof(T) == typeof(TrajectoryPoint))
            {
                var item = (TrajectoryPoint)(object)value;
                var bytes = new byte[24]; WriteInt64(bytes, 0, item.timestamp);
                WriteSingle(bytes, 8, item.position.x); WriteSingle(bytes, 12, item.position.y); WriteSingle(bytes, 16, item.position.z);
                WriteSingle(bytes, 20, item.targetVelocity);
                return bytes;
            }

            throw new NotSupportedException($"No uDTP v1 codec for {typeof(T).FullName}.");
        }

        public static bool TryDecode(int typeId, byte[] payload, out object value)
        {
            value = null;
            if (payload == null)
                return false;

            int expectedSize;
            try { expectedSize = GetPayloadSize(typeId); }
            catch (FormatException) { return false; }
            if (payload.Length != expectedSize)
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

            if (typeId == Float32TypeId)
            {
                if (!HasZeroBytes(payload, 4, 4)) return false;
                value = new Float32 { value = ReadSingle(payload, 0), timestamp = ReadInt64(payload, 8) };
                return true;
            }

            if (typeId == HILSensorTypeId)
            {
                var item = new HILSensor { timestamp = ReadInt64(payload, 0) };
                item.accel_x=ReadSingle(payload,8); item.accel_y=ReadSingle(payload,12); item.accel_z=ReadSingle(payload,16);
                item.gyro_x=ReadSingle(payload,20); item.gyro_y=ReadSingle(payload,24); item.gyro_z=ReadSingle(payload,28);
                item.mag_x=ReadSingle(payload,32); item.mag_y=ReadSingle(payload,36); item.mag_z=ReadSingle(payload,40);
                item.abs_pressure=ReadSingle(payload,44); item.pressure_alt=ReadSingle(payload,48); item.temperature=ReadSingle(payload,52);
                value = item; return true;
            }

            if (typeId == HILGPSTypeId)
            {
                if (!HasZeroBytes(payload, 9, 3) || !HasZeroBytes(payload, 39, 1)) return false;
                value = new HILGPS { timestamp=ReadInt64(payload,0), fix_type=payload[8], lat=ReadInt32(payload,12), lon=ReadInt32(payload,16), alt=ReadInt32(payload,20), eph=ReadUInt16(payload,24), epv=ReadUInt16(payload,26), vel=ReadUInt16(payload,28), vn=ReadInt16(payload,30), ve=ReadInt16(payload,32), vd=ReadInt16(payload,34), cog=ReadUInt16(payload,36), satellites_visible=payload[38] };
                return true;
            }

            if (typeId == HILStateQuaternionTypeId)
            {
                if (!HasZeroBytes(payload, 54, 2)) return false;
                var item = new HILStateQuaternion { timestamp=ReadInt64(payload,0), rollspeed=ReadSingle(payload,24), yawspeed=ReadSingle(payload,28), pitchspeed=ReadSingle(payload,32), lat=ReadInt32(payload,36), lon=ReadInt32(payload,40), alt=ReadInt32(payload,44), vn=ReadInt16(payload,48), ve=ReadInt16(payload,50), vd=ReadInt16(payload,52) };
                for (var i=0;i<4;i++) item.SetOrientation(i,ReadSingle(payload,8+i*4));
                value=item; return true;
            }

            if (typeId == HILOpticalFlowTypeId)
            {
                if (!HasZeroBytes(payload,18,2) || !HasZeroBytes(payload,49,3) || !HasZeroBytes(payload,60,4)) return false;
                value = new HILOpticalFlow { timestamp=ReadInt64(payload,0), time_usec=ReadUInt64(payload,8), sensor_id=ReadUInt16(payload,16), integration_time_us=ReadSingle(payload,20), integrated_x=ReadSingle(payload,24), integrated_y=ReadSingle(payload,28), integrated_xgyro=ReadSingle(payload,32), integrated_ygyro=ReadSingle(payload,36), integrated_zgyro=ReadSingle(payload,40), temperature=ReadUInt32(payload,44), quality=payload[48], time_delta_distance_us=ReadSingle(payload,52), distance=ReadSingle(payload,56) };
                return true;
            }

            if (typeId == LaserScan256TypeId)
            {
                var item = new LaserScan256 { angleMin=ReadSingle(payload,1024), angleMax=ReadSingle(payload,1028), angleIncrement=ReadSingle(payload,1032), rangeMin=ReadSingle(payload,1036), rangeMax=ReadSingle(payload,1040), timestamp=ReadInt64(payload,1044) };
                for(var i=0;i<256;i++) item.SetRange(i,ReadSingle(payload,i*4));
                value=item; return true;
            }

            if (typeId == ActuatorInputsTypeId)
            {
                var item = new ActuatorInputs { timestamp=ReadInt64(payload,0), size=ReadInt32(payload,8) };
                for(var i=0;i<16;i++) item.SetInput(i,ReadSingle(payload,12+i*4));
                value=item; return true;
            }

            if (typeId == ArmCommandTypeId)
            {
                if (!HasZeroBytes(payload,9,7)) return false;
                value=new ArmCommand { timestamp=ReadInt64(payload,0), arm=payload[8] }; return true;
            }

            if (typeId == FlightModeCommandTypeId)
            {
                value=new FlightModeCommand { timestamp=ReadInt64(payload,0), mode=ReadInt32(payload,8), sub_mode=ReadInt32(payload,12) }; return true;
            }

            if (typeId == MotorInputTypeId)
            {
                if (!HasZeroBytes(payload,12,4)) return false;
                value=new MotorInput { timestamp=ReadInt64(payload,0), input=ReadSingle(payload,8) }; return true;
            }

            if (typeId == RCChannelsInputTypeId)
            {
                if (!HasZeroBytes(payload,45,3)) return false;
                var item=new RCChannelsInput { timestamp=ReadInt64(payload,0), chancount=payload[44] };
                for(var i=0;i<18;i++) item.SetChannel(i,ReadUInt16(payload,8+i*2));
                value=item; return true;
            }

            if (typeId == TrajectoryPointTypeId)
            {
                value=new TrajectoryPoint { timestamp=ReadInt64(payload,0), position=new Vector3(ReadSingle(payload,8),ReadSingle(payload,12),ReadSingle(payload,16)), targetVelocity=ReadSingle(payload,20) };
                return true;
            }

            return false;
        }

        private static bool HasZeroBytes(byte[] source, int offset, int count)
        {
            for (var i = 0; i < count; i++)
                if (source[offset + i] != 0) return false;
            return true;
        }

        private static void WriteInt64(byte[] destination, int offset, long value) =>
            BinaryPrimitives.WriteInt64LittleEndian(destination.AsSpan(offset, 8), value);

        private static void WriteInt32(byte[] destination, int offset, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset, 4), value);

        private static void WriteUInt16(byte[] destination, int offset, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(offset, 2), value);

        private static void WriteInt16(byte[] destination, int offset, short value) =>
            BinaryPrimitives.WriteInt16LittleEndian(destination.AsSpan(offset, 2), value);

        private static void WriteUInt32(byte[] destination, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), value);

        private static void WriteUInt64(byte[] destination, int offset, ulong value) =>
            BinaryPrimitives.WriteUInt64LittleEndian(destination.AsSpan(offset, 8), value);

        private static void WriteSingle(byte[] destination, int offset, float value) =>
            BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset, 4), BitConverter.ToInt32(BitConverter.GetBytes(value), 0));

        private static long ReadInt64(byte[] source, int offset) =>
            BinaryPrimitives.ReadInt64LittleEndian(source.AsSpan(offset, 8));

        private static int ReadInt32(byte[] source, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, 4));

        private static uint ReadUInt32(byte[] source, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset, 4));

        private static ushort ReadUInt16(byte[] source, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset, 2));

        private static short ReadInt16(byte[] source, int offset) =>
            BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset, 2));

        private static ulong ReadUInt64(byte[] source, int offset) =>
            BinaryPrimitives.ReadUInt64LittleEndian(source.AsSpan(offset, 8));

        private static float ReadSingle(byte[] source, int offset) =>
            BitConverter.ToSingle(BitConverter.GetBytes(ReadInt32(source, offset)), 0);
    }
}