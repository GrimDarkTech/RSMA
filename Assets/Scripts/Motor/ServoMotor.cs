using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    protected HingeJoint hingeJoint;
    protected Rigidbody rigidbody;
    protected float input = 0;

    private JointSpring spring;
    private JointLimits limit;


    public void MotorInit()
    {
        if (!gameObject.TryGetComponent<Rigidbody>(out rigidbody))
            rigidbody = gameObject.AddComponent<Rigidbody>();
        if (!gameObject.TryGetComponent<HingeJoint>(out hingeJoint))
            hingeJoint = gameObject.AddComponent<HingeJoint>();
        spring = hingeJoint.spring;
        limit = hingeJoint.limits;
    }
    public void SetMotorAxis(Vector3 newMotorAxis)
    {
        hingeJoint.axis = newMotorAxis;
    }
    public Vector3 GetMotorAxis()
    {
        return hingeJoint.axis;
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

    public void SetSpringActive(bool isSpringActive)
    {
        hingeJoint.useSpring = isSpringActive;
    }
    public void SetLimitsActive(bool isLimitsActive)
    {
        hingeJoint.useLimits = isLimitsActive;
    }
    public void SetRotor(Rigidbody newRotor)
    {
        hingeJoint.connectedBody = newRotor;
    }
    public void SetTargetAngle(float targetAngle)
    {
        spring.targetPosition = targetAngle-maxAngle/2;
        hingeJoint.spring = spring;
    }
    public void SetTorque(float newTorque)
    {
        spring.spring = newTorque;
        hingeJoint.spring = spring;
    }
    public void SetDamper(float newDamper)
    {
        spring.damper = newDamper;
        hingeJoint.spring = spring;
    }
    public void SetLimits()
    {
        limit.min = limits.x;
        limit.max = limits.y;
        hingeJoint.limits = limit;
    }
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
        input = GPIOScript.GetPWMPort(portPWMId)+0.005f;
        SetTargetAngle(input * maxAngle);
        SetDamper(damper);
        SetTorque(torque);
        SetLimits();
    }
}
