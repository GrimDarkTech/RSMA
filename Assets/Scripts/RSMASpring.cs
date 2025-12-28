using UnityEngine;

/// <summary>
/// Simulates the behavior of the spring connection
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

        SetupJointMotion();

        
        SoftJointLimit linearLimits = new SoftJointLimit();

        linearLimits.limit = stockFreeStroke;
        _joint.linearLimit = linearLimits;

        _joint.targetPosition = -1 * AxisToLocalVector(stockAxis) * stockFreeStroke;

        JointDrive drive = new JointDrive();
        drive.positionSpring = elasticity;
        drive.positionDamper = damping;
        drive.maximumForce = 3.402822e+38f;

        if (stockAxis == CoordinateAxis.x)
        {
            _joint.xDrive = drive;
        }
        else if (stockAxis == CoordinateAxis.y)
        {
            _joint.yDrive = drive;
        }
        else if (stockAxis == CoordinateAxis.z)
        {
            _joint.zDrive = drive;
        }

        if (isResetAnchor)
        {
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = anchor;
            _joint.connectedAnchor = connectedAnchor;
        }
    }
    private Vector3 AxisToLocalVector(CoordinateAxis axis)
    {
        Vector3 direction = new Vector3();

        if (axis == CoordinateAxis.x)
        {
            direction = new Vector3(1, 0, 0);
        }
        else if (axis == CoordinateAxis.y)
        {
            direction = new Vector3(0, 1, 0);
        }
        else if (axis == CoordinateAxis.z)
        {
            direction = new Vector3(0, 0, 1);
        }

        return direction;
    }

    private void SetupJointMotion()
    {
        _joint.angularXMotion = ConfigurableJointMotion.Locked;
        _joint.angularYMotion = ConfigurableJointMotion.Locked;
        _joint.angularZMotion = ConfigurableJointMotion.Locked;

        if (stockAxis == CoordinateAxis.x)
        {
            _joint.xMotion = ConfigurableJointMotion.Limited;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if (stockAxis == CoordinateAxis.y)
        {
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Limited;
            _joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if (stockAxis == CoordinateAxis.z)
        {
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Limited;
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
            //Gizmos.DrawSphere(transform.TransformPoint(_joint.targetPosition), 0.0025f);
        }
    }
}

