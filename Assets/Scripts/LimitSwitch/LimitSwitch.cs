using UnityEngine;

/// <summary>
/// Implements the properties and functionality of the limit switch
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_switches.md")]
public class LimitSwitch : RSMADataTransferSlave
{
    /// <summary>
    /// Switch stock
    /// </summary>
    public GameObject stock;
    /// <summary>
    /// The axis of movement of the stock in local coordinates
    /// </summary>
    public CoordinateAxis stockAxis;
    /// <summary>
    /// Determines the free stroke of the stock
    /// </summary>
    [Min(0)]
    public float stockFreeStroke = 0.1f;

    [Space(10)]
    /// <summary>
    /// Switch spring stiffness
    /// </summary>
    public float springStiffness = 1f;
    /// <summary>
    /// Switch damper
    /// </summary>
    public float springDamper = 1f;
    /// <summary>
    /// The maximum force a spring can exert
    /// </summary>
    public float maxForce = 300f;

    [Space(10)]
    /// <summary>
    /// The distance between the anchors, which activates switch
    /// </summary>
    public float triggerDistance = 0.01f;
    /// <summary>
    /// Represents the anchor for switch body
    /// </summary>
    public Vector3 bodyAnchor;
    /// <summary>
    /// Represents the anchor for switch stock
    /// </summary>
    public Vector3 stockAnchor;
    /// <summary>
    /// If True, draws anchors position with spheres
    /// </summary>
    public bool isDrawAnchors = false;

    private Rigidbody _bodyBody;
    [Space(5)]
    [SerializeField]
    private int _state = 0;

    private void Start()
    {
        _bodyBody = gameObject.GetComponent<Rigidbody>();

        if(_bodyBody == null)
        {
            _bodyBody = gameObject.AddComponent<Rigidbody>();
        }

        ConfigurableJoint joint = stock.AddComponent<ConfigurableJoint>();

        joint.connectedBody = _bodyBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = stockAnchor;
        joint.connectedAnchor = bodyAnchor;
        LimitJointMotion(joint, stockAxis);

        SoftJointLimit stockLimit = new SoftJointLimit();
        stockLimit.limit = stockFreeStroke;
        joint.linearLimit = stockLimit;
        joint.targetPosition = -1 * AxisToLocalVector(stock, stockAxis) * stockFreeStroke;
        SetJointDrives(joint, stockAxis);


    }
    private void Update()
    {
        Vector3 distance = stock.gameObject.transform.TransformPoint(stockAnchor) - transform.TransformPoint(bodyAnchor);
        
        if(distance.sqrMagnitude <= Mathf.Pow(triggerDistance, 2))
        {
            _state = 1;
        }
        else
        {
            _state = 0;
        }
    }
    /// <summary>
    /// Sends switch state via datatransfer protocol
    /// </summary>
    /// <returns>Returns 1, if Switch is active(pressed), else returns 0</returns>
    public override string SendData()
    {
        return (_state.ToString());
    }
    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(bodyAnchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(stock.gameObject.transform.TransformPoint(stockAnchor), 0.002f);
        }
    }
    private void LimitJointMotion(ConfigurableJoint joint, CoordinateAxis freeAxis)
    {
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        if (freeAxis == CoordinateAxis.x)
        {
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if (freeAxis == CoordinateAxis.y)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else if(freeAxis == CoordinateAxis.z)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Limited;
        }
    }
    private Vector3 AxisToLocalVector(GameObject target, CoordinateAxis axis)
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
    private void SetJointDrives(ConfigurableJoint joint, CoordinateAxis driveAxis)
    {
        JointDrive drive = new JointDrive();
        drive.positionSpring = springStiffness;
        drive.positionDamper = springDamper;
        drive.maximumForce = maxForce;

        if (driveAxis == CoordinateAxis.x)
        {
            joint.xDrive = drive;
        }
        else if (driveAxis == CoordinateAxis.y)
        {
            joint.yDrive = drive;
        }
        else if (driveAxis == CoordinateAxis.z)
        {
            joint.zDrive = drive;
        }
    }

}
