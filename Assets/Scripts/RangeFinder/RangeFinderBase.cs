using UnityEngine;
using System;
using System.Collections.Generic;

public class RangeFinderBase : DataTransgerSlaveScript
{
    public float maxRange;

    private float curentAngle = 0;


    [ContextMenu("MeasureRange")]
    private float MeasureRange()
    {
        float range = maxRange+10;
        RaycastHit hit;
        Quaternion rayRotation;
        Ray ray;
        ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out hit, maxRange))
            {
                Debug.DrawLine(gameObject.transform.position, hit.point);

                if (hit.distance < range)
                    range = hit.distance;
            }
            else
            {
                range = maxRange;
            }

        return range;
    }
    public override string SendData()
    {
        return (MeasureRange().ToString());
    }
    private void Start()
    {
        gameObject.layer = 2;
    }
}
