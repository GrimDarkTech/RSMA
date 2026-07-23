using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;
public class Lidar : MonoBehaviour
{
    public string topicName = "Lidar";

    public float maxRange;
    public float minRange;

    [Min(0.5f)]
    public float angleMin = 0.5f;

    [Range(0.5f, 360.0f)]
    public float angleMax = 360.0f;

    public bool isDrawRays = false;
    public ScanSize scanSize = ScanSize.LaserScan128;


    private LaserScan128 scan128;
    private LaserScan256 scan256;

    private float angleIncrement = 5;

    private float[] ranges = new float[256];


    [ContextMenu("MeasureRange")]
    // Измените метод Start и MeasureRange в Unity:
    private void MeasureRange()
    {
        angleIncrement = (angleMax - angleMin) / ((int)scanSize - 1);

        for (int rayIndex = 0; rayIndex < (int)scanSize; rayIndex++)
        {
            float currentAngle = angleMin + (rayIndex * angleIncrement);
            Vector3 rayDirection = Quaternion.AngleAxis(currentAngle, transform.up) * transform.forward;

            float range = maxRange;
            Ray ray = new Ray(transform.position, rayDirection);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRange))
            {
                if (hit.distance < range) range = hit.distance;
                if (hit.distance < minRange) range = minRange;
            }

            ranges[rayIndex] = range;
        }
    }

    private void Start()
    {
        gameObject.layer = 2;

        angleIncrement = (angleMax - angleMin) / ((int)scanSize - 1);

        if (scanSize == ScanSize.LaserScan128)
        {
            scan128 = new LaserScan128();

            scan128.angleMax = angleMax;
            scan128.angleMin = angleMin;
            scan128.angleIncrement = angleIncrement;
            scan128.rangeMin = minRange;
            scan128.rangeMax = maxRange;
            scan128.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            scan128.ranges = ranges;

            DataBroker.Publish(topicName, scan128);
        }
        else 
        {
            scan256 = new LaserScan256();

            scan256.angleMax = angleMax;
            scan256.angleMin = angleMin;
            scan256.angleIncrement = angleIncrement;
            scan256.rangeMin = minRange;
            scan256.rangeMax = maxRange;
            scan256.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            scan256.ranges = ranges;

            DataBroker.Publish(topicName, scan256);
        }

    }

    private void Update()
    {
        MeasureRange();
        if (scanSize == ScanSize.LaserScan128)
        {
            scan128.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            scan128.ranges = ranges;
            DataBroker.Publish(topicName, scan128);
        }
        else 
        {
            scan256.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            scan256.ranges = ranges;
            DataBroker.Publish(topicName, scan256);
        }
    }

    private void OnDrawGizmos()
    {
        if (isDrawRays)
        {
            Gizmos.color = Color.red;
            for (float i = angleMin; i <= angleMax; i = i + angleIncrement)
            {
                Vector3 directon = Quaternion.AngleAxis(i, transform.up) * transform.forward;

                Gizmos.DrawLine(transform.position, transform.position + directon * maxRange);
            }

        }
    }
}

public enum ScanSize
{
    LaserScan128 = 128,
    LaserScan256 = 256
}