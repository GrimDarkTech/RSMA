using UnityEngine;
/// <summary>
/// Implements properties and functionality of servo motor
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class RSMAServo : MonoBehaviour
{
    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Represents the motor axis, emanating from anchor
    /// </summary>
    public Vector3 axis;
    /// <summary>
    /// Microcontroller GPIO connected to servo
    /// </summary>
    public RSMAGPIO microcontroller;
    /// <summary>
    /// Microcontroller GPIO port pin connected to servo
    /// </summary>
    public ConnectedPin connectedPin;
    /// <summary>
    /// If True, servo has limits
    /// </summary>
    public bool isUseLimits;
    /// <summary>
    /// Angles, that limits servo shaft rotation
    /// </summary>
    public Vector2 limits;
    /// <summary>
    /// Maximum torque value
    /// </summary>
    public float torque = 1;
    /// <summary>
    /// Damping factor
    /// </summary>
    public float damper = 0.5f;
    /// <summary>
    /// If True, resets the Anchor according to the anchor and connectedAnchor fields
    /// </summary>
    public bool isResetAnchor;
    /// <summary>
    /// Represents the motor Anchor position
    /// </summary>
    public Vector3 anchor;
    /// <summary>
    /// Represents the anchor position for connected body
    /// </summary>
    public Vector3 connectedAnchor;
    /// <summary>
    /// If True, draws anchors position with spheres and axis with lines
    /// </summary>
    public bool isDrawAnchors = true;

    protected HingeJoint _hingeJoint;
    protected Rigidbody _rigidbody;

    protected float input = 0;

    private void Start()
    {
        _hingeJoint = gameObject.GetComponent<HingeJoint>();
        _rigidbody = gameObject.GetComponent<Rigidbody>();

        _hingeJoint.axis = axis;
        _hingeJoint.connectedBody = connectedBody;
        _hingeJoint.anchor = anchor;
        _hingeJoint.connectedAnchor = connectedAnchor;

        _hingeJoint.useLimits = isUseLimits;
        var jointLimits = new JointLimits();
        jointLimits.min = limits.x;
        jointLimits.max = limits.y;
        _hingeJoint.limits = jointLimits;

        _hingeJoint.useSpring = true;
        var jointSpring = new JointSpring();
        jointSpring.spring = torque;
        jointSpring.damper = damper;

        _hingeJoint.spring = jointSpring;
    }
    private void FixedUpdate()
    {
        input = microcontroller.GetPin(connectedPin).value;

        var jointSpring = _hingeJoint.spring;
        jointSpring.targetPosition = input * (limits.y - limits.x) + limits.x;
        _hingeJoint.spring = jointSpring;

    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(anchor), (transform.right * axis.x + transform.up * axis.y + transform.forward * axis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(anchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), (transform.right * axis.x + transform.up * axis.y + transform.forward * axis.z) * 0.01f);
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), 0.002f);
        }
    }
}
