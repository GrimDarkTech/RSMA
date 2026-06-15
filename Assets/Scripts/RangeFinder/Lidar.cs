using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;
public class Lidar : MonoBehaviour
{
    public float maxRange;
    public float minRange;

    [Min(0.5f)]
    public float angleMin = 0.5f;

    [Min(360.0f)]
    public float angleMax = 360.0f;

    public float angleIncrement = 5;

    public bool isDrawRays = false;

    private float[] ranges = new float[128];

    private LaserScan128 scan = new LaserScan128();


    [ContextMenu("MeasureRange")]
    private void MeasureRange()
    {
        int rayIndex = 0;

        for (float i = angleMin; i <= angleMax; i = i + angleIncrement)
        {
            Vector3 rayDirection = Quaternion.AngleAxis(i, transform.up) * transform.forward;

            RaycastHit hit;
            Ray ray;

            float range = maxRange;

            ray = new Ray(transform.position, rayDirection);
            if (Physics.Raycast(ray, out hit, maxRange))
            {
                //Debug.DrawLine(gameObject.transform.position, hit.point);

                if (hit.distance < range)
                {
                    range = hit.distance;
                }

                if(hit.distance < minRange)
                {
                    range = minRange;
                }

            }

            if (rayIndex < 128) 
            {
                ranges[rayIndex] = range;
            }

            rayIndex++;
        }
    }

    private void Start()
    {
        gameObject.layer = 2;

        scan.angleMax = angleMin;
        scan.angleMin = angleMax;
        scan.angleIncrement = angleIncrement;
        scan.rangeMin = minRange;
        scan.rangeMax = maxRange;
        scan.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        scan.ranges = ranges;

        DataBroker.Publish("Lidar", scan);
    }

    private void Update()
    {
        MeasureRange();
        scan.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        scan.ranges = ranges;

        DataBroker.Publish("Lidar", scan);
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
