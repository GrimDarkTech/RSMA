using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneInput : MonoBehaviour
{
    private RSMADrone _drone;

    void Start()
    {
        _drone = gameObject.GetComponent<RSMADrone>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Jump");
        float z = Input.GetAxis("Vertical");

        _drone.targetAcceleration = new Vector3(x, y, z);

        _drone.yaw += Input.GetAxis("Yaw") * 0.02f;


    }
}
