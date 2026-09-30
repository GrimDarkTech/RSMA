using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct RCChannelsInput
    {
        public long timestamp;            // 8 байт
        public fixed ushort channels[18]; // 36 байт
        public byte chancount;            // 1 байт (+3 байта padding = 48 байт)

        public ushort GetChannel(int index)
        {
            if (index < 0 || index >= 18) return 1500;
            fixed (ushort* p = channels) return p[index];
        }

        public void SetChannel(int index, ushort val)
        {
            if (index < 0 || index >= 18) return;
            fixed (ushort* p = channels) p[index] = val;
        }
    }
}