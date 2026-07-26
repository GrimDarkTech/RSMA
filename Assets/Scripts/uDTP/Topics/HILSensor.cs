using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct HILSensor
    {
        public long timestamp;

        public float accel_x;
        public float accel_y;
        public float accel_z;

        public float gyro_x;
        public float gyro_y;
        public float gyro_z;

        public float mag_x;
        public float mag_y;
        public float mag_z;

        public float abs_pressure;
        public float pressure_alt;
        public float temperature;
    }
}