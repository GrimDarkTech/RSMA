using UnityEngine;
/// <summary>
/// Simulates the behavior of the axial connection. The hinge joint is used to simulate the interaction of two rigid bodies
/// </summary>
[RequireComponent(typeof(Rigidbody))]

public class RSMAHinge : MonoBehaviour
{
    private HingeJoint _hingeJoint;

    /// <summary>
    /// Body connected to hinge joint
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Connection axis direction relative local transfrom
    /// </summary>
    public Vector3 axis;
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
