using UnityEngine;

/// <summary>
/// Simulates the behavior of the spring connection.
/// Adaptive component that works with both Rigidbody (ConfigurableJoint) and ArticulationBody (PrismaticJoint).
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_spring_joints.md")]
public class RSMASpring : RSMAHybridJoint
{
    private ConfigurableJoint _joint;

    [Header("Common Settings")]
    /// <summary>
    /// The axis of movement of the stock in local coordinates
    /// </summary>
    public CoordinateAxis stockAxis = CoordinateAxis.z;

    /// <summary>
    /// Determines the free stroke of the stock (meters)
    /// </summary>
    [Min(0)]
    public float stockFreeStroke = 0.1f;

    /// <summary>
    /// Spring elasticity coefficient (stiffness)
    /// </summary>
    public float elasticity = 1.0f;

    /// <summary>
    /// Spring damping coefficient
    /// </summary>
    public float damping = 1.0f;

    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 anchor;

    /// <summary>
    /// If True, draws anchors position with spheres and axis with lines
    /// </summary>
    public bool isDrawAnchors = false;

    [Header("Rigidbody Specific")]
    /// <summary>
    /// Body connected to joint
    /// </summary>
    public Rigidbody connectedBody;
    public ArticulationBody connectedArticulationBody;

    /// <summary>
    /// If True, resets the Anchor according to the anchor and connectedAnchor fields
    /// </summary>
    public bool isResetAnchor;

    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 connectedAnchor;

    private void Start()
    {
        if (IsArticulation)
        {
            InitializeArticulation();
        }
        else
        {
            InitializeRigidbody();
        }
    }

    private void InitializeArticulation()
    {
        if (transform.parent == null || transform.parent.GetComponentInParent<ArticulationBody>() == null)
        {
            Debug.LogWarning($"[RSMASpring] {gameObject.name} является корнем. PrismaticJoint не может быть применен к Root.");
            return;
        }

        // 1. Устанавливаем тип — PrismaticJoint (линейное скольжение вдоль X)
        ArticulationBody.jointType = ArticulationJointType.PrismaticJoint;

        // 2. Позиция анкера
        ArticulationBody.anchorPosition = anchor;

        // 3. Ориентация оси. Разворачиваем локальную ось X сустава вдоль выбранной CoordinateAxis
        Vector3 localDirection = AxisToLocalVector(stockAxis);
        ArticulationBody.anchorRotation = Quaternion.FromToRotation(Vector3.right, localDirection);

        // 4. Настройка пружины и лимитов (В ArticulationBody это делается через ArticulationDrive)
        // Для линейного скольжения лимиты и приводы настраиваются на первой оси xDrive
        ArticulationDrive linearDrive = new ArticulationDrive();

        // Задаем физику пружины
        linearDrive.stiffness = elasticity;
        linearDrive.damping = damping;
        linearDrive.forceLimit = float.MaxValue; // Аналог максимальной силы в ConfigurableJoint

        // Задаем лимиты перемещения. В отличие от ConfigurableJoint, где лимит симметричен в обе стороны от центра,
        // у ArticulationBody мы жестко задаем нижнюю и верхнюю границы в метрах.
        // Настроим центрированную работу (как было в ConfigurableJoint):
        linearDrive.lowerLimit = -stockFreeStroke;
        linearDrive.upperLimit = stockFreeStroke;

        // Целевая позиция пружины (по умолчанию 0 — центр)
        linearDrive.target = 0f;

        // Применяем настройки к линейной оси X
        ArticulationBody.xDrive = linearDrive;
    }

    private void InitializeRigidbody()
    {
        _joint = gameObject.AddComponent<ConfigurableJoint>();

        if (connectedBody != null)
        {
            _joint.connectedBody = connectedBody;
        }
        else if (connectedArticulationBody != null)
        {
            _joint.connectedArticulationBody = connectedArticulationBody;
        }
        else
        {
            Debug.LogWarning($"[RSMA Spring] ConnectedBody в {gameObject.name} не задан. Объект зафиксирован в пространстве.");
        }

            SetupJointMotion();

        SoftJointLimit linearLimits = new SoftJointLimit();
        linearLimits.limit = stockFreeStroke;
        _joint.linearLimit = linearLimits;

        // Смещение таргета пружины
        _joint.targetPosition = -1 * AxisToLocalVector(stockAxis) * stockFreeStroke;

        JointDrive drive = new JointDrive();
        drive.positionSpring = elasticity;
        drive.positionDamper = damping;
        drive.maximumForce = float.MaxValue;

        switch (stockAxis)
        {
            case CoordinateAxis.x: _joint.xDrive = drive; break;
            case CoordinateAxis.y: _joint.yDrive = drive; break;
            case CoordinateAxis.z: _joint.zDrive = drive; break;
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
        return axis switch
        {
            CoordinateAxis.x => Vector3.right,
            CoordinateAxis.y => Vector3.up,
            CoordinateAxis.z => Vector3.forward,
            _ => Vector3.forward
        };
    }

    private void SetupJointMotion()
    {
        _joint.angularXMotion = ConfigurableJointMotion.Locked;
        _joint.angularYMotion = ConfigurableJointMotion.Locked;
        _joint.angularZMotion = ConfigurableJointMotion.Locked;

        _joint.xMotion = (stockAxis == CoordinateAxis.x) ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
        _joint.yMotion = (stockAxis == CoordinateAxis.y) ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
        _joint.zMotion = (stockAxis == CoordinateAxis.z) ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
    }

    private void OnDrawGizmos()
    {
        if (!isDrawAnchors) return;

        Vector3 localDir = AxisToLocalVector(stockAxis);
        Vector3 globalAxis = transform.TransformDirection(localDir);

        Gizmos.color = Color.red;
        Vector3 globalAnchor = transform.TransformPoint(anchor);
        Gizmos.DrawRay(globalAnchor, globalAxis * 0.05f);
        Gizmos.DrawSphere(globalAnchor, 0.002f);

        if (!IsArticulation && connectedBody != null)
        {
            Gizmos.color = Color.green;
            Vector3 globalConnectedAnchor = connectedBody.transform.TransformPoint(connectedAnchor);
            Gizmos.DrawRay(globalConnectedAnchor, globalAxis * 0.05f);
            Gizmos.DrawSphere(globalConnectedAnchor, 0.002f);
        }
    }
}