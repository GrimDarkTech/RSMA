using UnityEngine;

public class ModuleBaseScript : MonoBehaviour
{
    private Rigidbody rigidbody;
    private FixedJoint fixedJoint;
    public Rigidbody connectedBody;
    public float startMass;
    void Start()
    {
        if(!gameObject.TryGetComponent<Rigidbody>(out rigidbody))
            rigidbody = gameObject.AddComponent<Rigidbody>();
        if (!gameObject.TryGetComponent<FixedJoint>(out fixedJoint))
            fixedJoint = gameObject.AddComponent<FixedJoint>();
        if (connectedBody != null)
            setConnectedBody(connectedBody);
        rigidbody.mass = startMass;
    }
    public void setConnectedBody(Rigidbody newConnectedBody)
    {
        fixedJoint.connectedBody = newConnectedBody;
    }

}
