using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct HILGPS
    {
        public long timestamp;          // 8 байт
        public byte fix_type;           // 1 байт (+3 байта padding)
        public int lat;                 // 4 байта
        public int lon;                 // 4 байта
        public int alt;                 // 4 байта
        public ushort eph;              // 2 байта
        public ushort epv;              // 2 байта
        public ushort vel;              // 2 байта
        public short vn;                // 2 байта
        public short ve;                // 2 байта
        public short vd;                // 2 байта
        public ushort cog;              // 2 байта
        public byte satellites_visible; // 1 байт (+3 байта padding = 40 байт)
    }
}