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


    private HingeJoint _hingeJoint;

    private Rigidbody _rotor;


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
            _hingeJoint.anchor = anchor;
            _hingeJoint.autoConfigureConnectedAnchor = false;
            _hingeJoint.connectedAnchor = connectedAnchor;
            _hingeJoint.autoConfigureConnectedAnchor = true;
        }

        _rotor = connectedBody.GetComponent<Rigidbody>();
        _hingeJoint.connectedBody = _rotor;

        _rotor.maxAngularVelocity = 1048f;
    }
    private void FixedUpdate()
    {
        _rotor.AddTorque(gearboxAxis * inputTorque * ratio);

        rigidbody.AddRelativeTorque(-gearboxAxis * inputTorque * ratio);

        inputAngularVelocity = (_rotor.angularVelocity - rigidbody.angularVelocity).magnitude * ratio;
    }
}
