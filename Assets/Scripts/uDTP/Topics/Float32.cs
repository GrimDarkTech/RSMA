using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics 
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Float32
    {
        public float value;    // 4 байта (+4 байта padding)
        public long timestamp; // 8 байт -> 16 байт
    }
}

