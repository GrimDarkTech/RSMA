using UnityEngine;

/// <summary>
/// Simulates the behavior of the axial connection. The hinge joint is used to simulate the interaction of two rigid bodies
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_spring_joints.md")]
public class RSMASpring : MonoBehaviour
{
    private ConfigurableJoint _joint;

    /// <summary>
    /// Body connected to joint
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Connection axis direction relative local transfrom
    /// </summary>
    public Vector3 axis;
    /// <summary>
    /// Spring relaxed position relative local transfrom
    /// </summary>
    public Vector3 relaxedPosition;
    /// <summary>
    /// Spring elasticity coefficient
    /// </summary>
    public float elasticity = 1.0f;
    /// <summary>
    /// Spring damping coefficient
    /// </summary>
    public float damping = 1.0f;
    /// <summary>
    /// If True, resets the Anchor according to the anchor and connectedAnchor fields
    /// </summary>
    public bool isResetAnchor;
    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 anchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 connectedAnchor;
    /// <summary>
    /// If True, draws anchors position with spheres and axis with lines
    /// </summary>
    public bool isDrawAnchors = false;

    private void Start()
    {
        _joint = gameObject.AddComponent<ConfigurableJoint>();

        _joint.axis = axis;

        _joint.connectedBody = connectedBody;

        _joint.angularXMotion = ConfigurableJointMotion.Locked;
        _joint.angularYMotion = ConfigurableJointMotion.Locked;
        _joint.angularZMotion = ConfigurableJointMotion.Locked;

        _joint.xMotion = ConfigurableJointMotion.Free;
        _joint.yMotion = ConfigurableJointMotion.Free;
        _joint.zMotion = ConfigurableJointMotion.Free;

        _joint.targetPosition = relaxedPosition;

        JointDrive drive = new JointDrive();
        drive.positionSpring = elasticity;
        drive.positionDamper = damping;

        _joint.xDrive = drive;
        _joint.yDrive = drive;
        _joint.zDrive = drive;

        if (isResetAnchor)
        {
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = anchor;
            _joint.connectedAnchor = connectedAnchor;
        }
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

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(relaxedPosition), 0.0025f);
        }
    }
}
