using UnityEngine;

public class RangeFinderBaseScript : MonoBehaviour
{
    public float maxRange;
    public float measureSignalSpeed;
    public bool trigPortID;
    public bool echoPortID;
    public GPIOBaseScript GPIOScript;
    private int trigValue = 0;
    private float measureRange()
    {
        float range = 0;
        RaycastHit hit;
        Ray ray = new Ray(gameObject.transform.position, gameObject.transform.forward);
        if(Physics.Raycast(ray, out hit, maxRange))
        {
            range = hit.distance;
            return range;
        }
        else
            return 0;
    }
}
