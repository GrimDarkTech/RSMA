using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlls trails rendering
/// </summary>
public class TrailsRenderer : MonoBehaviour
{

    public List<TrailRenderer> trails = new List<TrailRenderer>();


    [ContextMenu("Start rendering")]
    public void StartRendering()
    {
        for (int i = 0; i < trails.Count; i++)
        {
            trails[i].enabled = true;
        }
    }
    [ContextMenu("Stop rendering")]
    public void StopRendering()
    {
        foreach (TrailRenderer trail in trails)
        {
            for (int i = 0; i < trails.Count; i++)
            {
                trails[i].enabled = false;
            }
        }
    }
}
