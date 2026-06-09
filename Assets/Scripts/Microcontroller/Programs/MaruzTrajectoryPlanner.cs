using NUnit;
using NUnit.Framework;
using RSMA.uDTP.Topics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using UnityEngine;

public class MaruzTrajectoryPlanner : MonoBehaviour
{
    public List<Vector3> targets = new List<Vector3>();
    public float time = 3.0f;
    public float deltaTime = 0.1f;
    public float vStart = 0;
    public float vEnd = 0;
    public float maxPathVelocity = 1.0f; // Максимальная скорость на середине пути

    public MaruzFollower controller = null;

    [ContextMenu("Generate")]
    public void GenerateAndMove()
    {
        List<TrajectoryPoint> path = new List<TrajectoryPoint>();

        if (targets != null && controller != null)
        {
            for (int i = 0; i < targets.Count - 1; i++)
            {
                var segment = GeneratePath(targets[i], targets[i+1], time, deltaTime, vStart, vEnd);
                path.AddRange(segment);
            }
        }

        controller.SetNewPath(path);
    }

    public List<TrajectoryPoint> GeneratePath(Vector3 startPos, Vector3 endPos, float duration, float stepTime, float startVel, float endVel)
    {
        List<TrajectoryPoint> path = new List<TrajectoryPoint>();

        int steps = Mathf.CeilToInt(duration / stepTime);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;

            // Линейная интерполяция пути (для двух точек этого достаточно)
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);

            // Трапециевидный/колоколообразный профиль скорости (разгон -> удержание -> торможение)
            float vel;
            if (t < 0.2f) // Разгон (первые 20% пути)
                vel = Mathf.Lerp(startVel, maxPathVelocity, t / 0.2f);
            else if (t > 0.8f) // Торможение (последние 20% пути)
                vel = Mathf.Lerp(maxPathVelocity, endVel, (t - 0.8f) / 0.2f);
            else
                vel = maxPathVelocity;

            path.Add(new TrajectoryPoint { position = pos, targetVelocity = vel });
        }
        return path;
    }
}