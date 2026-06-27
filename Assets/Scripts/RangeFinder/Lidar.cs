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

    private float[] ranges = new float[128];

    private LaserScan128 scan = new LaserScan128();
    private float angleIncrement = 5;
    private int rayCount = 128;


    [ContextMenu("MeasureRange")]
    // Измените метод Start и MeasureRange в Unity:
    private void MeasureRange()
    {
        angleIncrement = (angleMax - angleMin) / (rayCount - 1);

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
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

        angleIncrement = (angleMax - angleMin) / (rayCount - 1);

        scan.angleMax = angleMax;
        scan.angleMin = angleMin;
        scan.angleIncrement = angleIncrement;
        scan.rangeMin = minRange;
        scan.rangeMax = maxRange;
        scan.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        scan.ranges = ranges;

        DataBroker.Publish(topicName, scan);
    }

    private void Update()
    {
        MeasureRange();
        scan.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        scan.ranges = ranges;

        DataBroker.Publish(topicName, scan);
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
