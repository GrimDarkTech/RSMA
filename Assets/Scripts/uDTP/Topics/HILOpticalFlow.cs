using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct HILOpticalFlow
    {
        public long timestamp;              // 8 байт
        public ulong time_usec;             // 8 байт
        public ushort sensor_id;            // 2 байта (+2 байта padding)
        public float integration_time_us;   // 4 байта
        public float integrated_x;          // 4 байта
        public float integrated_y;          // 4 байта
        public float integrated_xgyro;      // 4 байта
        public float integrated_ygyro;      // 4 байта
        public float integrated_zgyro;      // 4 байта
        public uint temperature;            // 4 байта
        public byte quality;                // 1 байт (+3 байта padding)
        public float time_delta_distance_us;// 4 байта
        public float distance;              // 4 байта -> Итого: 64 байта
    }
}