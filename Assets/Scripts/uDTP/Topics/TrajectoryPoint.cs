using System;
using UnityEngine;

namespace RSMA.uDTP.Topics 
{
    [Serializable]
    public struct TrajectoryPoint
    {
        public long timestamp;
        public Vector3 position;
        public float targetVelocity;
    }
}


