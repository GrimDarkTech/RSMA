using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct CameraFramePacket
    {
        public int width;           // Ширина кадра (пиксели)
        public int height;          // Высота кадра (пиксели)
        public int channels;        // Количество каналов (3 для RGB24)
        public long timestamp;    // Mетка времени (Unix time в мс или Time.timeAsDouble)
        public uint frameSequence;  // Номер кадра (для проверки потерь)
        public byte[] pixelData;    // Сырые байты RGB24 (длина = width * height * 3)
    }
}