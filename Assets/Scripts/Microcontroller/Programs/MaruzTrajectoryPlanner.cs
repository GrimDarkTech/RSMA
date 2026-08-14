using RSMA.MissionPlanner.Core;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using UnityEngine;
using UnityEngine.UIElements;

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

    [Header("ROI Settings")]
    public GenerationParams polygonParams = new GenerationParams();

    [Header("Gizmos & Visualization Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color polygonAreaColor = new Color(0f, 1f, 0f, 0.25f);
    [SerializeField] private Color polygonBorderColor = Color.green;
    [SerializeField] private Color targetPathColor = Color.cyan;
    [SerializeField] private Color currentPathColor = Color.yellow;
    [SerializeField] private Color pointColor = Color.red;
    [SerializeField] private float pointSize = 0.15f;


    private List<TrajectoryPoint> currentPath = new List<TrajectoryPoint>();
    private int targetPointIdx = 0;
    private bool isExecutingPath = false;

    private Mesh polygonMesh;
    private List<Vector3> lastMeshTargets = new List<Vector3>();

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

    [ContextMenu("Load Mission")]
    public void LoadMission()
    {
        var mission = MissionSerializer.LoadFromJson();

        if (mission == null || mission.Count == 0)
        {
            Debug.LogWarning("No mission data loaded");
            return;
        }

        // Очищаем текущие цели
        targets.Clear();

        // Добавляем координаты из загруженных точек
        foreach (var point in mission)
        {
            targets.Add(point.position);
        }

        Debug.Log($"Loaded {targets.Count} target points from mission");
    }

    [ContextMenu("Generate Polygon Path")]
    public void GeneratePolygonPath()
    {
        if (targets == null || targets.Count < 3)
        {
            Debug.LogWarning("Need at least 3 targets to form a polygon");
            return;
        }

        // Генерируем зигзагообразный маршрут
        List<Vector3> pathPoints = PolygonPathGenerator.GenerateZigzagPath(targets, polygonParams);

        if (pathPoints != null && pathPoints.Count > 0)
        {
            // Очищаем старые целевые точки
            targets.Clear();

            // Добавляем сгенерированный маршрут
            targets.AddRange(pathPoints);

            Debug.Log($"Generated {targets.Count} waypoints inside polygon");
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
        if (!showGizmos) return;

        // 1. Отрисовка полигона (зона покрытия ROI)
        if (targets != null && targets.Count >= 3)
        {
            DrawFilledPolygon();
            DrawPolygonOutline();
        }

        // 2. Отрисовка траектории точек (targets)
        if (targets != null && targets.Count > 0)
        {
            Gizmos.color = targetPathColor;
            for (int i = 0; i < targets.Count - 1; i++)
            {
                Gizmos.DrawLine(targets[i], targets[i + 1]);
            }

            Gizmos.color = pointColor;
            foreach (var point in targets)
            {
                Gizmos.DrawSphere(point, pointSize);
            }
        }

        // 3. Отрисовка сгенерированной динамической траектории (currentPath)
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = currentPathColor;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i + 1].position);
            }

            // Выделяем текущую активную цель на траектории
            if (isExecutingPath && targetPointIdx < currentPath.Count)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(currentPath[targetPointIdx].position, pointSize * 1.5f);
            }
        }
    }

    /// <summary>
    /// Рисует замкнутый контур полигона
    /// </summary>
    private void DrawPolygonOutline()
    {
        Gizmos.color = polygonBorderColor;
        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 p1 = targets[i];
            Vector3 p2 = targets[(i + 1) % targets.Count];
            Gizmos.DrawLine(p1, p2);
        }
    }

    /// <summary>
    /// Создает и отрисовывает заливку полигона с помощью Triangulator
    /// </summary>
    private void DrawFilledPolygon()
    {
        if (HasTargetsChanged())
        {
            GeneratePolygonMesh();
        }

        if (polygonMesh != null)
        {
            Gizmos.color = polygonAreaColor;
            Gizmos.DrawMesh(polygonMesh);
        }
    }

    private bool HasTargetsChanged()
    {
        if (lastMeshTargets.Count != targets.Count) return true;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != lastMeshTargets[i]) return true;
        }
        return false;
    }

    private void GeneratePolygonMesh()
    {
        // Преобразуем 3D точки в 2D (плоскость XZ) для триангуляции
        Vector2[] vertices2D = new Vector2[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            vertices2D[i] = new Vector2(targets[i].x, targets[i].z);
        }

        // Выполняем триангуляцию
        Triangulator triangulator = new Triangulator(vertices2D);
        int[] indices = triangulator.Triangulate();

        // Формируем Mesh
        polygonMesh = new Mesh();
        polygonMesh.vertices = targets.ToArray();
        polygonMesh.triangles = indices;
        polygonMesh.RecalculateNormals();

        // Кэшируем текущее состояние
        lastMeshTargets = new List<Vector3>(targets);
    }
}

/// <summary>
/// Простой триангулятор Ear-Clipping для генерации полигональной сетки
/// </summary>
public class Triangulator
{
    private List<Vector2> m_points = new List<Vector2>();

    public Triangulator(Vector2[] points)
    {
        m_points = new List<Vector2>(points);
    }

    public int[] Triangulate()
    {
        List<int> indices = new List<int>();
        int n = m_points.Count;
        if (n < 3) return indices.ToArray();

        int[] V = new int[n];
        if (Area() > 0)
        {
            for (int v = 0; v < n; v++) V[v] = v;
        }
        else
        {
            for (int v = 0; v < n; v++) V[v] = (n - 1) - v;
        }

        int nv = n;
        int count = 2 * nv;

        for (int m = 0, v = nv - 1; nv > 2;)
        {
            if ((count--) <= 0) return indices.ToArray();

            int u = v;
            if (nv <= u) u = 0;
            v = u + 1;
            if (nv <= v) v = 0;
            int w = v + 1;
            if (nv <= w) w = 0;

            if (Snip(u, v, w, nv, V))
            {
                int a, b, c, s, t;
                a = V[u];
                b = V[v];
                c = V[w];
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                m++;

                for (s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                nv--;
                count = 2 * nv;
            }
        }

        indices.Reverse();
        return indices.ToArray();
    }

    private float Area()
    {
        int n = m_points.Count;
        float A = 0.0f;
        for (int p = n - 1, q = 0; q < n; p = q++)
        {
            Vector2 pPoint = m_points[p];
            Vector2 qPoint = m_points[q];
            A += pPoint.x * qPoint.y - qPoint.x * pPoint.y;
        }
        return (A * 0.5f);
    }

    private bool Snip(int u, int v, int w, int n, int[] V)
    {
        int p;
        Vector2 A = m_points[V[u]];
        Vector2 B = m_points[V[v]];
        Vector2 C = m_points[V[w]];

        if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x)))) return false;

        for (p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w)) continue;
            Vector2 P = m_points[V[p]];
            if (InsideTriangle(A, B, C, P)) return false;
        }
        return true;
    }

    private bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
        float cCROSSap, bCROSScp, aCROSSbp;

        ax = C.x - B.x; ay = C.y - B.y;
        bx = A.x - C.x; by = A.y - C.y;
        cx = B.x - A.x; cy = B.y - A.y;
        apx = P.x - A.x; apy = P.y - A.y;
        bpx = P.x - B.x; bpy = P.y - B.y;
        cpx = P.x - C.x; cpy = P.y - C.y;

        aCROSSbp = ax * bpy - ay * bpx;
        cCROSSap = cx * apy - cy * apx;
        bCROSScp = bx * cpy - by * cpx;

        return ((aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f));
    }
}