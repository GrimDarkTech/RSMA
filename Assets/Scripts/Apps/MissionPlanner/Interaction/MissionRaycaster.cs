using RSMA.MissionPlanner.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RSMA.MissionPlanner.Interaction
{
    public class MissionRaycaster : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private LayerMask _groundLayerMask = -1;
        [SerializeField] private LayerMask _pointLayerMask;
        [SerializeField] private float _maxDistance = 100f;
        [SerializeField] private float _pointSelectionRadius = 0.5f;

        [Header("UI Filter")]
        [SerializeField] private bool _ignoreUI = true;

        public Camera _camera;
        private MissionManager _manager;
        private PointSelector _selector;

        private void Start()
        {
            _manager = MissionManager.Instance;
            _selector = GetComponent<PointSelector>();

            if (_selector == null)
                _selector = gameObject.AddComponent<PointSelector>();
        }

        private void Update()
        {
            // ЛКМ для добавления/выделения
            if (Input.GetMouseButtonDown(0))
            {
                if (!_ignoreUI && IsPointerOverUI())
                {
                    return;
                }
                HandleLeftClick();
            }

            // Delete/Backspace для удаления выделенной точки
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_manager.SelectedIndex >= 0)
                {
                    _manager.DeletePoint(_manager.SelectedIndex);
                }
            }

            // Ctrl+Z для отмены (можно реализовать позже)
            if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftControl))
            {
                // Undo
            }
        }

        private void HandleLeftClick()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Сначала проверяем попадание в точку
            if (Physics.Raycast(ray, out hit, _maxDistance, _pointLayerMask))
            {
                var pointVisual = hit.collider.GetComponentInParent<PointVisual>();
                if (pointVisual != null)
                {
                    int index = pointVisual.PointIndex;
                    if (index >= 0 && index < _manager.Points.Count)
                    {
                        _manager.SelectPoint(index);
                        return;
                    }
                }
            }

            // Если не попали в точку, пробуем добавить на поверхность
            if (Physics.Raycast(ray, out hit, _maxDistance, _groundLayerMask))
            {
                // Проверяем, не слишком ли близко к существующим точкам
                if (!IsNearExistingPoint(hit.point))
                {
                    _manager.AddPoint(hit.point, 0f);
                }
                else
                {
                    // Если рядом с точкой, но не попали в коллайдер - выделяем ближайшую
                    int nearest = FindNearestPoint(hit.point);
                    if (nearest >= 0)
                        _manager.SelectPoint(nearest);
                }
            }
        }

        private bool IsNearExistingPoint(Vector3 position)
        {
            var points = _manager.Points;
            foreach (var point in points)
            {
                if (Vector3.Distance(point.position, position) < _pointSelectionRadius)
                    return true;
            }
            return false;
        }

        private int FindNearestPoint(Vector3 position)
        {
            var points = _manager.Points;
            int nearest = -1;
            float minDist = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                float dist = Vector3.Distance(points[i].position, position);
                if (dist < minDist && dist < _pointSelectionRadius * 2)
                {
                    minDist = dist;
                    nearest = i;
                }
            }
            return nearest;
        }

        // Визуализация луча в редакторе (для отладки)
        private void OnDrawGizmosSelected()
        {
            if (_camera != null)
            {
                Gizmos.color = Color.yellow;
                Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                Gizmos.DrawRay(ray);
            }
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
                return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                if (result.gameObject.layer == 5)
                    return true;
            }

            return false;
        }
    }
}