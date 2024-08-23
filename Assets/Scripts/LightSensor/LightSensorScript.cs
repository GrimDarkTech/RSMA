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

    private void Start()
    {
        lights = FindObjectsOfType<Light>();
    }
    private void Update()
    {
        lightIntensity = 0;
        foreach (Light light in lights)
        {
            if (light != null)
            {
                Vector3 rayDirection = Vector3.Normalize(gameObject.transform.position - light.transform.position);
                RaycastHit hit;
                if (Physics.Raycast(light.transform.position, rayDirection, out hit))
                {
                    if (hit.collider.gameObject == this.gameObject)
                    {
                        if (light.enabled)
                        {
                            Debug.DrawLine(gameObject.transform.position, light.transform.position, Color.blue);
                            lightIntensity += light.intensity * (Mathf.Pow(light.range, 2) / Mathf.Pow(hit.distance, 2)) * lightCoefficient / Mathf.Sqrt(hit.distance);
                        }
                    }
                }
            }
        }
        data = lightIntensity.ToString();
    }
    /// <summary>
    /// Sends light intensity data via a data transfer protocol
    /// </summary>
    /// <returns>Returns light intensity value</returns>
    public override string SendData()
    {
        return data;
    }
}
