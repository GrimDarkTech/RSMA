
using System;
using UnityEngine;

/// <summary>
/// Implements properties and functionality of stepper motor
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class StepperMotor : MonoBehaviour
{
    private HingeJoint _hingeJoint;

    private Rigidbody _rotor;

    private IRotationPowered rotationPowered;

    private int _input = 0;

    private int _state = 0;

    private float _currentAngle = 0;

    /// <summary>
    /// Body used as a motor rotor
    /// </summary>
    public GameObject connectedBody;
    /// <summary>
    /// Driver connected to motor
    /// </summary>
    public StepperDriver motorDriver;
    /// <summary>
    /// Represents the motor axis, emanating from origin
    /// </summary>
    public Vector3 motorAxis;
    /// <summary>
    /// Resets MotorAnchor with startMotorAnchor
    /// </summary>
    public bool isResetMotorAnchor;
    /// <summary>
    /// Represents the Motor Anchor
    /// </summary>
    public Vector3 motorAnchor;
    /// <summary>
    /// Represents the anchor for connected body
    /// </summary>
    public Vector3 connectedAnchor;
    /// <summary>
    /// Holding torque
    /// </summary>
    public float holdingTorque = 0.1f;
    /// <summary>
    /// Holding torque
    /// </summary>
    public float torque = 0.1f;
    /// <summary>
    /// Step angle
    /// </summary>
    public float stepAngle;

    public float maxAngularVelocity = 523f;

    public bool isDrawAnchors = false;

    private void Start()
    {
        rotationPowered = connectedBody.GetComponent<IRotationPowered>();

        if (rotationPowered == null)
        {
            _hingeJoint = gameObject.AddComponent<HingeJoint>();
            _rotor = connectedBody.GetComponent<Rigidbody>();
            _hingeJoint.connectedBody = _rotor;

            _hingeJoint.axis = motorAxis;

            if (isResetMotorAnchor)
            {
                _hingeJoint.anchor = motorAnchor;
                _hingeJoint.autoConfigureConnectedAnchor = false;
                _hingeJoint.connectedAnchor = connectedAnchor;
                _hingeJoint.autoConfigureConnectedAnchor = true;
            }

            _hingeJoint.useSpring = true;

            JointSpring spring = new JointSpring();
            spring.damper = holdingTorque;
            spring.spring = holdingTorque;
            _hingeJoint.spring = spring;

            _rotor.maxAngularVelocity = maxAngularVelocity;
        }
    }
    private void Update()
    {
        if (motorDriver != null)
        {
            _input = motorDriver.output;
        }

        if (rotationPowered == null)
        {
            if (_input > 0) 
            {
                float angleDelta = 0;

                if (Math.Abs(_input - _state) == 1)
                {
                    angleDelta = stepAngle * Math.Sign(_input - _state);
                } 
                else if(Math.Abs(_input - _state) == 3)
                {
                    angleDelta = stepAngle * -Math.Sign(_input - _state);
                }

                _currentAngle = _currentAngle + angleDelta;
                _state = _input;

                if (_currentAngle > 180)
                {
                    _currentAngle -= 360;
                }
                else if (_currentAngle < -180)
                {
                    _currentAngle += 360;
                }

                JointSpring spring = new JointSpring();
                spring.damper = holdingTorque;
                spring.spring = torque;
                spring.targetPosition = _currentAngle;
                _hingeJoint.spring = spring;
            }
        }
        else
        {
            rotationPowered.inputTorque = torque * _input;
        }
    }

    private void OnDrawGizmos()
    {
        if (isDrawAnchors)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.TransformPoint(motorAnchor), (transform.right * motorAxis.x + transform.up * motorAxis.y + transform.forward * motorAxis.z) * 0.01f);
            Gizmos.DrawSphere(transform.TransformPoint(motorAnchor), 0.002f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), (transform.right * motorAxis.x + transform.up * motorAxis.y + transform.forward * motorAxis.z) * 0.01f);
            Gizmos.DrawSphere(connectedBody.gameObject.transform.TransformPoint(connectedAnchor), 0.002f);
        }
    }
}

