using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMALimitSwitch : RSMADataTransferSlave
{
    public Switch LimitSwitchLever;

    public override string SendData()
    {
        return $"{LimitSwitchLever.MechState}";
    }
}
