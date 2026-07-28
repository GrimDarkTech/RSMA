using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct HILOpticalFlow
    {
        public long timestamp;
        public ulong time_usec;
        public ushort sensor_id;
        public float integration_time_us;
        public float integrated_x;
        public float integrated_y;
        public float integrated_xgyro;
        public float integrated_ygyro;
        public float integrated_zgyro;
        public uint temperature;
        public byte quality;
        public float time_delta_distance_us;
        public float distance;
    }
}