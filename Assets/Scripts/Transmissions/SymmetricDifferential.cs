using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SymmetricDifferential : MonoBehaviour
{
    public GameObject driver;

    public GameObject satelliteGear;

    public GameObject rightGear;

    public GameObject leftGear;


    public float driverRadius;

    public float satelliteGearRadius;

    public float rightGearRadius;

    public float leftGearRadius;


    public float driverAngularVelocity;

    public float satelliteGearAngularVelocity;

    public float rightGearAngularVelocity;

    public float leftGearAngularVelocity;


    private void Update()
    {
        rightGearAngularVelocity  = leftGearAngularVelocity + 2 * driverAngularVelocity;
        float satelliteGearAngularVelocity = (rightGearAngularVelocity * (rightGearAngularVelocity - driverAngularVelocity) - driverAngularVelocity * satelliteGearRadius) / rightGearRadius;

        driver.transform.Rotate(0, 0, driverAngularVelocity);
        satelliteGear.transform.Rotate(0, 0, satelliteGearAngularVelocity);
        rightGear.transform.Rotate(0, 0, rightGearAngularVelocity);
        leftGear.transform.Rotate(0, 0, leftGearAngularVelocity);
    }
}
