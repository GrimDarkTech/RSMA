using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
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
        private PointList _pointList;

        protected override void Start()
        {
            base.Start();

            _mainPanel = UIBuilder.CreatePanel("MissionPanel", _transform).transform;
            RectTransform rt = _mainPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.4f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Создаем три секции
            CreateHeader();
            CreatePointList();
            CreatePropertiesPanel();
            CreateControls();

            SubscribeToEvents();
            Close();
        }

        private void CreateHeader()
        {
            var header = UIBuilder.CreateLabel("Header", _mainPanel, "Mission Planner", _font, 30);
            UIBuilder.PlaceInGrid(header.gameObject, 0, 0, 1, 1, 1, 1);
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.9f);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void CreatePointList()
        {
            _pointList = new PointList(_mainPanel, _font);
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
            manager.OnPointSelected += _propertiesPanel.OnPointSelected;
            manager.OnPointSelected += _pointList.OnPointSelected;
            manager.OnPointsChanged += _pointList.OnPointsChanged;
            manager.OnPointsChanged += _controls.OnPointsChanged;
        }

        public override void Close()
        {
            base.Close();
            _mainPanel.gameObject.SetActive(false);
        }

        public override void Open()
        {
            base.Open();
            _mainPanel.gameObject.SetActive(true);
        }
    }
}