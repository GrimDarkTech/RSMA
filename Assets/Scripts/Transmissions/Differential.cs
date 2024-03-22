using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Differential : MonoBehaviour
{
    public AnimationCurve curver;

    public GameObject driver;

    public GameObject satelliteGear;

    public GameObject rightGear;

    public GameObject leftGear;

    public float rightGearRatio;

    public float leftGearRatio;


    public float driverAngularVelocity;

    public float satelliteGearAngularVelocity;

    public float rightGearAngularVelocity;

    public float leftGearAngularVelocity;


    private void Update()
    {
        rightGearAngularVelocity = driverAngularVelocity * (rightGearRatio + 1) - rightGearRatio * satelliteGearAngularVelocity;
        leftGearAngularVelocity = driverAngularVelocity * (-leftGearRatio + 1) + leftGearRatio * satelliteGearAngularVelocity;

        driver.transform.Rotate(0, 0, driverAngularVelocity);
        satelliteGear.transform.Rotate(0, 0, satelliteGearAngularVelocity);
        rightGear.transform.Rotate(0, 0, rightGearAngularVelocity);
        leftGear.transform.Rotate(0, 0, leftGearAngularVelocity);
    }
}
