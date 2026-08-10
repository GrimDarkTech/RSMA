using System;
using System.Collections.Generic;
using UnityEngine;

namespace RSMA.MissionPlanner.Core
{
    public class PolygonPathGenerator
    {
        public static List<Vector3> GenerateZigzagPath(List<Vector3> polygonPoints, GenerationParams parameters)
        {
            if (polygonPoints == null || polygonPoints.Count < 3)
            {
                Debug.LogError("Polygon must have at least 3 points");
                return null;
            }

            // 1. Создаем замкнутый полигон
            List<Vector3> closedPolygon = new List<Vector3>(polygonPoints);
            if (!IsPolygonClosed(closedPolygon))
            {
                closedPolygon.Add(polygonPoints[0]);
            }

            // 2. Поворачиваем полигон вокруг оси Y
            Quaternion rotation = Quaternion.Euler(0, -parameters.angle, 0);
            List<Vector3> rotatedPolygon = RotatePolygon(closedPolygon, rotation);

            // 3. Находим Bounds в плоскости XZ
            Bounds bounds = GetPolygonBounds(rotatedPolygon);

            float startX = bounds.min.x + parameters.margin;
            float endX = bounds.max.x - parameters.margin;

            if (startX >= endX)
            {
                Debug.LogWarning("Margin is too large for this polygon size.");
                return new List<Vector3>();
            }

            // Рассчитываем шаги
            int numPasses = parameters.passes > 0 ? parameters.passes :
                Mathf.Max(1, Mathf.FloorToInt((endX - startX) / parameters.spacing) + 1);

            float stepX = (numPasses > 1) ? (endX - startX) / (numPasses - 1) : 0;

            List<Vector3> pathPoints = new List<Vector3>();
            bool moveForward = true;

            for (int i = 0; i < numPasses; i++)
            {
                float x = (numPasses == 1) ? (startX + endX) / 2f : startX + i * stepX;

                // Находим пересечения по оси Z (для плоскости XZ)
                List<Vector3> intersections = FindIntersectionsWithPolygonXZ(rotatedPolygon, x);

                if (intersections.Count >= 2)
                {
                    // Сортируем по оси Z
                    intersections.Sort((a, b) => a.z.CompareTo(b.z));

                    Vector3 bottom = intersections[0];
                    Vector3 top = intersections[intersections.Count - 1];

                    // Задаем направление захода (зигзаг)
                    if (moveForward)
                    {
                        pathPoints.Add(bottom);
                        pathPoints.Add(top);
                    }
                    else
                    {
                        pathPoints.Add(top);
                        pathPoints.Add(bottom);
                    }

                    moveForward = !moveForward;
                }
            }

            // 4. Поворачиваем точки обратно
            Quaternion inverseRotation = Quaternion.Euler(0, parameters.angle, 0);
            for (int i = 0; i < pathPoints.Count; i++)
            {
                pathPoints[i] = inverseRotation * pathPoints[i];
            }

            // 5. Сглаживание
            if (parameters.cornerRadius > 0 && pathPoints.Count > 2)
            {
                pathPoints = SmoothCorners(pathPoints, parameters.cornerRadius);
            }

            return pathPoints;
        }

        #region Helper Methods

        private static bool IsPolygonClosed(List<Vector3> polygon)
        {
            return polygon.Count > 0 &&
                   Vector3.Distance(polygon[0], polygon[polygon.Count - 1]) < 0.01f;
        }

        private static List<Vector3> RotatePolygon(List<Vector3> polygon, Quaternion rotation)
        {
            List<Vector3> rotated = new List<Vector3>();
            foreach (var point in polygon)
            {
                rotated.Add(rotation * point);
            }
            return rotated;
        }

        private static Bounds GetPolygonBounds(List<Vector3> polygon)
        {
            if (polygon.Count == 0) return new Bounds();

            Vector3 min = polygon[0];
            Vector3 max = polygon[0];

            foreach (var point in polygon)
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static List<Vector3> FindIntersectionsWithPolygonXZ(List<Vector3> polygon, float x)
        {
            List<Vector3> intersections = new List<Vector3>();

            for (int i = 0; i < polygon.Count - 1; i++)
            {
                Vector3 p1 = polygon[i];
                Vector3 p2 = polygon[i + 1];

                // Проверяем, пересекает ли вертикальная линия x отрезок p1-p2 по оси X
                if ((p1.x <= x && p2.x > x) || (p2.x <= x && p1.x > x))
                {
                    float t = (x - p1.x) / (p2.x - p1.x);
                    float z = p1.z + t * (p2.z - p1.z);
                    float y = p1.y + t * (p2.y - p1.y); // Сохраняем корректную высоту

                    intersections.Add(new Vector3(x, y, z));
                }
            }

            return intersections;
        }

        private static List<Vector3> SmoothCorners(List<Vector3> points, float radius)
        {
            List<Vector3> smoothed = new List<Vector3>();
            if (points.Count < 3) return points;

            smoothed.Add(points[0]);

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 prev = points[i - 1];
                Vector3 curr = points[i];
                Vector3 next = points[i + 1];

                Vector3 dir1 = (prev - curr).normalized;
                Vector3 dir2 = (next - curr).normalized;

                float dist1 = Vector3.Distance(curr, prev);
                float dist2 = Vector3.Distance(curr, next);

                // Ограничиваем радиус половиной расстояния до соседних точек
                float actualRadius = Mathf.Min(radius, dist1 * 0.4f, dist2 * 0.4f);

                Vector3 p1 = curr + dir1 * actualRadius;
                Vector3 p2 = curr + dir2 * actualRadius;

                smoothed.Add(p1);
                smoothed.Add(p2);
            }

            smoothed.Add(points[points.Count - 1]);
            return smoothed;
        }

        #endregion
    }

    [Serializable]
    public class GenerationParams
    {

        [Tooltip("Расстояние между параллельными линиями (ширина полосы)")]
        public float spacing = 1.0f;
        [Tooltip("Угол поворота зигзага в градусах (0 - горизонтально, 90 - вертикально)")]
        public float angle = 0f;
        [Tooltip("Отступ от краев полигона")]
        public float margin = 0.5f;
        [Tooltip("Количество проходов (если 0 - автоматически)")]
        public int passes = 0;
        [Tooltip("Сглаживание углов (радиус скругления)")]
        public float cornerRadius = 0.3f;
    }
}