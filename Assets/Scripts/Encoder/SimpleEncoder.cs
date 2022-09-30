using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleEncoder : MonoBehaviour
{
    public float angularVelocityOut;

    Vector3 angularVelocity;
    Rigidbody rotorRigidbody;
    Rigidbody motorRigidbody;

    [SerializeField] GameObject rotor;
    [SerializeField] GameObject motor;

    private void Start()
    {
        rotorRigidbody = rotor.GetComponent<Rigidbody>();
        motorRigidbody = motor.GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        angularVelocity = rotorRigidbody.angularVelocity - motorRigidbody.angularVelocity;
        angularVelocityOut = angularVelocity.magnitude;
    }
}
