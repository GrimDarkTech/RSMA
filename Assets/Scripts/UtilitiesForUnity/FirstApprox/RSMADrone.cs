using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMADrone : MonoBehaviour
{
    [SerializeField] private float _gravity = 9.81f;

    [SerializeField] private float _mass = 1f;

    private Rigidbody _rigidbody;

    public Vector3 targetDirection;

    public Vector3 acceleration = Vector3.zero;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        float pitch = Mathf.Atan2(targetDirection.z, targetDirection.y + _gravity);
        float yaw = Mathf.Atan2(targetDirection.x, targetDirection.y + _gravity);
        transform.rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0, yaw * Mathf.Rad2Deg);

        acceleration = transform.up * (_mass * targetDirection.z / Mathf.Sin(pitch));

        if(acceleration.y is float.NaN)
        {
            acceleration.y = _gravity;
        }
    }

    private void FixedUpdate()
    {
        acceleration = _mass * (targetDirection + (_gravity * Vector3.down));
        _rigidbody.AddForce(acceleration);
    }
}
