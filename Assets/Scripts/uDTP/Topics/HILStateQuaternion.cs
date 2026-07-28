using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct HILStateQuaternion
    {
        public long timestamp;
        public float[] orientation; // [w, x, y, z]
        public float rollspeed;
        public float yawspeed;
        public float pitchspeed;
        public int lat;
        public int lon;
        public int alt;
        public short vn;
        public short ve;
        public short vd;
    }
}