using System;
using UnityEngine;

namespace RSMA.uDTP.Topics
{
    [Serializable]
    public struct FlightModeCommand
    {
        public long timestamp;
        public int mode;
        public int sub_mode;
    }
}