using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class RSMAMotor : MonoBehaviour
{
    private HingeJoint motorHingeJoint;
    private JointMotor hingeJointMotor;
    private JointSpring hingeJointSpring;
    private float input = 0;

    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public Rigidbody rotor;
    /// <summary>
    /// Driver connected to motor
    /// </summary>
    public RSMAMotorDriver motorDriver;
    /// <summary>
    /// Represents the motor axis, emanating from origin
    /// </summary>
    public Vector3 startMotorAxis;

    /// <summary>
    /// Resets MotorAnchor with startMotorAnchor
    /// </summary>
    public bool resetMotorAnchor;

    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 startMotorAnchor;
   
    /// <summary>
    /// Maximum angular velocity of motor
    /// </summary>
    public float maxVelocity = 1;
    /// <summary>
    /// Maximum torque of motor
    /// </summary>
    public float maxForce = 1;
    /// <summary>
    /// Springiness factor of motor
    /// </summary>
    public float springForce = 1;
    /// <summary>
    /// Motor damping factor 
    /// </summary>
    public float springDumper = 1;

    void Start()
    {
        MotorInit();
        SetMotorAxis(startMotorAxis);
        if (resetMotorAnchor)
        {
            SetMotorAnchor(startMotorAnchor);
        }
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
    /// <summary>
    /// Initializes motor
    /// </summary>
    public void MotorInit()
    {
        motorHingeJoint = gameObject.GetComponent<HingeJoint>();
    }
    /// <summary>
    /// Sets motor axis
    /// </summary>
    /// <param name="newMotorAxis">Motor axis</param>
    public void SetMotorAxis(Vector3 newMotorAxis)
    {
        motorHingeJoint.axis = newMotorAxis;
    }
    /// <summary>
    /// Sets motor axis
    /// </summary>
    /// <param name="newMotorX">X axis</param>
    /// <param name="newMotorY">Y axis</param>
    /// <param name="newMotorZ">Z axis</param>
    public void SetMotorAxis(float newMotorX, float newMotorY, float newMotorZ)
    {
        Vector3 newMotorAxis = new Vector3(newMotorX, newMotorY, newMotorZ);
        motorHingeJoint.axis = newMotorAxis;
    }
    /// <summary>
    /// Position of motor axis 
    /// </summary>
    /// <param name="newMotorAnchor">Motor anchor</param>
    public void SetMotorAnchor(Vector3 newMotorAnchor)
    {
        motorHingeJoint.anchor = newMotorAnchor;
    }
    /// <summary>
    /// Turn motor on/off
    /// </summary>
    /// <param name="isMotorActive">Motor activity mode</param>
    public void SetMotorActive(bool isMotorActive)
    {
        motorHingeJoint.useMotor = isMotorActive;
    }
    /// <summary>
    /// Activates/deactivates springiness of motor
    /// </summary>
    /// <param name="isSpringActive">Springiness activity mode</param>
    public void SetSpringActive(bool isSpringActive)
    {
        motorHingeJoint.useSpring = isSpringActive;
    }
    /// <summary>
    /// Sets body connected to motor rotor
    /// </summary>
    /// <param name="newRotor">Body connected to rotor</param>
    public void SetRotor(Rigidbody newRotor)
    {
        motorHingeJoint.connectedBody = newRotor;
    }
    /// <summary>
    /// Sets target angular velocity
    /// </summary>
    /// <param name="newVelocity">Target angular velocity (degrees per sec)</param>
    public void SetVelocity(float newVelocity)
    {
        hingeJointMotor.targetVelocity = newVelocity;
        motorHingeJoint.motor = hingeJointMotor;
    }
    /// <summary>
    /// Sets target torque
    /// </summary>
    /// <param name="newForce">Target torque (Newton * meters)</param>
    public void SetForce(float newForce)
    {
        hingeJointMotor.force = newForce;
        motorHingeJoint.motor = hingeJointMotor;
    }
    /// <summary>
    /// Set springiness factor
    /// </summary>
    /// <param name="newSpringForce">Springiness</param>
    public void SetSpringForce(float newSpringForce)
    {
        hingeJointSpring.spring = newSpringForce;
        motorHingeJoint.spring = hingeJointSpring;
    }
    /// <summary>
    /// Sets damping factor for springiness
    /// </summary>
    /// <param name="newSpringDamper">Damping factor</param>
    public void SetSpringDamper(float newSpringDamper)
    {
        hingeJointSpring.damper = newSpringDamper;
        motorHingeJoint.spring = hingeJointSpring;
    }
}
