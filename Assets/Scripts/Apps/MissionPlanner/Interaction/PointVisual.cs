using TMPro;
using UnityEngine;

namespace RSMA.MissionPlanner.Interaction
{
    [RequireComponent(typeof(Renderer))]
    public class PointVisual : MonoBehaviour
    {
        [SerializeField] private int _pointIndex = -1;
        [SerializeField] private TextMeshPro _labelText;
        [SerializeField] private GameObject _selectionRing;

        public int PointIndex => _pointIndex;

        private Renderer _renderer;
        private Material _defaultMaterial;
        private float _baseScale;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _defaultMaterial = _renderer.material;
            _baseScale = transform.localScale.x;

            if (_selectionRing != null)
                _selectionRing.SetActive(false);
        }

        public void Initialize(int index, Vector3 position, Color color, float radius = 0.3f)
        {
            _pointIndex = index;
            transform.position = position;
            transform.localScale = Vector3.one * radius * 2;

            if (_renderer != null)
                _renderer.material.color = color;

            UpdateLabel();
        }

        public void UpdateLabel()
        {
            if (_labelText != null)
            {
                _labelText.text = (_pointIndex + 1).ToString();
            }
        }

        public void SetSelected(bool selected)
        {
            if (_selectionRing != null)
                _selectionRing.SetActive(selected);

            // Можно изменить цвет или размер
            if (selected)
            {
                transform.localScale = Vector3.one * _baseScale * 1.2f;
            }
            else
            {
                transform.localScale = Vector3.one * _baseScale;
            }
        }

        public void SetColor(Color color)
        {
            if (_renderer != null)
                _renderer.material.color = color;
        }

        public void SetIndex(int index)
        {
            _pointIndex = index;
            UpdateLabel();
        }

        // Метод для проверки попадания мыши
        private void OnMouseEnter()
        {
            // Можно добавить эффект наведения
        }

        private void OnMouseExit()
        {
            // Снять эффект наведения
        }

        private void OnMouseDown()
        {
            // Обработка клика через PointSelector
            // Вызывается автоматически, если на объекте есть Collider
        }
    }
}