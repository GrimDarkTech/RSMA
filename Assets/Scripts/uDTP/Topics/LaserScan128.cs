using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct LaserScan128
    {
        public fixed float ranges[128]; // 512 байт
        public float angleMin;          // 4 байта
        public float angleMax;          // 4 байта
        public float angleIncrement;    // 4 байта
        public float rangeMin;          // 4 байта
        public float rangeMax;          // 4 байта
        public long timestamp;          // 8 байт -> Итого: 540 байт

        public float GetRange(int index)
        {
            if (index < 0 || index >= 128) return 0f;
            fixed (float* p = ranges) return p[index];
        }

        public void SetRange(int index, float value)
        {
            if (index < 0 || index >= 128)
            {
                return;
            }

            fixed (float* p = ranges)
            {
                p[index] = value;
            }
        }
    }
}