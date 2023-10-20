using UnityEngine;
/// <summary>
/// Simulates the behavior of the axial connection. The hinge joint is used to simulate the interaction of two rigid bodies
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class RSMAHinge : MonoBehaviour
{
    private HingeJoint axisHingeJoint;

    /// <summary>
    /// Body connected to joint
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Connection axis direction relative local transfrom
    /// </summary>
    public Vector3 axis;



    void Start()
    {
        Init();
        SetAxis(axis);
        axisHingeJoint.anchor= Vector3.zero;
        axisHingeJoint.connectedBody= connectedBody;
    }
    /// <summary>
    /// Sets up joint and gets refernce
    /// </summary>
    public void Init()
    {
        axisHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    /// <summary>
    /// Sets axis
    /// </summary>
    /// <param name="axis">axis in relative tranform</param>
    public void SetAxis(Vector3 axis)
    {
        axisHingeJoint.axis = axis;
    }
}
