using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ArmCommand
    {
        public const int TypeId = 1011;
        public long timestamp; // 8 байт
        public byte arm;       // 1 байт (+7 байт padding = 16 байт)
    }
}