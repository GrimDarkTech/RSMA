using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct ActuatorInputs
    {
        public long timestamp;         // 8 байт (0..7)
        public int size;               // 4 байта (8..11)
        public fixed float inputs[16]; // 64 байта (12..75) -> Итого: 76 байт

        public float GetInput(int index)
        {
            if (index < 0 || index >= 16) return 0f;
            fixed (float* p = inputs) return p[index];
        }

        public void SetInput(int index, float val)
        {
            if (index < 0 || index >= 16) return;
            fixed (float* p = inputs) p[index] = val;
        }
    }
}