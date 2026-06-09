using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
public class MaruzVelocity : MonoBehaviour
{
    public bool manualMode = false;
    [Range(-1, 1)]
    public float velocityL = 0.0f;
    [Range(-1, 1)]
    public float velocityR = 0.0f;

    public float angularVelocity = 0.0f;
    public float linearVelocity = 0.0f;

    private MotorInput motorData;
    private RobotVelocity velocityData;

    private void Start()
    {
        motorData = new MotorInput();

        motorData.input = velocityL;
        RSMA.uDTP.DataBroker.Publish("MaruzML", motorData);

        motorData.input = velocityR;
        RSMA.uDTP.DataBroker.Publish("MaruzMR", motorData);

        velocityData.linearVelocity = 0;
        velocityData.angularVelocity = 0;
        RSMA.uDTP.DataBroker.Publish("MaruzTargetVelocity", velocityData);
    }

    private void Update()
    {
        if (manualMode)
        {
            motorData.input = velocityL;
            RSMA.uDTP.DataBroker.Publish("MaruzML", motorData);

            motorData.input = velocityR;
            RSMA.uDTP.DataBroker.Publish("MaruzMR", motorData);
        }
        else 
        {
            velocityData.linearVelocity = linearVelocity;
            velocityData.angularVelocity = angularVelocity;
            RSMA.uDTP.DataBroker.Publish("MaruzTargetVelocity", velocityData);
        }
    }
}
