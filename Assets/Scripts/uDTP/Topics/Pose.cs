using UnityEngine;

namespace RSMA.uDTP.Topics 
{
    public struct Pose
    {
        public const int TypeId = 1002;
        public long timestamp;
        public Vector3 position;
        public Quaternion rotation;
    }
}

