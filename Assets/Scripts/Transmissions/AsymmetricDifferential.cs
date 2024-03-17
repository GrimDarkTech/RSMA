using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsymmetricDifferential : MonoBehaviour
{
    public GameObject driver;

    public GameObject satelliteGear;

    public GameObject sunGear;

    public GameObject ringGear;

    public float sunGearRadius = 1f;

    public float satelliteGearRadius = 1f;

    public float driverAngularVelocity = 1f;

    public float sunGearAngularVelocity = 1f;

    public float ringGearAngularVelocity = 1f;

    private void Update()
    {
        ringGearAngularVelocity = sunGearRadius * ((sunGearAngularVelocity - driverAngularVelocity) + driverAngularVelocity * (sunGearRadius + 2 * satelliteGearRadius)) / (sunGearRadius + 2 * satelliteGearRadius);
        float satelliteGearAngularVelocity = ((ringGearAngularVelocity - driverAngularVelocity) * (sunGearRadius + 2 * satelliteGearRadius) + satelliteGearRadius * driverAngularVelocity) / satelliteGearRadius;

        driver.transform.Rotate(0, 0, driverAngularVelocity);
        satelliteGear.transform.Rotate(0, 0, satelliteGearAngularVelocity);
        sunGear.transform.Rotate(0, 0, sunGearAngularVelocity);
        ringGear.transform.Rotate(0, 0, ringGearAngularVelocity);
    }
}
