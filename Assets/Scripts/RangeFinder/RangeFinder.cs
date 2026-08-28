using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;
public class RangeFinder : MonoBehaviour
{
    public string topicName = "RangeFinder";

    public float maxRange;
    public float minRange;

    public bool isDrawRay = false;

    private Float32 rangeData = new Float32();
    private float range = 0.0f;


    [ContextMenu("MeasureRange")]
    private void MeasureRange()
    {
        Vector3 rayDirection = Quaternion.AngleAxis(0, transform.up) * transform.forward;

        range = maxRange;
        Ray ray = new Ray(transform.position, rayDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange))
        {
            if (hit.distance < range) 
            {
                range = hit.distance;
            }

            if (hit.distance < minRange)
            {
                range = minRange;
            }
        }
    }

    private void Start()
    {
        gameObject.layer = 2;

        rangeData = new Float32();

        rangeData.value = range;
        rangeData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish(topicName, rangeData);
    }

    private void Update()
    {
        MeasureRange();
        rangeData.value = range;
        rangeData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DataBroker.Publish(topicName, rangeData);
    }

    private void OnDrawGizmos()
    {
        if (isDrawRay)
        {
            Gizmos.color = Color.red;
            Vector3 directon = Quaternion.AngleAxis(0, transform.up) * transform.forward;

            Gizmos.DrawLine(transform.position, transform.position + directon * maxRange);
        }
    }
}