using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RSMA.MissionPlanner.UI
{
    public class MissionControls
    {
        private Transform _parent;
        private Font _font;
        private GameObject _panel;

        public MissionControls(Transform parent, Font font)
        {
            _parent = parent;
            _font = font;
            CreatePanel();
        }

        private void CreatePanel()
        {
            _panel = UIBuilder.CreatePanel("ControlsPanel", _parent);
            RectTransform rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0.3f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Фон панели
            var bg = _panel.GetComponent<Image>();
            if (bg == null)
                bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var clearBtn = UIBuilder.CreateButton("ClearBtn", _panel.transform, "Clear All", _font, 16,
                () => MissionManager.Instance?.ClearAllPoints());
            UIBuilder.PlaceInGrid(clearBtn.gameObject, 0, 0, 1, 1, 2, 4);

            var saveBtn = UIBuilder.CreateButton("SaveBtn", _panel.transform, "Save JSON", _font, 16,
                SaveMission);
            UIBuilder.PlaceInGrid(saveBtn.gameObject, 0, 2, 1, 1, 2, 4);

            var loadBtn = UIBuilder.CreateButton("LoadBtn", _panel.transform, "Load JSON", _font, 16,
                LoadMission);
            UIBuilder.PlaceInGrid(loadBtn.gameObject, 0, 3, 1, 1, 2, 4);
        }

        private void SaveMission()
        {
            MissionSerializer.SaveMissionWithDialog();
        }
        private void LoadMission()
        {
            MissionSerializer.LoadMissionWithDialog();
        }
    }
}