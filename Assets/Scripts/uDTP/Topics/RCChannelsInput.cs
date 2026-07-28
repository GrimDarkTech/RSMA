using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct RCChannelsInput
    {
        public long timestamp;
        public ushort[] channels; // Массив PWM от 1000 до 2000 (обычно 18 каналов)
        public byte chancount;
    }
}