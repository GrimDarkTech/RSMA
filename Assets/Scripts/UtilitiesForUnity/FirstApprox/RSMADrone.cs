using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class RSMADrone : MonoBehaviour
{
    [SerializeField] private float _gravity = 9.81f;

    [SerializeField] private float _mass = 1f;

    private Rigidbody _rigidbody;

    private Camera _camera;

    private Vector3 acceleration = Vector3.zero;

    [Space(15)]

    public Vector3 targetAcceleration;

    [Space(15)]

    public bool isLockCamera;

    public Vector3 cameraRotation;

    public float cameraTurnSmoothness = 0.05f;

    public DroneGyro gyro;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        gyro = new DroneGyro();
        _camera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        acceleration = targetAcceleration;

        Vector3 thrust = (targetAcceleration + _gravity * Vector3.up);
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, thrust);

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.05f);

        gyro.acceleration = acceleration;
        gyro.velocity = _rigidbody.velocity;
        gyro.position = transform.position;

        gyro.angularVelocity= _rigidbody.angularVelocity;
        gyro.rotation = transform.rotation.eulerAngles;

        if (!isLockCamera)
        {
            _camera.transform.localRotation = Quaternion.Lerp(_camera.transform.localRotation, Quaternion.Euler(cameraRotation), cameraTurnSmoothness);
        }
        else
        {
            _camera.transform.rotation = Quaternion.Lerp(_camera.transform.rotation, Quaternion.Euler(cameraRotation), cameraTurnSmoothness);
        }
    }

    private void FixedUpdate()
    {
        _rigidbody.AddForce(acceleration * _mass);
    }

    public void MoveToPosition(Vector3 targetPosition, float kp, float ki, float kd)
    {
        StopCoroutine("MoveToPositionAsync");
        StartCoroutine(MoveToPositionAsync(targetPosition, kp, ki, kd));
    }

    private IEnumerator MoveToPositionAsync(Vector3 targetPosition, float kp, float ki, float kd)
    {
        Vector3 error = Vector3.zero;
        Vector3 error_integral = Vector3.zero;
        Vector3 error_derivative = Vector3.zero;
        Vector3 error_prev = Vector3.zero;

        while (!(Vector3.Distance(targetPosition, transform.position) < 0.03f && _rigidbody.velocity.magnitude < 0.03f))
        {
            error = targetPosition - transform.position;
            error_integral += error * 0.01f;
            error_derivative = (error - error_prev) / 0.01f;

            Vector3 control = kp * error + ki * error_integral + kd * error_derivative;

            targetAcceleration = control;

            error_prev = error;

            yield return new WaitForSeconds(0.01f);
        }

        targetAcceleration = Vector3.zero;
    }

    [Serializable]
    public struct DroneGyro
    {
        public Vector3 acceleration;
        public Vector3 velocity;
        public Vector3 position;

        public Vector3 angularVelocity;
        public Vector3 rotation;
    }
}
