using UnityEngine;
/// <summary>
/// Implements properties and functionality of hinge joint
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
    /// Connection axis
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
    /// Inits compoenents
    /// </summary>
    public void Init()
    {
        axisHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    /// <summary>
    /// Sets axis
    /// </summary>
    /// <param name="axis">axis</param>
    public void SetAxis(Vector3 axis)
    {
        axisHingeJoint.axis = axis;
    }
}
