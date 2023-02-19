using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class LightSensorScript : MonoBehaviour
{
    public float LightIntensity;
    private GameObject[] allGameObjects;
    Light[] lights;

    void Start()
    {
        lights = FindObjectsOfType<Light>();
        foreach(Light light in lights)
        {
            light.gameObject.tag = "Light";
            SphereCollider temp = light.gameObject.AddComponent<SphereCollider>();
            temp.radius = 0.1f;
        }
    }

    // Update is called once per frame
    void Update()
    {
        LightIntensity = 0;
        foreach(Light light in lights)
        {
            Vector3 lightPos = light.transform.position;
            RaycastHit hit;
            if(Physics.Raycast(gameObject.transform.position, lightPos-transform.position, out hit))
            {  
                if (hit.collider.gameObject.tag == "Light")
                { 
                    Debug.DrawRay(gameObject.transform.position, hit.point-transform.position, Color.blue);
                    float distance = (lightPos-transform.position).x*(lightPos-transform.position).x+(lightPos-transform.position).y*(lightPos-transform.position).y+(lightPos-transform.position).z*(lightPos-transform.position).z;
                    distance = Mathf.Sqrt(distance);
                    float tuner = light.range-distance;
                    Debug.Log(distance);
                    if (tuner < 0)
                    {
                        tuner = 0;
                    }
                    LightIntensity += light.intensity*tuner;
                }
            }
        }
    }
}
