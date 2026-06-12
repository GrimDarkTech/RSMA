using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;
public class Maruz : MonoBehaviour
{
    public ArticulationBody motorL = null;
    public ArticulationBody motorR = null;
    public float maxVelocity = 3600.0f;
    public float maxTorque = 20.0f;
    public float zeroLevelVelocity = 40.0f;

    ArticulationDrive drive;
    RSMA.uDTP.Topics.Pose pose;

    private void Start()
    {
        MotorInput data = new MotorInput();
        data.input = 0;

        DataBroker.Publish("MaruzML", data);
        DataBroker.Publish("MaruzMR", data);

        drive = new ArticulationDrive();
        drive.forceLimit = maxTorque;
        drive.damping = 10;

        pose = new RSMA.uDTP.Topics.Pose();
        pose.position = transform.position;
        pose.rotation = transform.rotation;

        DataBroker.Publish("MaruzPose", pose);
    }

    private void Update()
    {
        var data = RSMA.uDTP.DataBroker.GetState<MotorInput>("MaruzML");
        drive.targetVelocity = zeroLevelVelocity + maxVelocity * data.input;
        motorL.xDrive = drive;

        data = RSMA.uDTP.DataBroker.GetState<MotorInput>("MaruzMR");
        drive.targetVelocity = zeroLevelVelocity + maxVelocity * data.input;
        motorR.xDrive = drive;

        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DataBroker.Publish("MaruzPose", pose);
    }
}
