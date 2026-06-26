using System;
using System.Collections.Generic;
using UnityEngine;
using RSMA.uDTP;
using RSMA.uDTP.Topics;

public class MaruzTrajectoryPlanner : MonoBehaviour
{
    public List<Vector3> targets = new List<Vector3>();
    public float time = 3.0f;
    public float deltaTime = 0.1f;
    public float vStart = 0;
    public float vEnd = 0;
    public float maxPathVelocity = 1.0f;

    [Header("Navigation Settings")]
    public float lookAheadDistance = 0.4f; // Перенесли логику "взгляда вперед" сюда

    private List<TrajectoryPoint> currentPath = new List<TrajectoryPoint>();
    private int targetPointIdx = 0;
    private bool isExecutingPath = false;

    [ContextMenu("Generate and Start")]
    public void GenerateAndMove()
    {
        List<TrajectoryPoint> path = new List<TrajectoryPoint>();

        if (targets != null && targets.Count > 1)
        {
            for (int i = 0; i < targets.Count - 1; i++)
            {
                var segment = GeneratePath(targets[i], targets[i + 1], time, deltaTime, vStart, vEnd);
                path.AddRange(segment);
            }
        }

        if (path.Count > 0)
        {
            currentPath = path;
            targetPointIdx = 0;
            isExecutingPath = true;
        }
    }

    private void Update()
    {
        if (!isExecutingPath || currentPath == null || currentPath.Count == 0) return;

        // Если дошли до конца массива путей — передаем управление контроллеру (он сам затормозит на финише)
        if (targetPointIdx >= currentPath.Count)
        {
            isExecutingPath = false;
            return;
        }

        // Получаем текущую позицию робота для расчета Pure Pursuit
        var robotPose = DataBroker.GetState<RSMA.uDTP.Topics.Pose>("MaruzPose");

        // Твоя логика: перебираем и пропускаем точки, которые ближе чем lookAheadDistance
        while (targetPointIdx < currentPath.Count - 1 &&
               Vector3.Distance(robotPose.position, currentPath[targetPointIdx].position) < lookAheadDistance)
        {
            targetPointIdx++;
        }

        // Берем найденную точку, обновляем timestamp (если нужно) и шлем контроллеру
        TrajectoryPoint nextTarget = currentPath[targetPointIdx];
        nextTarget.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish("MaruzTargetPoint", nextTarget);
    }

    public List<TrajectoryPoint> GeneratePath(Vector3 startPos, Vector3 endPos, float duration, float stepTime, float startVel, float endVel)
    {
        List<TrajectoryPoint> path = new List<TrajectoryPoint>();
        int steps = Mathf.CeilToInt(duration / stepTime);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);

            float vel;
            if (t < 0.2f)
                vel = Mathf.Lerp(startVel, maxPathVelocity, t / 0.2f);
            else if (t > 0.8f)
                vel = Mathf.Lerp(maxPathVelocity, endVel, (t - 0.8f) / 0.2f);
            else
                vel = maxPathVelocity;

            path.Add(new TrajectoryPoint { position = pos, targetVelocity = vel });
        }
        return path;
    }
    private void OnDrawGizmos()
    {
        if (currentPath == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < currentPath.Count; i++)
        {
            Gizmos.DrawSphere(currentPath[i].position, 0.05f);
        }
    }
}