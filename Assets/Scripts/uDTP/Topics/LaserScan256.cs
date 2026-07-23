using UnityEngine;

namespace RSMA.uDTP.Topics 
{
    public struct LaserScan256
    {
        public float[] ranges;
        public float angleMin;
        public float angleMax;
        public float angleIncrement;
        public float rangeMin;
        public float rangeMax;
        public long timestamp;
    }
}

