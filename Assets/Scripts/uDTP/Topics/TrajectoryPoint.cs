using System;
using UnityEngine;

namespace RSMA.uDTP.Topics 
{
    [Serializable]
    public struct TrajectoryPoint
    {
        public Vector3 position;
        public float targetVelocity;
    }
}


