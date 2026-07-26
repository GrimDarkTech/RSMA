using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct HILGPS
    {
        public long timestamp;

        public int lat;                // Широта * 1e7 (градусы)
        public int lon;                // Долгота * 1e7 (градусы)
        public int alt;                // Высота над уровнем моря (мм)

        public ushort eph;             // HDOP * 100
        public ushort epv;             // VDOP * 100
        public ushort vel;             // Ground speed (см/с)

        public short vn;               // Скорость Север (см/с)
        public short ve;               // Скорость Восток (см/с)
        public short vd;               // Скорость Вниз (см/с)

        public ushort cog;             // Курс (cdeg, 0..36000)
        public byte satellites_visible;// Число спутников (например, 12)
    }
}