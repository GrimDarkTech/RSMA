using System.Runtime.InteropServices;

namespace RSMA.uDTP.Topics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TopicHeader
    {
        public long timestamp;      // Метка времени
        public uint sequence;       // Порядковый номер
        public int payloadSize;     // Размер бинарного тела (в байтах)
        public int topicTypeId;     // Уникальный ID типа (например, 1 = Pose, 2 = Camera, 3 = MotorInput)
    }
}