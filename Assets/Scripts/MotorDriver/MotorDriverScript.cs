using UnityEngine;

public class MotorDriverScript : MonoBehaviour
{
    public int portIn1Id;
    public int portIn2Id;
    private float portIn1;
    private float portIn2;
    public float output = 0;
    public GPIOBaseScript GPIOScript;
    public float getOutput()
    {
        return output;
    }
    void Update()
    {
        portIn1 = GPIOScript.getPWMPort(portIn1Id);
        portIn2 = GPIOScript.getPWMPort(portIn2Id);
        output = portIn1 - portIn2;
    }
}
