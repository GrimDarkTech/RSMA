using System;
using System.Collections.Generic;
using UnityEngine;

namespace RSMA.MissionPlanner.Core
{
    /// <summary>
    /// Класс для создания зигзагообразного маршрута внутри полигона
    /// Аналог ROI (Region of Interest) в QGroundControl
    /// </summary>
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

            // 3. Создаем внутренний отступ (Margin) со всех сторон полигона
            List<Vector3> targetPolygon = rotatedPolygon;
            if (parameters.margin > 0)
            {
                targetPolygon = InsetPolygon(rotatedPolygon, parameters.margin);
                if (targetPolygon.Count < 3)
                {
                    Debug.LogWarning("Margin is too large. Inset polygon has no area left.");
                    return new List<Vector3>();
                }
            }

            // 4. Находим Bounds уменьшенного полигона в плоскости XZ
            Bounds bounds = GetPolygonBounds(targetPolygon);
            float startX = bounds.min.x;
            float endX = bounds.max.x;

            if (startX >= endX)
            {
                Debug.LogWarning("Margin is too large for this polygon size.");
                return new List<Vector3>();
            }

            // 5. Рассчитываем количество проходов
            int numPasses = parameters.passes > 0 ? parameters.passes :
                Mathf.Max(1, Mathf.FloorToInt((endX - startX) / parameters.spacing) + 1);

            float stepX = (numPasses > 1) ? (endX - startX) / (numPasses - 1) : 0;

            List<Vector3> pathPoints = new List<Vector3>();
            bool moveForward = true;

            for (int i = 0; i < numPasses; i++)
            {
                float x = (numPasses == 1) ? (startX + endX) / 2f : startX + i * stepX;

                // Находим пересечения по оси Z с уменьшенным полигоном
                List<Vector3> intersections = FindIntersectionsWithPolygonXZ(targetPolygon, x);

                if (intersections.Count >= 2)
                {
                    // Сортируем пересечения по оси Z
                    intersections.Sort((a, b) => a.z.CompareTo(b.z));

                    Vector3 bottom = intersections[0];
                    Vector3 top = intersections[intersections.Count - 1];

                    // Добавляем точки с чередованием направления (зигзаг)
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

            // 6. Поворачиваем точки обратно
            Quaternion inverseRotation = Quaternion.Euler(0, parameters.angle, 0);
            for (int i = 0; i < pathPoints.Count; i++)
            {
                pathPoints[i] = inverseRotation * pathPoints[i];
            }

            // 7. Сглаживание углов
            if (parameters.cornerRadius > 0 && pathPoints.Count > 2)
            {
                pathPoints = SmoothCorners(pathPoints, parameters.cornerRadius);
            }

            return pathPoints;
        }

        #region Helper Methods

        private static List<Vector3> InsetPolygon(List<Vector3> polygon, float margin)
        {
            if (margin <= 0) return new List<Vector3>(polygon);

            bool isClosed = IsPolygonClosed(polygon);
            int realCount = isClosed ? polygon.Count - 1 : polygon.Count;
            if (realCount < 3) return new List<Vector3>(polygon);

            // 1. Точно определяем Signed Area (ориентацию обхода) в плоскости XZ
            float signedArea = 0f;
            for (int i = 0; i < realCount; i++)
            {
                Vector3 p1 = polygon[i];
                Vector3 p2 = polygon[(i + 1) % realCount];
                signedArea += (p1.x * p2.z - p2.x * p1.z);
            }

            // Если signedArea > 0 — против часовой стрелки, иначе — по часовой
            bool isCCW = signedArea > 0;

            // 2. Строим сдвинутые линии для каждого ребра
            // Линия задается как: Point + Direction * t
            Vector3[] shiftedP1 = new Vector3[realCount];
            Vector3[] shiftedP2 = new Vector3[realCount];

            for (int i = 0; i < realCount; i++)
            {
                Vector3 p1 = polygon[i];
                Vector3 p2 = polygon[(i + 1) % realCount];
                Vector3 dir = (p2 - p1).normalized;

                // Вычисляем внутренюю нормаль к ребру
                // Для CCW внутренняя нормаль смотрит «влево» относительно направления движения (-dir.z, 0, dir.x)
                Vector3 inNormal = isCCW ? new Vector3(-dir.z, 0, dir.x) : new Vector3(dir.z, 0, -dir.x);

                // Сдвигаем оба конца ребра строго по нормали внутрь
                shiftedP1[i] = p1 + inNormal * margin;
                shiftedP2[i] = p2 + inNormal * margin;
            }

            // 3. Находим точки пересечения смежных сдвинутых прямых
            List<Vector3> insetPolygon = new List<Vector3>();

            for (int i = 0; i < realCount; i++)
            {
                int prevIdx = (i - 1 + realCount) % realCount;

                Vector3 A = shiftedP1[prevIdx];
                Vector3 B = shiftedP2[prevIdx];
                Vector3 C = shiftedP1[i];
                Vector3 D = shiftedP2[i];

                // Находим точку пересечения прямых AB и CD в плоскости XZ
                Vector3 intersection = GetLineIntersectionXZ(A, B, C, D);
                insetPolygon.Add(intersection);
            }

            if (isClosed && insetPolygon.Count > 0)
            {
                insetPolygon.Add(insetPolygon[0]);
            }

            return insetPolygon;
        }

        /// <summary>
        /// Находит точку пересечения двух бесконечных прямых AB и CD в плоскости XZ
        /// </summary>
        private static Vector3 GetLineIntersectionXZ(Vector3 A, Vector3 B, Vector3 C, Vector3 D)
        {
            float dy1 = B.z - A.z;
            float dx1 = B.x - A.x;
            float dy2 = D.z - C.z;
            float dx2 = D.x - C.x;

            float det = dx1 * dy2 - dy1 * dx2;

            // Если линии параллельны, берем исходную сдвинутую точку
            if (Mathf.Abs(det) < 0.0001f)
            {
                return C;
            }

            float t = ((C.x - A.x) * dy2 - (C.z - A.z) * dx2) / det;

            return new Vector3(A.x + t * dx1, (A.y + C.y) * 0.5f, A.z + t * dy1);
        }

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
                    float y = p1.y + t * (p2.y - p1.y); // Сохраняем перепад высот

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

                // Ограничиваем радиус скругления безопасной длиной соседних отрезков
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

        [Tooltip("Угол поворота зигзага в градусах (0 - вдоль X, 90 - вдоль Z)")]
        public float angle = 0f;

        [Tooltip("Равномерный отступ от всех краев полигона")]
        public float margin = 0.5f;

        [Tooltip("Количество проходов (если 0 - рассчитывается автоматически)")]
        public int passes = 0;

        [Tooltip("Сглаживание углов на разворотах (радиус скругления)")]
        public float cornerRadius = 0.3f;
    }
}