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

        private int _currentIndex = -1;

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
            rt.anchorMin = new Vector2(0, 0.25f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Заголовок
            _pointIndexText = UIBuilder.CreateLabel("PointIndex", _panel.transform, "Point # -", _font, 18);
            UIBuilder.PlaceInGrid(_pointIndexText.gameObject, 0, 0, 1, 3, 4, 3);

            // X
            var xLabel = UIBuilder.CreateLabel("XLabel", _panel.transform, "X:", _font, 16);
            UIBuilder.PlaceInGrid(xLabel.gameObject, 1, 0, 1, 1, 4, 3);
            _xInput = UIBuilder.CreateInputField("XInput", _panel.transform, _font, 16, "0");
            UIBuilder.PlaceInGrid(_xInput.gameObject, 1, 1, 1, 2, 4, 3);
            _xInput.onEndEdit.AddListener(OnXChanged);

            // Y
            var yLabel = UIBuilder.CreateLabel("YLabel", _panel.transform, "Y:", _font, 16);
            UIBuilder.PlaceInGrid(yLabel.gameObject, 2, 0, 1, 1, 4, 3);
            _yInput = UIBuilder.CreateInputField("YInput", _panel.transform, _font, 16, "0");
            UIBuilder.PlaceInGrid(_yInput.gameObject, 2, 1, 1, 2, 4, 3);
            _yInput.onEndEdit.AddListener(OnYChanged);

            // Z
            var zLabel = UIBuilder.CreateLabel("ZLabel", _panel.transform, "Z:", _font, 16);
            UIBuilder.PlaceInGrid(zLabel.gameObject, 3, 0, 1, 1, 4, 3);
            _zInput = UIBuilder.CreateInputField("ZInput", _panel.transform, _font, 16, "0");
            UIBuilder.PlaceInGrid(_zInput.gameObject, 3, 1, 1, 2, 4, 3);
            _zInput.onEndEdit.AddListener(OnZChanged);

            // Velocity
            var velLabel = UIBuilder.CreateLabel("VelLabel", _panel.transform, "Speed:", _font, 16);
            UIBuilder.PlaceInGrid(velLabel.gameObject, 0, 3, 1, 1, 4, 4);
            _velocityInput = UIBuilder.CreateInputField("VelInput", _panel.transform, _font, 16, "0");
            UIBuilder.PlaceInGrid(_velocityInput.gameObject, 0, 4, 1, 1, 4, 4);
            _velocityInput.onEndEdit.AddListener(OnVelocityChanged);

            // Кнопка удаления
            var deleteBtn = UIBuilder.CreateButton("DeleteBtn", _panel.transform, "Delete Point", _font, 16, DeleteCurrentPoint);
            UIBuilder.PlaceInGrid(deleteBtn.gameObject, 1, 3, 3, 2, 4, 4);
        }

        public void OnPointSelected(int index)
        {
            _currentIndex = index;
            if (index < 0 || index >= MissionManager.Instance.Points.Count)
            {
                _panel.SetActive(false);
                return;
            }

            _panel.SetActive(true);
            var point = MissionManager.Instance.Points[index];
            _pointIndexText.text = $"Point #{index + 1}";
            _xInput.text = point.position.x.ToString("F2");
            _yInput.text = point.position.y.ToString("F2");
            _zInput.text = point.position.z.ToString("F2");
            _velocityInput.text = point.targetVelocity.ToString("F2");
        }

        private void OnXChanged(string value)
        {
            UpdatePointPosition(value, _yInput.text, _zInput.text);
        }

        private void OnYChanged(string value)
        {
            UpdatePointPosition(_xInput.text, value, _zInput.text);
        }

        private void OnZChanged(string value)
        {
            UpdatePointPosition(_xInput.text, _yInput.text, value);
        }

        private void OnVelocityChanged(string value)
        {
            if (_currentIndex < 0) return;
            if (float.TryParse(value, out float vel))
            {
                MissionManager.Instance.UpdatePoint(_currentIndex, velocity: vel);
            }
        }

        private void UpdatePointPosition(string x, string y, string z)
        {
            if (_currentIndex < 0) return;
            if (float.TryParse(x, out float fx) &&
                float.TryParse(y, out float fy) &&
                float.TryParse(z, out float fz))
            {
                MissionManager.Instance.UpdatePoint(_currentIndex, position: new Vector3(fx, fy, fz));
            }
        }

        private void DeleteCurrentPoint()
        {
            if (_currentIndex >= 0)
                MissionManager.Instance.DeletePoint(_currentIndex);
        }

        public void SetActive(bool active)
        {
            _panel.SetActive(active);
        }
    }
}