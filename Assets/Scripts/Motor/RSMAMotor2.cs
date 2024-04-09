using UnityEngine;

/// <summary>
/// Implements properties and functionality of DC electric motor
/// </summary>

[RequireComponent(typeof(Rigidbody))]
public class RSMAMotor2 : MonoBehaviour
{
    private HingeJoint _hingeJoint;

    private Rigidbody _rigidbody;

    private Rigidbody _rotor;

    private IRotationPowered rotationPowered;

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
    /// Braking factor
    /// </summary>
    public float brakingFactor = 0.1f;

    public float outputTorque;

    public float maxAngularVelocity = 523f;

    public bool isDrawAnchors = false;

    private void Start()
    {
        _rigidbody= GetComponent<Rigidbody>();

        rotationPowered = connectedBody.GetComponent<IRotationPowered>();

        if (rotationPowered == null)
        {
            _hingeJoint = gameObject.AddComponent<HingeJoint>();
            _rotor = connectedBody.GetComponent<Rigidbody>();
            _hingeJoint.connectedBody = _rotor;

            _hingeJoint.axis = motorAxis;

            if (isResetMotorAnchor)
            {
                _hingeJoint.anchor = motorAnchor;
                _hingeJoint.autoConfigureConnectedAnchor = false;
                _hingeJoint.connectedAnchor = connectedAnchor;
                _hingeJoint.autoConfigureConnectedAnchor = true;
            }

            _hingeJoint.useSpring = true;

            JointSpring spring = new JointSpring();
            spring.damper = brakingFactor;
            _hingeJoint.spring = spring;

            _rotor.maxAngularVelocity = maxAngularVelocity;
        }
    }
    private void FixedUpdate()
    {
        if(motorDriver != null)
        {
            input = motorDriver.getOutput();
        }

        float angularVelocity;
        float torque;

        if (rotationPowered == null)
        {
            angularVelocity = (_rigidbody.angularVelocity - _rotor.angularVelocity).magnitude;
            torque = mechanicalCharacteristics.Evaluate(angularVelocity);

            _rotor.AddRelativeTorque(motorAxis * torque * input);

 //           _rigidbody.AddRelativeTorque(-motorAxis * torque * input);
        }
        else
        {
            angularVelocity = rotationPowered.inputAngularVelocity;
            torque = mechanicalCharacteristics.Evaluate(angularVelocity);

            rotationPowered.inputTorque = torque * input;

 //           _rigidbody.AddRelativeTorque(-motorAxis * torque * input);
        }

        outputTorque = torque * input;
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(motorAnchor), (transform.right * motorAxis.x + transform.up * motorAxis.y + transform.forward * motorAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(motorAnchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), (transform.right * motorAxis.x + transform.up * motorAxis.y + transform.forward * motorAxis.z) * 0.01f);
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), 0.002f);
        }
    }
}
