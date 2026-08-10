using RSMA.MissionPlanner.Core;
using RSMA.uDTP.Topics;
using System.Collections.Generic;
using UnityEngine;

namespace RSMA.MissionPlanner.Interaction
{
    public class TrajectoryVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private GameObject _pointPrefab;
        [SerializeField] private Material _pointDefaultMaterial;
        [SerializeField] private Material _pointSelectedMaterial;
        [SerializeField] private Material _trajectoryLineMaterial;
        [SerializeField] private float _sphereRadius = 0.3f;
        [SerializeField] private Color _trajectoryColor = Color.green;
        [SerializeField] private Color _pointColor = Color.blue;
        [SerializeField] private bool _showLabels = true;

        private List<GameObject> _pointVisuals = new List<GameObject>();
        private LineRenderer _trajectoryLine;
        private int _selectedVisualIndex = -1;
        private MissionManager _manager;

        private void Start()
        {
            _manager = MissionManager.Instance;
            SetupLineRenderer();
            SubscribeToEvents();

            // Если префаб не задан - создаем базовую сферу
            if (_pointPrefab == null)
                CreateDefaultPointPrefab();
        }

        private void CreateDefaultPointPrefab()
        {
            // Создаем префаб для точки
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prefab.name = "PointPrefab";

            // Добавляем компонент PointVisual
            var visual = prefab.AddComponent<PointVisual>();

            // Добавляем Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(prefab.transform);
            labelObj.transform.localPosition = new Vector3(0, 0.5f, 0);
            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.fontSize = 24;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            //visual.SetLabel(textMesh);

            // Убираем коллайдер для основного объекта (используем триггер)
            var collider = prefab.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            // Добавляем Collider для взаимодействия
            var sphereCollider = prefab.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;

            _pointPrefab = prefab;
            _pointPrefab.SetActive(false);
        }

        private void SetupLineRenderer()
        {
            var lineObj = new GameObject("TrajectoryLine");
            lineObj.transform.SetParent(transform);
            _trajectoryLine = lineObj.AddComponent<LineRenderer>();
            _trajectoryLine.material = _trajectoryLineMaterial ?? new Material(Shader.Find("Sprites/Default"));
            _trajectoryLine.startColor = _trajectoryColor;
            _trajectoryLine.endColor = _trajectoryColor;
            _trajectoryLine.startWidth = 0.05f;
            _trajectoryLine.endWidth = 0.05f;
            _trajectoryLine.positionCount = 0;
        }

        private void SubscribeToEvents()
        {
            _manager.OnPointsChanged += UpdateVisualization;
            _manager.OnPointSelected += OnPointSelected;
            _manager.OnPointAdded += OnPointAdded;
            _manager.OnPointDeleted += OnPointDeleted;
        }

        private void UpdateVisualization()
        {
            var points = _manager.Points;

            // Обновляем визуальные объекты
            while (_pointVisuals.Count < points.Count)
            {
                var visual = Instantiate(_pointPrefab, transform);
                visual.SetActive(true);
                _pointVisuals.Add(visual);
            }

            while (_pointVisuals.Count > points.Count)
            {
                var last = _pointVisuals[_pointVisuals.Count - 1];
                _pointVisuals.RemoveAt(_pointVisuals.Count - 1);
                Destroy(last);
            }

            for (int i = 0; i < points.Count; i++)
            {
                var visual = _pointVisuals[i];
                var pointVisual = visual.GetComponent<PointVisual>();

                if (pointVisual != null)
                {
                    pointVisual.Initialize(i, points[i].position, _pointColor, _sphereRadius);
                    pointVisual.SetSelected(i == _selectedVisualIndex);
                }
                else
                {
                    // Fallback: просто устанавливаем позицию
                    visual.transform.position = points[i].position;
                    visual.transform.localScale = Vector3.one * _sphereRadius * 2;

                    var renderer = visual.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = (i == _selectedVisualIndex) ? _pointSelectedMaterial : _pointDefaultMaterial;
                    }
                }
            }

            UpdateTrajectoryLine();
        }

        private void UpdateTrajectoryLine()
        {
            var points = _manager.Points;
            _trajectoryLine.positionCount = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                _trajectoryLine.SetPosition(i, points[i].position);
            }
        }

        private void OnPointSelected(int index)
        {
            // Снимаем выделение со старой точки
            if (_selectedVisualIndex >= 0 && _selectedVisualIndex < _pointVisuals.Count)
            {
                var oldVisual = _pointVisuals[_selectedVisualIndex].GetComponent<PointVisual>();
                if (oldVisual != null)
                    oldVisual.SetSelected(false);
            }

            _selectedVisualIndex = index;

            // Выделяем новую точку
            if (index >= 0 && index < _pointVisuals.Count)
            {
                var newVisual = _pointVisuals[index].GetComponent<PointVisual>();
                if (newVisual != null)
                    newVisual.SetSelected(true);
            }
        }

        private void OnPointAdded(TrajectoryPoint point)
        {
            UpdateVisualization();
        }

        private void OnPointDeleted(int index)
        {
            _selectedVisualIndex = -1;
            UpdateVisualization();
        }

        // Вспомогательный метод для обновления цвета точки
        public void SetPointColor(int index, Color color)
        {
            if (index >= 0 && index < _pointVisuals.Count)
            {
                var visual = _pointVisuals[index].GetComponent<PointVisual>();
                if (visual != null)
                    visual.SetColor(color);
            }
        }

        private void OnDestroy()
        {
            if (_pointPrefab != null && _pointPrefab.scene.rootCount == 0)
                Destroy(_pointPrefab);
        }
    }
}