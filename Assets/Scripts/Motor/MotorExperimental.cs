using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotorExperimental : MonoBehaviour
{
    protected HingeJoint hingeJoint;
    protected Rigidbody rigidbody;
    protected JointSpring spring;
    public Rigidbody rotor;
    public MotorDriver motorDriver;
    public Vector3 startMotorAxis;
    protected float input = 0;
    public float torque = 1;
    public float damper = 0.25f;
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
    public void SetRotor(Rigidbody newRotor)
    {
        hingeJoint.connectedBody = newRotor;
    }
    public void SetTorque(float SetTorque)
    {
        rotor.AddRelativeTorque(SetTorque * startMotorAxis);
        rigidbody.AddTorque(-SetTorque * startMotorAxis);
    }
    public void SetDamper(float newDamper)
    {
        spring.damper = newDamper;
        hingeJoint.spring = spring;
    }

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
}
