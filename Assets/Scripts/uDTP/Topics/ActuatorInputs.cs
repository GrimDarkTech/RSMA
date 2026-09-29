using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct ActuatorInputs
    {
        public long timestamp;      // 8 байт
        public int size;            // 4 байта
        public fixed float inputs[16]; // 64 байта непрерывного массива

        public float GetInput(int index)
        {
            if (index < 0 || index >= 16) return 0f;
            fixed (float* p = inputs)
            {
                return p[index];
            }
        }
    }
}