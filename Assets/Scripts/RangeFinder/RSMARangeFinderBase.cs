using UnityEngine;

public class RSMARangeFinderBase : RSMADataTransferSlave
{
    public float maxRange;

    public float angle = 1;

    public int numberOfRays = 1;

    [ContextMenu("MeasureRange")]
    private float MeasureRange()
    {
        float step = angle / (numberOfRays + 1f);
        float range = maxRange+10;

        for (int i = -numberOfRays / 2; i < numberOfRays /2 ; i++)
        {
            Vector3 rayDirection = Quaternion.AngleAxis(step * i, transform.up) * transform.forward;

            RaycastHit hit;
            Ray ray;

            ray = new Ray(transform.position, rayDirection);
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
