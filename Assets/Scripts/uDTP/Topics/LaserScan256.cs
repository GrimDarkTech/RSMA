using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct LaserScan256
    {
        public fixed float ranges[256]; // 1024 байта
        public float angleMin;
        public float angleMax;
        public float angleIncrement;
        public float rangeMin;
        public float rangeMax;
        public long timestamp;          // Итого: 1052 байта

        public float GetRange(int index)
        {
            if (index < 0 || index >= 256) return 0f;
            fixed (float* p = ranges) return p[index];
        }

        public void SetRange(int index, float value)
        {
            if (index < 0 || index >= 256)
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