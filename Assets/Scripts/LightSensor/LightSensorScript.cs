using UnityEngine;

/// <summary>
/// Implements properties and functionality of light sensor
/// </summary>
public class LightSensorScript : RSMADataTransferSlave
{
    /// <summary>
    /// Light intensity value determined by sensor
    /// </summary>
    public float lightIntensity;
    /// <summary>
    /// Scale factor for calculating light intensity
    /// </summary>
    [Range(0f,1f)]
    public float lightCoefficient = 1f;
    
    private Light[] lights;

    void Start()
    {
        lights = FindObjectsOfType<Light>();
    }
    void Update()
    {
        lightIntensity = 0;
        foreach(Light light in lights)
        {
            float distance = Vector3.Distance(gameObject.transform.position, light.transform.position);
            Vector3 rayDirection = Vector3.Normalize(light.transform.position - gameObject.transform.position);
            RaycastHit hit;
            if(Physics.Raycast(gameObject.transform.position, rayDirection, out hit))
            {
                if (hit.distance >= distance)
                {
                    if (light.enabled)
                    {
                        Debug.DrawLine(gameObject.transform.position, light.transform.position, Color.blue);
                        lightIntensity += light.intensity * (Mathf.Pow(light.range, 2) / Mathf.Pow(distance, 2)) * lightCoefficient / Mathf.Sqrt(distance);
                    }
                }
            }
        }
        data = lightIntensity.ToString();
    }
    public override string SendData()
    {
        return data;
    }
}
