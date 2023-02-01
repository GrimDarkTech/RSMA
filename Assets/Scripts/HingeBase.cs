using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class HingeBase : MonoBehaviour
{
    private HingeJoint axisHingeJoint;


    public Rigidbody connectedBody;
    public Vector3 axis;



    void Start()
    {
        MotorInit();
        SetAxis(axis);
        SetMotorAnchor(new Vector3(0, 0, 0));
        SetRotor(connectedBody);

    }
    public void MotorInit()
    {
        axisHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    public void SetAxis(Vector3 newAxis)
    {
        axisHingeJoint.axis = newAxis;
    }
    public void SetAxis(float newX, float newY, float newZ)
    {
        Vector3 newMotorAxis = new Vector3(newX, newY, newZ);
        axisHingeJoint.axis = newMotorAxis;
    }
    public void SetMotorAnchor(Vector3 newMotorAnchor)
    {
        axisHingeJoint.anchor = newMotorAnchor;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        axisHingeJoint.connectedBody = newRotor;
    }
}
