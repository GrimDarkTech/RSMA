using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class Gearbox : MonoBehaviour, IRotationPowered
{
    /// <summary>
    /// Gear ratio of the gearbox
    /// </summary>
    public float ratio;
    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public Rigidbody connectedBody;
    /// <summary>
    /// Driver connected to motor
    /// </summary>
    public Vector3 gearboxAxis;
    /// <summary>
    /// Resets MotorAnchor with startMotorAnchor
    /// </summary>
    public bool isResetAnchor;
    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 anchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 connectedAnchor;

    public float maxAngularVelocity = 523f;

    public bool isDrawAnchors = true;

    public Vector3 outputTorque;


    private HingeJoint _hingeJoint;


    public Rigidbody rigidbody { get; set; }

    public float inputAngularVelocity { get; set; }

    public float inputTorque { get; set; }


    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        _hingeJoint = GetComponent<HingeJoint>();

        _hingeJoint.axis = gearboxAxis;


        if (isResetAnchor)
        {
            _hingeJoint.autoConfigureConnectedAnchor = false;
            _hingeJoint.anchor = anchor;
            _hingeJoint.connectedAnchor = connectedAnchor;
        }

        _hingeJoint.connectedBody = connectedBody;

        connectedBody.maxAngularVelocity = maxAngularVelocity;
    }
    private void FixedUpdate()
    {
        connectedBody.AddRelativeTorque(gearboxAxis * inputTorque * ratio);

        outputTorque = gearboxAxis * inputTorque * ratio;

        rigidbody.AddRelativeTorque(-gearboxAxis * inputTorque * ratio);

        inputAngularVelocity = (connectedBody.angularVelocity - rigidbody.angularVelocity).magnitude * ratio;
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(anchor), (transform.right * gearboxAxis.x + transform.up * gearboxAxis.y + transform.forward * gearboxAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(anchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), (transform.right * gearboxAxis.x + transform.up * gearboxAxis.y + transform.forward * gearboxAxis.z) * 0.01f);
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), 0.002f);
        }
    }
}
