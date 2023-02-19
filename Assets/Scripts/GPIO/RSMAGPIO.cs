using UnityEngine;


public class RSMAGPIO : MonoBehaviour
{
    [SerializeField] private bool[] digitalPort = new bool[500];
    [SerializeField] private float[] PWMPort = new float[500];
   
    public void SetDigitalPort(int portId, bool value)
    {
            if (portId < 500)
                digitalPort[portId] = value;
            else
                Debug.LogError("Value of port id is to big, try value less then 500");
    }
    public bool GetDigitalPort(int portId)
    {
        if (portId < 500)
            return digitalPort[portId];
        else
        {
            Debug.LogError("Value of port id is to big, try value less then 500");
            return false;
        }
    }
    public void SetPWMPort(int portId, float value)
    {
        if (portId < 500)
            if(value > 1)
                PWMPort[portId] = 1;
            else
            {
                if(value < 0)
                    PWMPort[portId] = 0;
                else
                    PWMPort[portId] = value;
            }
        
        else
            Debug.LogError("Value of port id is to big, try value less then 500");
    }
    public float GetPWMPort(int portId)
    {
        if (portId < 500)
            return PWMPort[portId];
        else
        {
            Debug.LogError("Value of port id is to big, try value less then 500");
            return 0;
        }
    }
}
