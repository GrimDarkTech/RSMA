using UnityEngine;

/// <summary>
/// Controls of an object's position and rotation through physics simulation. Uses Rigidbody to simulate physics
/// </summary>

[RequireComponent(typeof(Rigidbody))]

public class RSMAMechanicalPart: MonoBehaviour
{
    private Rigidbody rigidbody;

    /// <summary>
    /// The mass of part in kilograms
    /// </summary>
    public float mass = 0.5f;

    /// <summary>
    /// The center of mass relative to the transform's origin position in meters
    /// </summary>
    public Vector3 centerOfMassPosition = Vector3.zero;

    public bool isDrawCenterOfMass = true;


    private void OnEnable()
    {
        if(rigidbody == null)
        {
            rigidbody = gameObject.GetComponent<Rigidbody>();
        }
        rigidbody.mass = mass;
        rigidbody.centerOfMass = centerOfMassPosition;
        rigidbody.WakeUp();
    }

    private void OnDrawGizmos()
    {
        if (isDrawCenterOfMass)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMassPosition), 0.002f);
        }
    }
}
