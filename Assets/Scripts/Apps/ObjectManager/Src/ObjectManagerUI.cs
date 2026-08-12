using RSMA.GUI;
using UnityEngine;

namespace RSMA.ObjectManager.UI
{
    public class ObjectManagerUI : Window
    {
        [Header("UI References")]
        [SerializeField] private Font _font;

        private Transform _mainPanel;
        private ObjectTrackingControls _trackingControls;

        protected override void Start()
        {
            base.Start();

            // Создаем основную панель окна
            _mainPanel = UIBuilder.CreatePanel("ObjectManagerPanel", _transform).transform;
            RectTransform rt = _mainPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            CreateHeader();
            CreateTrackingControls();

            Close();
        }

        private void CreateHeader()
        {
            var header = UIBuilder.CreateLabel("Header", _mainPanel, "Object Manager", _font, 24);
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.92f);
            rt.anchorMax = new Vector2(1, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void CreateTrackingControls()
        {
            _trackingControls = new ObjectTrackingControls(_mainPanel, _font);
        }

        public override void Open()
        {
            base.Open();
            if (_mainPanel != null)
            {
                _mainPanel.gameObject.SetActive(true);
                _trackingControls?.RefreshObjectList();
            }
        }

        public override void Close()
        {
            base.Close();
            if (_mainPanel != null)
                _mainPanel.gameObject.SetActive(false);
        }
    }
}