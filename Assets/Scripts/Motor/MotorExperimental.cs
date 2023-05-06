using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class MotorExperimental : MonoBehaviour
{
    protected HingeJoint motorHingeJoint;
    protected Rigidbody motorRigidbody;
    protected JointSpring spring;

    protected float input = 0;

    public Rigidbody rotor;
    public RSMAMotorDriver motorDriver;
    public Vector3 startMotorAxis;
    
    public float torque = 1;
    public float damper = 0.25f;

    void Start()
    {
        MotorInit();
        SetMotorAxis(startMotorAxis);
        SetMotorAnchor(new Vector3(0, 0, 0));
        SetSpringActive(true);
        SetRotor(rotor);
        SetDamper(damper);
    }
    void FixedUpdate()
    {
        input = motorDriver.getOutput();
        SetTorque(-input * torque);
    }

    public void MotorInit()
    {
        motorRigidbody = gameObject.GetComponent<Rigidbody>();
        motorHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    public void SetMotorAxis(Vector3 newMotorAxis)
    {
        motorHingeJoint.axis = newMotorAxis;
    }
    public Vector3 GetMotorAxis()
    {
        return motorHingeJoint.axis;
    }
    public void SetMotorAxis(float newMotorX, float newMotorY, float newMotorZ)
    {
        Vector3 newMotorAxis = new Vector3(newMotorX, newMotorY, newMotorZ);
        motorHingeJoint.axis = newMotorAxis;
    }
    public void SetMotorAnchor(Vector3 newMotorAnchor)
    {
        motorHingeJoint.anchor = newMotorAnchor;
    }

    public void SetSpringActive(bool isSpringActive)
    {
        motorHingeJoint.useSpring = isSpringActive;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        motorHingeJoint.connectedBody = newRotor;
    }
    public void SetTorque(float torque)
    {
        rotor.AddRelativeTorque(torque * startMotorAxis);
        motorRigidbody.AddTorque(-torque * startMotorAxis);
    }
    public void SetDamper(float newDamper)
    {
        spring.damper = newDamper;
        motorHingeJoint.spring = spring;
    }
}
