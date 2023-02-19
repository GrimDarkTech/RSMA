using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RSMAEncoder : RSMADataTransferSlave
{
    public float angularVelocityOut;

    Vector3 angularVelocity;
    Rigidbody rotorRigidbody;
    Rigidbody motorRigidbody;

    public GameObject rotor;
    public GameObject motor;

    private void Start()
    {
        rotorRigidbody = rotor.GetComponent<Rigidbody>();
        motorRigidbody = motor.GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        angularVelocity = rotorRigidbody.angularVelocity - motorRigidbody.angularVelocity;
        angularVelocityOut = angularVelocity.magnitude;
        data = angularVelocityOut.ToString();
    }
    public override string SendData()
    {
        return (data);
    }
}
