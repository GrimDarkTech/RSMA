using UnityEngine;

public class RSMAMotorDriver : MonoBehaviour
{
    public ConnectedPin connectedPin1;
    public ConnectedPin connectedPin2;
    public ConnectedPin connectedPinPWM;
    private float portIn1;
    private float portIn2;
    private float portSpeed;
    private float output = 0;
    public RSMAGPIO connectMicrocontroller;
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
        portIn1 = connectMicrocontroller.GetPin(connectedPin1).value;
        portIn2 = connectMicrocontroller.GetPin(connectedPin2).value;
        portSpeed = connectMicrocontroller.GetPin(connectedPinPWM).value;
        output = portSpeed * (portIn1 - portIn2);
    }
}
