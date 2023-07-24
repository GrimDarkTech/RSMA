using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RSMAEncoder : RSMADataTransferSlave
{
    public float encoderResolution = 1;

    public Vector3 rotation;
    public Vector3 angularVelocity;
    public Vector3 encoderValue;

    Rigidbody rotorRigidbody;

    public GameObject rotor;
    public HingeJoint motor;

    private void Start()
    {
        rotorRigidbody = rotor.GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        angularVelocity = Vector3.Dot(rotorRigidbody.angularVelocity, motor.axis) * motor.axis;
        rotation = rotation + angularVelocity * Time.deltaTime;
        encoderValue = (rotation / (Mathf.PI * 2)) * encoderResolution;

        data = angularVelocity.x + ";" + angularVelocity.y + ";" + angularVelocity.z;
    }
    public override string SendData()
    {
        return (data);
    }
}
