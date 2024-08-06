using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Differential : MonoBehaviour, IRotationPowered
{
    /// <summary>
    /// Gear ratio of the right output of the differential
    /// </summary>
    public float rightGearRatio;
    /// <summary>
    /// Gear ratio of the left output of the differential
    /// </summary>
    public float leftGearRatio;

    [Space(10)]
    /// <summary>
    /// Body connected to right output of the gearbox
    /// </summary>
    public Rigidbody rightConnectedBody;
    /// <summary>
    /// Axis of output of the gearbox
    /// </summary>
    public Vector3 rightAxis;
    /// <summary>
    /// Resets right anchor setting
    /// </summary>
    public bool isResetRightAnchor;
    /// <summary>
    /// Represents the gearbox Anchor
    /// </summary>
    public Vector3 rightAnchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 rightConnectedAnchor;

    [Space(10)]
    /// <summary>
    /// Body connected to right output of the gearbox
    /// </summary>
    public Rigidbody leftConnectedBody;
    /// <summary>
    /// Axis of output of the gearbox
    /// </summary>
    public Vector3 leftAxis;
    /// <summary>
    /// Resets right anchor setting
    /// </summary>
    public bool isResetLeftAnchor;
    /// <summary>
    /// Represents the gearbox Anchor
    /// </summary>
    public Vector3 leftAnchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 leftConnectedAnchor;

    [Space(10)]

    /// <summary>
    /// Braking factor
    /// </summary>
    public float brakingFactor = 0.01f;

    private HingeJoint _rightHingeJoint;
    private HingeJoint _leftHingeJoint;

    public Rigidbody rigidbody { get; set; }

    public float inputAngularVelocity { get; set; }

    public float inputTorque { get; set; }


    public float maxAngularVelocity = 523f;

    public bool isDrawAnchors = true;

    public Vector3 outputTorque;

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        _rightHingeJoint = gameObject.AddComponent<HingeJoint>();
        _leftHingeJoint = gameObject.AddComponent<HingeJoint>();

        _rightHingeJoint.axis = rightAxis;
        _leftHingeJoint.axis = rightAxis;

        if (isResetRightAnchor)
        {
            _rightHingeJoint.autoConfigureConnectedAnchor = false;
            _rightHingeJoint.anchor = rightAnchor;
            _rightHingeJoint.connectedAnchor = rightConnectedAnchor;
        }
        if (isResetLeftAnchor)
        {
            _leftHingeJoint.autoConfigureConnectedAnchor = false;
            _leftHingeJoint.anchor = leftAnchor;
            _leftHingeJoint.connectedAnchor = leftConnectedAnchor;
        }

        _rightHingeJoint.connectedBody = rightConnectedBody;
        _leftHingeJoint.connectedBody = leftConnectedBody;

        rightConnectedBody.maxAngularVelocity = maxAngularVelocity;
        leftConnectedBody.maxAngularVelocity = maxAngularVelocity;

        _rightHingeJoint.useSpring = true;
        _leftHingeJoint.useSpring = true;

        JointSpring spring = new JointSpring();
        spring.damper = brakingFactor;
        _rightHingeJoint.spring = spring;
        _leftHingeJoint.spring = spring;
    }

    private void Update()
    {
        rightConnectedBody.AddRelativeTorque((rightAxis * inputTorque * rightGearRatio) / 2);
        leftConnectedBody.AddRelativeTorque((leftAxis * inputTorque * leftGearRatio) / 2);

        outputTorque = (rightAxis * inputTorque * rightGearRatio) / 2;

        float rightAngularVelocity = (rightConnectedBody.angularVelocity - rigidbody.angularVelocity).magnitude;
        float leftAngularVelocity = (leftConnectedBody.angularVelocity - rigidbody.angularVelocity).magnitude;

        inputAngularVelocity = (rightAngularVelocity + leftAngularVelocity) / 2;
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(rightAnchor), (transform.right * rightAxis.x + transform.up * rightAxis.y + transform.forward * rightAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(rightAnchor), 0.002f);
            Gizmos.DrawRay(transform.TransformPoint(leftAnchor), (transform.right * leftAxis.x + transform.up * leftAxis.y + transform.forward * leftAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(leftAnchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(rightConnectedBody.gameObject.transform.TransformPoint(rightConnectedAnchor), (transform.right * rightAxis.x + transform.up * rightAxis.y + transform.forward * rightAxis.z) * 0.01f);
            Gizmos.DrawSphere(rightConnectedBody.gameObject.transform.TransformPoint(rightConnectedAnchor), 0.002f);
            Gizmos.DrawRay(leftConnectedBody.gameObject.transform.TransformPoint(leftConnectedAnchor), (transform.right * leftAxis.x + transform.up * leftAxis.y + transform.forward * leftAxis.z) * 0.01f);
            Gizmos.DrawSphere(leftConnectedBody.gameObject.transform.TransformPoint(leftConnectedAnchor), 0.002f);
        }
    }
}
