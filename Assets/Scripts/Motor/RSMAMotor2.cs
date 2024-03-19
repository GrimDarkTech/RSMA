using System.ComponentModel.Design.Serialization;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of DC electric motor
/// </summary>

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class RSMAMotor2 : MonoBehaviour
{
    private HingeJoint _hingeJoint;

    private Rigidbody _rigidbody;

    private Rigidbody _rotor;

    protected float input = 0;

    public AnimationCurve mechanicalCharacteristics;

    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public GameObject connectedBody;
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

    private void Start()
    {
        _rigidbody= GetComponent<Rigidbody>();
        _hingeJoint= GetComponent<HingeJoint>();

        _hingeJoint.axis = motorAxis;


        if (isResetMotorAnchor)
        {
            _hingeJoint.anchor = motorAnchor;
            _hingeJoint.autoConfigureConnectedAnchor = false;
            _hingeJoint.connectedAnchor = connectedAnchor;
            _hingeJoint.autoConfigureConnectedAnchor = true;
        }

        _rotor = connectedBody.GetComponent<IRotationPowered>().rigidbody;
        _hingeJoint.connectedBody = _rotor;
    }
    private void FixedUpdate()
    {
        if(motorDriver != null)
        {
            input = motorDriver.getOutput();
        }


        float rotorAngularVelocity = (_rigidbody.angularVelocity - _rotor.angularVelocity).magnitude;

        float torque = mechanicalCharacteristics.Evaluate(rotorAngularVelocity);

        _rotor.AddTorque(motorAxis * torque * input);

        _rigidbody.AddRelativeTorque(- motorAxis * torque * input);
    }
}
