using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPIOBaseScript : MonoBehaviour
{
    [SerializeField] private bool[] digitalPort = new bool[1000];

    public void setDigitalPort(int portId, bool value)
    {
            if (portId < 1000)
                digitalPort[portId] = value;
            else
                Debug.LogError("Value of port id is to big, try value less then 1000");
    }
    public bool getDigitalPort(int portId)
    {
        if (portId < 1000)
            return digitalPort[portId];
        else
        {
            Debug.LogError("Value of port id is to big, try value less then 1000");
            return false;
        }
    }
}
