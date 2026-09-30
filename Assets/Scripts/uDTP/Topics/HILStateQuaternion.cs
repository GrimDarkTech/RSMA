using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct HILStateQuaternion
    {
        public long timestamp;           // 8 байт
        public fixed float orientation[4]; // 16 байт [w, x, y, z]
        public float rollspeed;          // 4 байта
        public float yawspeed;           // 4 байта
        public float pitchspeed;         // 4 байта
        public int lat;                  // 4 байта
        public int lon;                  // 4 байта
        public int alt;                  // 4 байта
        public short vn;                 // 2 байта
        public short ve;                 // 2 байта
        public short vd;                 // 2 байта (плюс 2 байта padding в конце = 48 байт)

        public float GetOrientation(int index)
        {
            if (index < 0 || index >= 4) return 0f;
            fixed (float* p = orientation) return p[index];
        }

        public void SetOrientation(int index, float val)
        {
            if (index < 0 || index >= 4) return;
            fixed (float* p = orientation) p[index] = val;
        }
    }
}