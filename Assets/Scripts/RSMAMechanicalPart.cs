using UnityEngine;

/// <summary>
/// Controls of an object's position and rotation through physics simulation. Uses Rigidbody to simulate physics
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Mechanics/Setting_up_the_physics_of_models.md")]
public class RSMAMechanicalPart : MonoBehaviour
{
    private Rigidbody _rigidbody;

    /// <summary>
    /// The mass of body in kilograms
    /// </summary>
    public float mass = 0.5f;

    /// <summary>
    /// The center of mass relative to the transform's origin position in meters
    /// </summary>
    public Vector3 centerOfMassPosition = Vector3.zero;

    /// <summary>
    /// If True, the rendering of the position of the center of mass of the body is enabled. 
    /// The center of mass is displayed as a yellow sphere.
    /// </summary>
    public bool isDrawCenterOfMass = true;


    private void Start()
    {
        if(_rigidbody == null)
        {
            _rigidbody = gameObject.GetComponent<Rigidbody>();
        }
        if(mass <= 0)
        {
            mass = 0.01f;
        }
        _rigidbody.mass = mass;
        _rigidbody.centerOfMass = centerOfMassPosition;
        _rigidbody.WakeUp();
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
