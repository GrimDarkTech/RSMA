using UnityEngine;
/// <summary>
/// Connects two Rigidbody with fixed joint
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_fixed_joints.md")]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FixedJoint))]
public class RSMAFixed : MonoBehaviour
{
    private Rigidbody moduleRigidbody;
    private FixedJoint fixedJoint;

    /// <summary>
    /// The body that is attached. If the body is not specified, the object will be bound to a point in space
    /// </summary>
    public Rigidbody connectedBody;
    private void Start()
    {
        moduleRigidbody = GetComponent<Rigidbody>();
        fixedJoint = GetComponent<FixedJoint>();

        if(connectedBody != null)
        { 
            fixedJoint.connectedBody = connectedBody; 
        }
        else
        {
            Debug.LogError("ConnectedBody (RSMAFixed in" + gameObject.name + ") is null.");
        }
    }
}