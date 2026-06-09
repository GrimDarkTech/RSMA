using RSMA.uDTP.Topics;
using System.Collections.Generic;
using UnityEditor.Android;
using UnityEngine;

public class MaruzTrajectoryPlanner : MonoBehaviour
{
    public GameObject start = null;
    public GameObject end = null;
    public float time = 1;
    public float deltaTime = 0.1f;
    public float vStart = 0;
    public float vEnd = 0;

    public MaruzFollower controller = null;

    [ContextMenu("Generate")]
    public void GenerateAndMove() 
    {
        if (start != null && end != null) 
        {
            var path = GeneratePath(start.transform.position, end.transform.position, time, deltaTime, vStart, vEnd);

            if (controller != null) 
            {
                controller.pathQueue.Clear();
                foreach (var point in path)
                {
                    controller.pathQueue.Enqueue(point);
                }
            }
        }
    }



    public List<TrajectoryPoint> GeneratePath(Vector3 start, Vector3 end, float time, float deltaTime, float vStart, float vEnd)
    {
        List<TrajectoryPoint> path = new List<TrajectoryPoint>();
        int steps = Mathf.CeilToInt(time / deltaTime);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;

            // 1. Интерполяция позиции (можно заменить на сплайн для кривизны)
            Vector3 pos = Vector3.Lerp(start, end, t);

            // 2. Профиль скорости (линейная интерполяция скорости)
            float vel = Mathf.Lerp(vStart, vEnd, t);

            path.Add(new TrajectoryPoint { position = pos, targetVelocity = vel });
        }
        return path;
    }
}