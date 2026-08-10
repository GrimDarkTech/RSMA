using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RSMA.MissionPlanner.UI
{
    public class MissionWindow : Window
    {
        [Header("UI References")]
        [SerializeField] private Font _font;
        [SerializeField] private GameObject _contentPanel;

        private Transform _mainPanel;
        private PointPropertiesPanel _propertiesPanel;
        private MissionControls _controls;

        protected override void Start()
        {
            base.Start();

            _mainPanel = UIBuilder.CreatePanel("MissionPanel", _transform).transform;
            RectTransform rt = _mainPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Создаем секции
            CreateHeader();
            CreatePropertiesPanel();
            CreateControls();

            SubscribeToEvents();
            Close();
        }

        private void CreateHeader()
        {
            var header = UIBuilder.CreateLabel("Header", _mainPanel, "Mission Planner", _font, 30);
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.9f);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void CreatePropertiesPanel()
        {
            _propertiesPanel = new PointPropertiesPanel(_mainPanel, _font);
        }

        private void CreateControls()
        {
            _controls = new MissionControls(_mainPanel, _font);
        }

        private void SubscribeToEvents()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                manager.OnPointSelected += _propertiesPanel.OnPointSelected;
                // Убираем подписку на PointList
            }
        }

        public override void Close()
        {
            base.Close();
            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(false);
        }

        public override void Open()
        {
            base.Open();
            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            var manager = MissionManager.Instance;
            if (manager != null)
            {
                manager.OnPointSelected -= _propertiesPanel.OnPointSelected;
            }
        }
    }
}