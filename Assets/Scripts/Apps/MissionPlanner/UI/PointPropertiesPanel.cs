using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RSMA.MissionPlanner.UI
{
    public class PointPropertiesPanel
    {
        private Transform _parent;
        private Font _font;
        private GameObject _panel;

        private InputField _xInput, _yInput, _zInput, _velocityInput;
        private Text _pointIndexText;
        private Text _pointCountText;

        private int _currentIndex = -1;
        private bool _isUpdating = false;

        public PointPropertiesPanel(Transform parent, Font font)
        {
            _parent = parent;
            _font = font;
            CreatePanel();
        }

        private void CreatePanel()
        {
            _panel = UIBuilder.CreatePanel("PropertiesPanel", _parent);
            RectTransform rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.3f);
            rt.anchorMax = new Vector2(1, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Фон панели
            var bg = _panel.GetComponent<Image>();
            if (bg == null)
                bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // Заголовок с информацией о точке
            _pointIndexText = UIBuilder.CreateLabel("PointIndex", _panel.transform, "No point selected", _font, 18);
            var indexRt = _pointIndexText.GetComponent<RectTransform>();
            indexRt.anchorMin = new Vector2(0, 0.8f);
            indexRt.anchorMax = new Vector2(1, 1);
            indexRt.offsetMin = new Vector2(10, 0);
            indexRt.offsetMax = new Vector2(-10, 0);
            _pointIndexText.alignment = TextAnchor.MiddleLeft;

            // Информация о количестве точек
            _pointCountText = UIBuilder.CreateLabel("PointCount", _panel.transform, "Total: 0 points", _font, 14);
            var countRt = _pointCountText.GetComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.7f);
            countRt.anchorMax = new Vector2(1, 0.8f);
            countRt.offsetMin = new Vector2(10, 0);
            countRt.offsetMax = new Vector2(-10, 0);
            _pointCountText.alignment = TextAnchor.MiddleLeft;
            _pointCountText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            // Создаем поля ввода в сетке
            CreateInputFields();

            // Кнопки управления
            CreateControlButtons();
        }

        private void CreateInputFields()
        {
            float startY = 0.65f;
            float fieldHeight = 0.12f;
            float spacing = 0.02f;

            // X
            CreateLabelAndInput("X:", ref _xInput, 0, startY, fieldHeight, spacing, "0");
            _xInput.onEndEdit.AddListener(OnXChanged);

            // Y
            CreateLabelAndInput("Y:", ref _yInput, 1, startY - (fieldHeight + spacing) * 1, fieldHeight, spacing, "0");
            _yInput.onEndEdit.AddListener(OnYChanged);

            // Z
            CreateLabelAndInput("Z:", ref _zInput, 2, startY - (fieldHeight + spacing) * 2, fieldHeight, spacing, "0");
            _zInput.onEndEdit.AddListener(OnZChanged);

            // Velocity
            CreateLabelAndInput("Speed:", ref _velocityInput, 3, startY - (fieldHeight + spacing) * 3, fieldHeight, spacing, "0");
            _velocityInput.onEndEdit.AddListener(OnVelocityChanged);
        }

        private void CreateLabelAndInput(string labelText, ref InputField inputField, int row, float yPos, float height, float spacing, string defaultValue)
        {
            // Создаем контейнер для строки
            var rowContainer = UIBuilder.CreatePanel($"Row_{row}", _panel.transform);
            var rowRt = rowContainer.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, yPos - height);
            rowRt.anchorMax = new Vector2(1, yPos);
            rowRt.offsetMin = new Vector2(10, 0);
            rowRt.offsetMax = new Vector2(-10, 0);

            // Создаем Label
            var label = UIBuilder.CreateLabel($"Label_{row}", rowContainer.transform, labelText, _font, 16);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.2f, 1);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleLeft;

            // Создаем InputField
            var inputObj = UIBuilder.CreateInputField($"Input_{row}", rowContainer.transform, _font, 16, defaultValue);
            var inputRt = inputObj.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.22f, 0.05f);
            inputRt.anchorMax = new Vector2(0.98f, 0.95f);
            inputRt.offsetMin = Vector2.zero;
            inputRt.offsetMax = Vector2.zero;

            inputField = inputObj.GetComponent<InputField>();

            // Устанавливаем тип ввода
            inputField.contentType = InputField.ContentType.DecimalNumber;
            inputField.characterValidation = InputField.CharacterValidation.Decimal;
        }

        private void CreateControlButtons()
        {
            var buttonContainer = UIBuilder.CreatePanel("ButtonContainer", _panel.transform);
            var btnRt = buttonContainer.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0, 0);
            btnRt.anchorMax = new Vector2(1, 0.2f);
            btnRt.offsetMin = new Vector2(10, 5);
            btnRt.offsetMax = new Vector2(-10, -5);
        }

        public void OnPointSelected(int index)
        {
            _isUpdating = true;

            _currentIndex = index;
            var manager = MissionManager.Instance;

            if (manager == null || index < 0 || index >= manager.Points.Count)
            {
                _panel.SetActive(false);
                _isUpdating = false;
                return;
            }

            _panel.SetActive(true);
            var point = manager.Points[index];

            _pointIndexText.text = $"Point #{index + 1}";
            _pointCountText.text = $"Total: {manager.Points.Count} points";

            _xInput.text = point.position.x.ToString("F2");
            _yInput.text = point.position.y.ToString("F2");
            _zInput.text = point.position.z.ToString("F2");
            _velocityInput.text = point.targetVelocity.ToString("F2");

            _isUpdating = false;
        }

        private void OnXChanged(string value)
        {
            if (_isUpdating || _currentIndex < 0) return;
            UpdatePointPosition(value, _yInput.text, _zInput.text);
        }

        private void OnYChanged(string value)
        {
            if (_isUpdating || _currentIndex < 0) return;
            UpdatePointPosition(_xInput.text, value, _zInput.text);
        }

        private void OnZChanged(string value)
        {
            if (_isUpdating || _currentIndex < 0) return;
            UpdatePointPosition(_xInput.text, _yInput.text, value);
        }

        private void OnVelocityChanged(string value)
        {
            if (_isUpdating || _currentIndex < 0) return;
            if (float.TryParse(value, out float vel))
            {
                MissionManager.Instance?.UpdatePoint(_currentIndex, velocity: vel);
            }
        }

        private void UpdatePointPosition(string x, string y, string z)
        {
            if (_currentIndex < 0) return;

            if (float.TryParse(x, out float fx) &&
                float.TryParse(y, out float fy) &&
                float.TryParse(z, out float fz))
            {
                MissionManager.Instance?.UpdatePoint(_currentIndex, position: new Vector3(fx, fy, fz));
            }
        }

        public void SetActive(bool active)
        {
            _panel.SetActive(active);
        }
    }
}