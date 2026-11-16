using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics 
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Float32
    {
        public const int TypeId = 1004;
        public float value;    // offset 0
        public long timestamp; // offset 8, total 16 bytes
    }
}

