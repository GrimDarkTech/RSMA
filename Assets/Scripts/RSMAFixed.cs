using UnityEngine;
using UnityEditor;
/// <summary>
/// Implements properties and functionality of connectable module
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/API/en/RSMAModule.cs.md")]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FixedJoint))]

public class RSMAFixed : MonoBehaviour
{
     
    private Rigidbody moduleRigidbody;
    private FixedJoint fixedJoint;
    public Rigidbody connectedBody;
    void Start()
    {
        moduleRigidbody = GetComponent<Rigidbody>();
        fixedJoint = GetComponent<FixedJoint>();
        if (connectedBody != null)
        {
            SetConnectedBody(connectedBody);
        }
        else
        {
            Debug.LogError("ConnectedBody (ModuleBase in" + gameObject.name + ") is null.");
        }
    }
    /// <summary>
    /// Sets connected rigid body 
    /// </summary>
    /// <param name="connectedBody">Body to connect</param>
    public void SetConnectedBody(Rigidbody connectedBody)
    {
        fixedJoint.connectedBody = connectedBody;
    }

}