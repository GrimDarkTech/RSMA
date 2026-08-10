using RSMA.GUI;
using RSMA.MissionPlanner.Core;
using RSMA.NetMQ;
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
            rt.anchorMax = new Vector2(1, 0.25f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Clear Button
            var clearBtn = UIBuilder.CreateButton("ClearBtn", _panel.transform, "Clear All", _font, 18, () => MissionManager.Instance.ClearAllPoints());
            UIBuilder.PlaceInGrid(clearBtn.gameObject, 0, 0, 1, 1, 2, 4);

            // Upload Button
            var uploadBtn = UIBuilder.CreateButton("UploadBtn", _panel.transform, "Upload Mission", _font, 18, UploadMission);
            UIBuilder.PlaceInGrid(uploadBtn.gameObject, 0, 1, 1, 1, 2, 4);

            // Save Button (future)
            var saveBtn = UIBuilder.CreateButton("SaveBtn", _panel.transform, "Save JSON", _font, 18, SaveMission);
            UIBuilder.PlaceInGrid(saveBtn.gameObject, 0, 2, 1, 1, 2, 4);

            // Load Button (future)
            var loadBtn = UIBuilder.CreateButton("LoadBtn", _panel.transform, "Load JSON", _font, 18, LoadMission);
            UIBuilder.PlaceInGrid(loadBtn.gameObject, 0, 3, 1, 1, 2, 4);
        }

        public void OnPointsChanged()
        {
            // Можно обновить состояние кнопок
        }

        private void UploadMission()
        {
            var mission = MissionManager.Instance.GetMission();
            if (mission.Count == 0)
            {
                Debug.LogWarning("Mission is empty!");
                return;
            }
        }

        private void SaveMission()
        {
            var mission = MissionManager.Instance.GetMission();
            MissionSerializer.SaveToJson(mission);
        }

        private void LoadMission()
        {
            var mission = MissionSerializer.LoadFromJson();
            if (mission != null)
            {
                MissionManager.Instance.ClearAllPoints();
                foreach (var point in mission)
                {
                    MissionManager.Instance.AddPoint(point.position, point.targetVelocity);
                }
            }
        }
    }
}