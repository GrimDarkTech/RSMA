using UnityEngine;

/// <summary>
/// Simulates the behavior of the axial connection. 
/// Adaptive component that works with both Rigidbody (HingeJoint) and ArticulationBody (RevoluteJoint).
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_hinge_joints.md")]
public class RSMAHinge : RSMAHybridJoint
{
    private HingeJoint _hingeJoint;

    [Header("Common Settings")]
    /// <summary>
    /// Connection axis direction relative local transform
    /// </summary>
    public Vector3 axis = Vector3.right;

    /// <summary>
    /// Represents the Motor Anchor (Local space for Rigidbody / Parent-child alignment space)
    /// </summary>
    public Vector3 anchor;

    /// <summary>
    /// If True, draws anchors position with spheres and axis with lines
    /// </summary>
    public bool isDrawAnchors = false;

    [Header("Rigidbody Specific")]
    /// <summary>
    /// Body connected to hinge joint
    /// </summary>
    public Rigidbody connectedBody;

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
        // ѕровер€ем, что это не корневой элемент
        if (transform.parent == null || transform.parent.GetComponentInParent<ArticulationBody>() == null)
        {
            Debug.LogWarning($"[RSMAHinge] {gameObject.name} €вл€етс€ корневым (Root) ArticulationBody. Ўарнир Revolute не может быть применен к Root.");
            return;
        }

        // 1. «адаем тип сустава Ч Revolute (одноосевое вращение)
        ArticulationBody.jointType = ArticulationJointType.RevoluteJoint;

        // 2. Ќастраиваем точку прив€зки (Anchor)
        // ¬ ArticulationBody anchorPosition задает положение сустава относительно родительского тела
        ArticulationBody.anchorPosition = anchor;

        // 3. ћаги€ ориентации оси:
        // “ак как ArticulationBody всегда вращаетс€ вокруг своей локальной оси X,
        // нам нужно повернуть сустав так, чтобы его ось X совпала с переданным вектором axis.
        if (axis != Vector3.zero)
        {
            // Ќаходим кватернион поворота от Vector3.right (стандартна€ ось X) к нашей кастомной оси axis
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.right, axis.normalized);
            ArticulationBody.anchorRotation = targetRotation;
        }
    }

    private void InitializeRigidbody()
    {
        // ƒинамически добавл€ем HingeJoint, как в твоем исходном коде
        _hingeJoint = gameObject.AddComponent<HingeJoint>();

        _hingeJoint.axis = axis;
        _hingeJoint.connectedBody = connectedBody;

        if (isResetAnchor)
        {
            _hingeJoint.autoConfigureConnectedAnchor = false;
            _hingeJoint.anchor = anchor;
            _hingeJoint.connectedAnchor = connectedAnchor;
        }
    }

    private void OnDrawGizmos()
    {
        if (!isDrawAnchors) return;

        // –ассчитываем направление глобальной оси дл€ отрисовки
        Vector3 globalAxis = transform.TransformDirection(axis);

        // ќтрисовка основного анкера (работает дл€ обеих систем)
        Gizmos.color = Color.red;
        Vector3 globalAnchor = transform.TransformPoint(anchor);
        Gizmos.DrawRay(globalAnchor, globalAxis * 0.05f); // Ќемного увеличил длину луча дл€ видимости
        Gizmos.DrawSphere(globalAnchor, 0.002f);

        // ќтрисовка второго анкера (только дл€ Rigidbody, если задан connectedBody)
        if (!IsArticulation && connectedBody != null)
        {
            Gizmos.color = Color.green;
            Vector3 globalConnectedAnchor = connectedBody.transform.TransformPoint(connectedAnchor);
            Gizmos.DrawRay(globalConnectedAnchor, globalAxis * 0.05f);
            Gizmos.DrawSphere(globalConnectedAnchor, 0.002f);
        }
    }
}