using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class ServoMotor : MonoBehaviour
{

    public Rigidbody rotor;
    public Vector3 startMotorAxis;
    public GPIOBase GPIOScript;
    public Vector2 limits;
    public int portPWMId;
    public float torque = 1;
    public float maxAngle = 180;
    public float damper = 0.5f;
    public bool isUseLimit;

    protected HingeJoint motorHingeJoint;
    protected Rigidbody motorRigidbody;
    protected float input = 0;

    private JointSpring spring;
    private JointLimits limit;

    void Start()
    {
        MotorInit();
        SetMotorAxis(startMotorAxis);
        SetMotorAnchor(new Vector3(0, 0, 0));
        SetSpringActive(true);
        SetLimitsActive(isUseLimit);
        SetRotor(rotor);
    }
    void FixedUpdate()
    {
        input = GPIOScript.GetPWMPort(portPWMId) + 0.005f;
        SetTargetAngle(input * maxAngle);
        SetDamper(damper);
        SetTorque(torque);
        SetLimits();
    }

    public void MotorInit()
    {
        motorHingeJoint = gameObject.GetComponent<HingeJoint>();
        motorRigidbody = gameObject.GetComponent<Rigidbody>();

        spring = motorHingeJoint.spring;
        limit = motorHingeJoint.limits;
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
    public void SetLimitsActive(bool isLimitsActive)
    {
        motorHingeJoint.useLimits = isLimitsActive;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        motorHingeJoint.connectedBody = newRotor;
    }
    public void SetTargetAngle(float targetAngle)
    {
        spring.targetPosition = targetAngle-maxAngle/2;
        motorHingeJoint.spring = spring;
    }
    public void SetTorque(float newTorque)
    {
        spring.spring = newTorque;
        motorHingeJoint.spring = spring;
    }
    public void SetDamper(float newDamper)
    {
        spring.damper = newDamper;
        motorHingeJoint.spring = spring;
    }
    public void SetLimits()
    {
        limit.min = limits.x;
        limit.max = limits.y;
        motorHingeJoint.limits = limit;
    }
}
