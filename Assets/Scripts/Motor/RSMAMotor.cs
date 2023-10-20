using UnityEngine;

/// <summary>
/// Implements properties and functionality of DC electric motor
/// </summary>

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class RSMAMotor : MonoBehaviour
{
    protected HingeJoint motorHingeJoint;
    protected Rigidbody motorRigidbody;

    protected float input = 0;

    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public Rigidbody rotor;
    /// <summary>
    /// Driver connected to motor
    /// </summary>
    public RSMAMotorDriver motorDriver;
    /// <summary>
    /// Represents the motor axis, emanating from origin
    /// </summary>
    public Vector3 motorAxis;
    /// <summary>
    /// Resets MotorAnchor with startMotorAnchor
    /// </summary>
    public bool isResetMotorAnchor;

    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 motorAnchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 connectedAnchor;
    /// <summary>
    /// Maximum torque of motor
    /// </summary>
    public float torque = 1;
    /// <summary>
    /// Maximum angle velocity of motor in degrees per second
    /// </summary>
    public float angularVelocity = 3600;

    private void Start()
    {
        MotorInit();
        motorHingeJoint.axis = motorAxis;
        if (isResetMotorAnchor)
        {
            motorHingeJoint.anchor = motorAnchor;
            motorHingeJoint.autoConfigureConnectedAnchor = false;
            motorHingeJoint.connectedAnchor = connectedAnchor;
            motorHingeJoint.autoConfigureConnectedAnchor = true;
        }
        motorHingeJoint.connectedBody = rotor;
        motorHingeJoint.useMotor = true;
    }
    private void FixedUpdate()
    {
        if(motorDriver != null)
        {
            input = motorDriver.getOutput();
        }
        var motor = motorHingeJoint.motor;
        motor.force = torque;
        motor.targetVelocity = - angularVelocity * input;
        motor.freeSpin = false;
        motorHingeJoint.motor = motor;
    }
    private void MotorInit()
    {
        motorRigidbody = gameObject.GetComponent<Rigidbody>();
        motorHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
}
