using UnityEngine;

/// <summary>
/// Converts the incoming flow of rotational energy into one outgoing flow by changing the torque of the output shaft and the angular velocity
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_mechanical_gears.md")]
public class Gearbox : MonoBehaviour, IRotationPowered
{
    /// <summary>
    /// Gear ratio of the gearbox
    /// </summary>
    public float ratio;
    /// <summary>
    /// Body connected to output of the gearbox
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Braking factor
    /// </summary>
    public float brakingFactor = 0.01f;
    /// <summary>
    /// Axis of output of the gearbox
    /// </summary>
    public Vector3 gearboxAxis;
    /// <summary>
    /// If True, resets anchors with anchor and connectedAnchor
    /// </summary>
    public bool isResetAnchor;
    /// <summary>
    /// Anchor position
    /// </summary>
    public Vector3 anchor;
    /// <summary>
    /// Connected body anchor position
    /// </summary>
    public Vector3 connectedAnchor;
    /// <summary>
    /// Max value of angular velocity for connected body
    /// </summary>
    public float maxAngularVelocity = 523f;
    /// <summary>
    /// If True, draws anchors position with spheres and axis with lines
    /// </summary>
    public bool isDrawAnchors = false;
    /// <summary>
    /// Value of the torque of rotation of the output shaft
    /// </summary>
    public Vector3 outputTorque;


    private HingeJoint _hingeJoint;

    /// <summary>
    /// Body of gearbox
    /// </summary>
    public Rigidbody rigidbody { get; set; }
    /// <summary>
    /// Value of the angular velocity of rotation of the input shaft
    /// </summary>
    public float inputAngularVelocity { get; set; }
    /// <summary>
    /// Value of the torque of rotation of the input shaft
    /// </summary>
    public float inputTorque { get; set; }


    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        _hingeJoint = GetComponent<HingeJoint>();

        _hingeJoint.axis = gearboxAxis;


        if (isResetAnchor)
        {
            _hingeJoint.autoConfigureConnectedAnchor = false;
            _hingeJoint.anchor = anchor;
            _hingeJoint.connectedAnchor = connectedAnchor;
        }

        _hingeJoint.connectedBody = connectedBody;

        connectedBody.maxAngularVelocity = maxAngularVelocity;

        _hingeJoint.useSpring = true;

        JointSpring spring = new JointSpring();
        spring.damper = brakingFactor;
        _hingeJoint.spring = spring;
    }
    private void FixedUpdate()
    {
        connectedBody.AddRelativeTorque(gearboxAxis * inputTorque * ratio);

        outputTorque = gearboxAxis * inputTorque * ratio;

        inputAngularVelocity = (connectedBody.angularVelocity - rigidbody.angularVelocity).magnitude * ratio;
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(anchor), (transform.right * gearboxAxis.x + transform.up * gearboxAxis.y + transform.forward * gearboxAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(anchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), (transform.right * gearboxAxis.x + transform.up * gearboxAxis.y + transform.forward * gearboxAxis.z) * 0.01f);
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), 0.002f);
        }
    }
}
