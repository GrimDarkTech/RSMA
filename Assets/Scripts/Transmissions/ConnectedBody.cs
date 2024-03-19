using System;
using UnityEngine;

namespace RotationPowered
{
    public enum Mode
    {
        body,
        transmission,
        mechanism
    }
}

public class RotationPowered: MonoBehaviour
{
    [Ser]
    public Mode mode;

    public Rigidbody rigidbody;

    public Vector3 angularVelocity;

    public void FixedUpdate()
    {
        angularVelocity = rigidbody.angularVelocity;
    }

    public void AddTorque(Vector3 torque)
    {
        if(mode == Mode.body)
        {
            rigidbody.AddRelativeTorque(torque);
        }
    }

    [Serializable]

}

public interface IRotationPowered
{
    public Rigidbody rigidbody { get; set; }

    public 
}
