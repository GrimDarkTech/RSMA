using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class MotorBase : MonoBehaviour
{
    private HingeJoint motorHingeJoint;
    private JointMotor hingeJointMotor;
    private JointSpring hingeJointSpring;
    private float input = 0;

    public Rigidbody rotor;
    public MotorDriver motorDriver;
    public Vector3 startMotorAxis;
    
    public float maxVelocity = 1;
    public float maxForce = 1;
    public float springForce = 1;
    public float springDumper = 1;

    void Start()
    {
        MotorInit();
        SetMotorAxis(startMotorAxis);
        SetMotorAnchor(new Vector3(0, 0, 0));
        SetMotorActive(true);
        SetSpringActive(true);
        SetRotor(rotor);
        SetVelocity(360);
        SetForce(maxForce);
        SetSpringForce(springForce);
        SetSpringDamper(springDumper);
    }
    void FixedUpdate()
    {
        input = motorDriver.getOutput();
        SetVelocity(maxVelocity * input);
        SetForce(maxForce);
        SetSpringForce(springForce);
        SetSpringDamper(springDumper);
    }

    public void MotorInit()
    {
        motorHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    public void SetMotorAxis(Vector3 newMotorAxis)
    {
        motorHingeJoint.axis = newMotorAxis;
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
    public void SetMotorActive(bool isMotorActive)
    {
        motorHingeJoint.useMotor = isMotorActive;
    }
    public void SetSpringActive(bool isSpringActive)
    {
        motorHingeJoint.useSpring = isSpringActive;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        motorHingeJoint.connectedBody = newRotor;
    }
    public void SetVelocity(float newVelocity)
    {
        hingeJointMotor.targetVelocity = newVelocity;
        motorHingeJoint.motor = hingeJointMotor;
    }
    public void SetForce(float newForce)
    {
        hingeJointMotor.force = newForce;
        motorHingeJoint.motor = hingeJointMotor;
    }
    public void SetSpringForce(float newSpringForce)
    {
        hingeJointSpring.spring = newSpringForce;
        motorHingeJoint.spring = hingeJointSpring;
    }
    public void SetSpringDamper(float newSpringDamper)
    {
        hingeJointSpring.damper = newSpringDamper;
        motorHingeJoint.spring = hingeJointSpring;
    }
}
