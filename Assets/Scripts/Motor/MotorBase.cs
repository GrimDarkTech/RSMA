using UnityEngine;
public class MotorBase : MonoBehaviour
{
    private HingeJoint hingeJoint;
    private JointMotor hingeJointMotor;
    private JointSpring hingeJointSpring;
    private Rigidbody rigidbody;
    public Rigidbody rotor;
    public MotorDriver motorDriver;
    public Vector3 startMotorAxis;
    private float input = 0;
    public float maxVelocity = 1;
    public float maxForce = 1;
    public float springForce = 1;
    public float springDumper = 1;
    public void MotorInit()
    {
        if (!gameObject.TryGetComponent<Rigidbody>(out rigidbody))
            rigidbody = gameObject.AddComponent<Rigidbody>();
        if (!gameObject.TryGetComponent<HingeJoint>(out hingeJoint))
            hingeJoint = gameObject.AddComponent<HingeJoint>();
    }
    public void SetMotorAxis(Vector3 newMotorAxis)
    {
        hingeJoint.axis = newMotorAxis;
    }
    public void SetMotorAxis(float newMotorX, float newMotorY, float newMotorZ)
    {
        Vector3 newMotorAxis = new Vector3(newMotorX, newMotorY, newMotorZ);
        hingeJoint.axis = newMotorAxis;
    }
    public void SetMotorAnchor(Vector3 newMotorAnchor)
    {
        hingeJoint.anchor = newMotorAnchor;
    }
    public void SetMotorActive(bool isMotorActive)
    {
        hingeJoint.useMotor = isMotorActive;
    }
    public void SetSpringActive(bool isSpringActive)
    {
        hingeJoint.useSpring = isSpringActive;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        hingeJoint.connectedBody = newRotor;
    }
    public void SetVelocity(float newVelocity)
    {
        hingeJointMotor.targetVelocity = newVelocity;
        hingeJoint.motor = hingeJointMotor;
    }
    public void SetForce(float newForce)
    {
        hingeJointMotor.force = newForce;
        hingeJoint.motor = hingeJointMotor;
    }
    public void SetSpringForce(float newSpringForce)
    {
        hingeJointSpring.spring = newSpringForce;
        hingeJoint.spring = hingeJointSpring;
    }
    public void SetSpringDamper(float newSpringDamper)
    {
        hingeJointSpring.damper = newSpringDamper;
        hingeJoint.spring = hingeJointSpring;
    }
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
}
