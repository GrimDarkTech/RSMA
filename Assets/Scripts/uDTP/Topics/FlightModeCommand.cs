using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct FlightModeCommand
    {
        public long timestamp; // 8 байт
        public int mode;       // 4 байта
        public int sub_mode;   // 4 байта -> 16 байт
    }
}