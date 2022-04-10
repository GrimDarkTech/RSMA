using UnityEngine;

public class RangeFinderBaseScript : I2CSlaveScript
{
    public float maxRange;
    [ContextMenu("measureRange")]
    private float measureRange()
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
    public override string sendData()
    {
        return (""+measureRange());
    }
    private void Start()
    {
        gameObject.layer = 2;
    }
}
