using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
public class Maruz : MonoBehaviour
{
    public ArticulationBody motorL = null;
    public ArticulationBody motorR = null;
    public float maxVelocity = 3600.0f;
    public float maxTorque = 20.0f;

    ArticulationDrive drive;

    private void Start()
    {
        MotorInput data = new MotorInput();
        data.input = 0;

        RSMA.uDTP.DataBroker.Publish("MaruzML", data);
        RSMA.uDTP.DataBroker.Publish("MaruzMR", data);

        drive = new ArticulationDrive();
        drive.forceLimit = maxTorque;
        drive.damping = 10;
    }

    private void Update()
    {
        var data = RSMA.uDTP.DataBroker.GetState<MotorInput>("MaruzML");
        drive.targetVelocity = maxVelocity * data.input;
        motorL.xDrive = drive;

        data = RSMA.uDTP.DataBroker.GetState<MotorInput>("MaruzMR");
        drive.targetVelocity = maxVelocity * data.input;
        motorR.xDrive = drive;
    }
}
