using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotorBaseScript : MonoBehaviour
{
    private HingeJoint hingeJoint;
    private JointMotor hingeJointMotor;
    private Rigidbody rigidbody;
    public Rigidbody rotor;
    public void motorInit()
    {
        rigidbody = gameObject.AddComponent<Rigidbody>();
        hingeJoint = gameObject.AddComponent<HingeJoint>();
    }
    public void setMotorAxis(Vector3 newMotorAxis)
    {
        hingeJoint.axis = newMotorAxis;
    }
    public void setMotorAxis(float newMotorX, float newMotorY, float newMotorZ)
    {
        Vector3 newMotorAxis = new Vector3(newMotorX, newMotorY, newMotorZ);
        hingeJoint.axis = newMotorAxis;
    }
    public void setMotorActive(bool isMotorActive)
    {
        hingeJoint.useMotor = isMotorActive;
    }
    public void setRotor(Rigidbody newRotor)
    {
        hingeJoint.connectedBody = newRotor;
    }
    public void setVelocity(float newVelocity)
    {
        hingeJointMotor.targetVelocity = newVelocity;
        hingeJoint.motor = hingeJointMotor;
    }
    public void setForce(float newForce)
    {
        hingeJointMotor.force = newForce;
        hingeJoint.motor = hingeJointMotor;
    }
    void Start()
    {
        motorInit();
        setMotorAxis(0, 1, 0);
        setMotorActive(true);
        setRotor(rotor);
        setVelocity(360);
        setForce(60);
    }
    void Update()
    {
        
    }
}
