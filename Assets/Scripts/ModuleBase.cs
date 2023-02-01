using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FixedJoint))]
public class ModuleBase : MonoBehaviour
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
    public void SetConnectedBody(Rigidbody newConnectedBody)
    {
        fixedJoint.connectedBody = newConnectedBody;
    }

}
