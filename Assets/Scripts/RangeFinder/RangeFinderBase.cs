using UnityEngine;

public class RangeFinderBase : DataTransgerSlaveScript
{
    public float maxRange;
    [ContextMenu("MeasureRange")]
    private float MeasureRange()
    {
        float range = 0;
        RaycastHit hit;
        Ray ray = new Ray(gameObject.transform.position, gameObject.transform.forward);
        if(Physics.Raycast(ray, out hit, maxRange))
        {
            Debug.DrawLine(gameObject.transform.position, hit.point);
            range = hit.distance;
            return range;
        }
        else
        {
            return maxRange;
        }
            
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
