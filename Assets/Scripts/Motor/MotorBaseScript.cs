using UnityEngine;
public class MotorBaseScript : MonoBehaviour
{
    private HingeJoint hingeJoint;
    private JointMotor hingeJointMotor;
    private Rigidbody rigidbody;
    public Rigidbody rotor;
    public MotorDriverScript motorDriver;
    private float input = 0;
    public float maxVelocity = 0;
    public void motorInit()
    {
        if (!gameObject.TryGetComponent<Rigidbody>(out rigidbody))
            rigidbody = gameObject.AddComponent<Rigidbody>();
        if (!gameObject.TryGetComponent<HingeJoint>(out hingeJoint))
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
        setForce(120);
    }
    void Update()
    {
        input = motorDriver.getOutput();
        setVelocity(maxVelocity * input);
    }
}
