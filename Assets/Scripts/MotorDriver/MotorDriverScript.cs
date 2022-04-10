using UnityEngine;

public class MotorDriverScript : MonoBehaviour
{
    public int portIn1Id;
    public int portIn2Id;
    public int portSpeedId;
    private float portIn1;
    private float portIn2;
    private float portSpeed;
    private float output = 0;
    public GPIOBaseScript GPIOScript;
    public float getOutput()
    {
        return output;
    }
    int boolToInt(bool boolValue)
    {
        if (boolValue)
            return 1;
        else
            return 0;
    }
    void Update()
    {
        portIn1 =  boolToInt(GPIOScript.getDigitalPort(portIn1Id));
        portIn2 = boolToInt(GPIOScript.getDigitalPort(portIn2Id));
        portSpeed = GPIOScript.getPWMPort(portSpeedId);
        output = portSpeed * (portIn1 - portIn2);
    }
}
