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
    /// The axis of movement of the stock in local coordinates
    /// </summary>
    public CoordinateAxis stockAxis;
    /// <summary>
    /// Determines the free stroke of the stock
    /// </summary>
    [Min(0)]
    public float stockFreeStroke = 0.1f;
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

    private Vector3 axis = new Vector3(0, 0, 1);

    private void Start()
    {
        _joint = gameObject.AddComponent<ConfigurableJoint>();

        _joint.connectedBody = connectedBody;

        _joint.xMotion = ConfigurableJointMotion.Free;
        _joint.yMotion = ConfigurableJointMotion.Free;
        _joint.zMotion = ConfigurableJointMotion.Free;

        SetupJointMotion();

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

    private void SetupJointMotion()
    {
        _joint.angularXMotion = ConfigurableJointMotion.Locked;
        _joint.angularYMotion = ConfigurableJointMotion.Locked;
        _joint.angularZMotion = ConfigurableJointMotion.Locked;

        if (stockAxis == CoordinateAxis.x)
        {
            axis = new Vector3(1, 0, 0);
            _joint.axis = axis;
            _joint.xMotion = ConfigurableJointMotion.Free;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if (stockAxis == CoordinateAxis.y)
        {
            axis = new Vector3(0, 1, 0);
            _joint.axis = axis;
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Free;
            _joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if (stockAxis == CoordinateAxis.z)
        {
            axis = new Vector3(0, 0, 1);
            _joint.axis = axis;
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Free;
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
            Gizmos.DrawSphere(transform.TransformPoint(relaxedPosition), 0.0025f);
        }
    }
}
