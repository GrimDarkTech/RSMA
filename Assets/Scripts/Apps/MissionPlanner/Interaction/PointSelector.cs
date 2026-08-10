using RSMA.MissionPlanner.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RSMA.MissionPlanner.Interaction
{
    public class PointSelector : MonoBehaviour
    {
        [Header("Selection Settings")]
        [SerializeField] private float _selectionRadius = 0.5f;
        [SerializeField] private LayerMask _pointLayerMask;
        [SerializeField] private Color _highlightColor = Color.yellow;
        [SerializeField] private float _highlightIntensity = 1.5f;

        [Header("UI Filter")]
        [SerializeField] private bool _ignoreUI = true;

        public Camera _camera;
        private MissionManager _manager;
        private GameObject _selectedVisual;
        private Material _originalMaterial;
        private Material _highlightMaterial;
        private int _selectedIndex = -1;

        // Событие при изменении выделения
        public System.Action<int> OnSelectionChanged;

        private void Start()
        {
            _manager = MissionManager.Instance;

            // Создаем материал для подсветки
            _highlightMaterial = new Material(Shader.Find("Standard"));
            _highlightMaterial.color = _highlightColor;
            _highlightMaterial.EnableKeyword("_EMISSION");
            _highlightMaterial.SetColor("_EmissionColor", _highlightColor * _highlightIntensity);

            // Подписываемся на события менеджера
            _manager.OnPointSelected += OnManagerPointSelected;
            _manager.OnPointDeleted += OnPointDeleted;
            _manager.OnPointsChanged += OnPointsChanged;
        }

        private void Update()
        {


            // Обработка клика правой кнопкой для снятия выделения
            if (Input.GetMouseButtonDown(1))
            {
                ClearSelection();
            }

            // Обработка клика по точке для выделения (дублируем из Raycaster, но с явным выделением)
            if (Input.GetMouseButtonDown(0))
            {
                if (!_ignoreUI && IsPointerOverUI())
                {
                    Debug.Log("Over UI!");
                    return;
                }

                TrySelectPoint();
            }
        }

        private void TrySelectPoint()
        {
            if (_camera is null) 
            {
                Debug.Log("Camera is null");
            }
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, _pointLayerMask))
            {
                // Проверяем, попали ли в визуальный объект точки
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

            // Если клик не по точке, но по поверхности - снимаем выделение
            // (но не мешаем добавлению новой точки)
        }

        private void OnManagerPointSelected(int index)
        {
            ClearSelectionVisual();
            _selectedIndex = index;

            if (index >= 0 && index < _manager.Points.Count)
            {
                HighlightPoint(index);
            }

            OnSelectionChanged?.Invoke(index);
        }

        private void HighlightPoint(int index)
        {
            // Находим визуальный объект точки по индексу
            var visual = FindPointVisual(index);
            if (visual != null)
            {
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    _originalMaterial = renderer.material;
                    renderer.material = _highlightMaterial;
                    _selectedVisual = visual;
                }
            }
        }

        private void ClearSelectionVisual()
        {
            if (_selectedVisual != null)
            {
                var renderer = _selectedVisual.GetComponent<Renderer>();
                if (renderer != null && _originalMaterial != null)
                {
                    renderer.material = _originalMaterial;
                }
                _selectedVisual = null;
                _originalMaterial = null;
            }
        }

        private GameObject FindPointVisual(int index)
        {
            // Ищем все объекты с компонентом PointVisual
            var visuals = Object.FindObjectsByType<PointVisual>(FindObjectsSortMode.None);
            foreach (var visual in visuals)
            {
                if (visual.PointIndex == index)
                    return visual.gameObject;
            }
            return null;
        }

        private void OnPointDeleted(int index)
        {
            if (_selectedIndex == index)
            {
                ClearSelectionVisual();
                _selectedIndex = -1;
                OnSelectionChanged?.Invoke(-1);
            }
        }

        private void OnPointsChanged()
        {
            // Если выделение стало невалидным - снимаем
            if (_selectedIndex >= _manager.Points.Count)
            {
                ClearSelectionVisual();
                _selectedIndex = -1;
                OnSelectionChanged?.Invoke(-1);
            }
        }

        public void ClearSelection()
        {
            if (_selectedIndex >= 0)
            {
                _manager.SelectPoint(-1);
            }
            else
            {
                ClearSelectionVisual();
                _selectedIndex = -1;
                OnSelectionChanged?.Invoke(-1);
            }
        }

        public int GetSelectedIndex() => _selectedIndex;

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                //Debug.LogWarning("EventSystem не найден!");
                return false;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            //Debug.Log($"Найдено UI объектов: {results.Count}");
            //foreach (var result in results)
            //{
            //    Debug.Log($"UI объект: {result.gameObject.name}");
            //}

            return results.Count > 0;
        }
    }
}