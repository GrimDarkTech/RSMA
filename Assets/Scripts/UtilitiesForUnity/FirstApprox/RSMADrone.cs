using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMADrone : MonoBehaviour
{
    [SerializeField] private float _gravity = 9.81f;

    [SerializeField] private float _mass = 1f;

    private Rigidbody _rigidbody;

    private Camera _camera;

    private Vector3 acceleration = Vector3.zero;

    [Space(15)]

    public Vector3 targetAcceleration;

    public float yaw = 0f;

    public float droneTurnSmoothness = 0.4f;

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
        float pitch = Mathf.Atan2(targetAcceleration.z, targetAcceleration.y + _gravity);
        float roll = Mathf.Atan2(-targetAcceleration.x, targetAcceleration.y + _gravity);

        transform.rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);

        if (pitch == 0 && roll == 0)
        {
            acceleration = new Vector3(0, _mass * _gravity + targetAcceleration.y, 0);
        }
        else
        {
            acceleration = new Vector3(_mass * targetAcceleration.x, _mass * (targetAcceleration.y + _gravity), _mass * targetAcceleration.z);
            acceleration = transform.up * acceleration.magnitude;
        }

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
        _rigidbody.AddForce(acceleration + new Vector3(0, - _mass * _gravity,0));
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
