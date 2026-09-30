using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics 
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct MotorInput
    {
        public long timestamp; // 8 байт
        public float input;    // 4 байта (+4 байта padding = 16 байт)
    }
}

