using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct ArmCommand
    {
        public long timestamp;
        public byte arm;
    }
}