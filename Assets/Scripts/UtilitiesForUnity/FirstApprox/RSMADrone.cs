using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMADrone : MonoBehaviour
{
    [SerializeField] private float _gravity = 9.81f;

    [SerializeField] private float _mass = 1f;

    public Vector3 targetDirection;

    public Vector3 acceleration = Vector3.zero;

    private void Update()
    {
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(acceleration), 0.01f);
    }

    private void FixedUpdate()
    {
        acceleration = targetDirection + (_mass * _gravity * Vector3.down);
    }
}
