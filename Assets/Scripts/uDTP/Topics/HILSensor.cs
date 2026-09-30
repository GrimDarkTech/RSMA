using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct HILSensor
    {
        public long timestamp;    // 8 байт
        public float accel_x;     // 4 байта
        public float accel_y;     // 4 байта
        public float accel_z;     // 4 байта
        public float gyro_x;      // 4 байта
        public float gyro_y;      // 4 байта
        public float gyro_z;      // 4 байта
        public float mag_x;       // 4 байта
        public float mag_y;       // 4 байта
        public float mag_z;       // 4 байта
        public float abs_pressure;// 4 байта
        public float pressure_alt;// 4 байта
        public float temperature; // 4 байта -> Итого: 56 байт
    }
}