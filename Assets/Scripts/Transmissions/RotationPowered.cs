using UnityEngine;

/// <summary>
/// Implements the functionality and properties of a device powered by rotational energy
/// </summary>
public interface IRotationPowered
{
    /// <summary>
    /// The body to which the rotation is transmitted 
    /// </summary>
    public Rigidbody rigidbody { get; set; }

    /// <summary>
    /// Angular velocity of the body
    /// </summary>
    public float inputAngularVelocity { get; set; }

    /// <summary>
    /// The torque applied to the body
    /// </summary>
    public float inputTorque { get; set; }

}
