using UnityEngine;

/// <summary>
/// Implements properties and functionality of rangefinder
/// </summary>
[HelpURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Electronics/Setting_up_rangefinders.md")]
public class RSMARangeFinderBase : RSMADataTransferSlave
{
    /// <summary>
    /// Maximum range that can be measured by rangefinder
    /// </summary>
    public float maxRange;
    /// <summary>
    /// Viewing angle of the rangefinder. The angle lies in the zx plane (horizontal) , z axis (forward) - is the bisector of the angle
    /// </summary>
    [Min(0.5f)]
    public float angle = 1;
    /// <summary>
    /// Number of rays casted on angle
    /// </summary>
    [Min(0)]
    public int numberOfRays = 1;
    /// <summary>
    /// If True, draws rays of rangefinder
    /// </summary>
    public bool isDrawRays = false;
    /// <summary>
    /// If True, draws rays of rangefinder
    /// </summary>
    public bool isDebugMode = false;
    /// <summary>
    /// If isDebugMode is True, contains measured range
    /// </summary>
    public float debugRange = 0f;


    [ContextMenu("MeasureRange")]
    private float MeasureRange()
    {
        float step = angle / (numberOfRays + 1);
        float range = maxRange;

        for (float i = -angle * 0.5f; i <= angle * 0.5f; i = i + step)
        {
            Vector3 rayDirection = Quaternion.AngleAxis(i, transform.up) * transform.forward;

            RaycastHit hit;
            Ray ray;

            ray = new Ray(transform.position, rayDirection);
            if (Physics.Raycast(ray, out hit, maxRange))
            {
                Debug.DrawLine(gameObject.transform.position, hit.point);

                if (hit.distance < range)
                    range = hit.distance;
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

    private void OnDrawGizmos()
    {
        if (isDrawRays)
        {
            Gizmos.color = Color.red;
            float step = angle / (numberOfRays + 1);
            for (float i = -angle * 0.5f; i <= angle * 0.5f; i = i + step)
            {
                Vector3 directon = Quaternion.AngleAxis(i, transform.up) * transform.forward;

                Gizmos.DrawLine(transform.position, transform.position + directon * maxRange);
            }

        }
        if (isDebugMode) 
        {
            debugRange = MeasureRange();
        }
    }
}
