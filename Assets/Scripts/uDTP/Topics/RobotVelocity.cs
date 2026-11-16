using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics 
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct RobotVelocity
    {
        public const int TypeId = 1001;
        public long timestamp;      // 8 байт
        public float linearVelocity; // 4 байта
        public float angularVelocity;// 4 байта -> 16 байт
    }
}

