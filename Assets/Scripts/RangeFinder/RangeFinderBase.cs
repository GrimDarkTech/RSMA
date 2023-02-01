using UnityEngine;

public class RangeFinderBase : DataTransgerSlaveScript
{
    public float maxRange;

    [ContextMenu("MeasureRange")]
    private float MeasureRange()
    {
        float range = maxRange+10;
        RaycastHit hit;
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
