using UnityEngine;

/// <summary>
/// Implements properties and functionality of rangefinder
/// </summary>
public class RSMARangeFinderBase : RSMADataTransferSlave
{
    /// <summary>
    /// Maximum range that can be measured by rangefinder
    /// </summary>
    public float maxRange;
    /// <summary>
    /// Viewing angle of the rangefinder. The angle lies in the zx plane (horizontal) , z axis (forward) - is the bisector of the angle
    /// </summary>
    public float angle = 1;
    /// <summary>
    /// Number of rays casted on angle
    /// </summary>
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
    /// <summary>
    /// Sends measured range via datatransfer protocol
    /// </summary>
    /// <returns>Returns measured range coverted to string</returns>
    public override string SendData()
    {
        return (MeasureRange().ToString());
    }
    private void Start()
    {
        gameObject.layer = 2;
    }
}
