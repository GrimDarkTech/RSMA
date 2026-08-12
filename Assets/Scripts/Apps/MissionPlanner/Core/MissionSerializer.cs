using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RSMA.uDTP.Topics;
using SFB;

namespace RSMA.MissionPlanner.Core
{
    public static class MissionSerializer
    {
        private static string _savePath = Path.Combine(Application.persistentDataPath, "missions");

        static MissionSerializer()
        {
            if (!Directory.Exists(_savePath))
                Directory.CreateDirectory(_savePath);
        }

        public static void SaveToJson(List<TrajectoryPoint> mission)
        {
            SaveToJson(mission, null);
        }

        public static void SaveToJson(List<TrajectoryPoint> mission, string customFileName)
        {
            if (mission == null || mission.Count == 0)
            {
                Debug.LogWarning("Cannot save empty mission");
                return;
            }

            string fileName = customFileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"mission_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            }

            string filePath = Path.Combine(_savePath, fileName);

            string json = JsonUtility.ToJson(new MissionWrapper { points = mission }, true);
            File.WriteAllText(filePath, json);
            Debug.Log($"Mission saved to: {filePath}");
        }

        public static List<TrajectoryPoint> LoadFromJson()
        {
            var extensions = new[]
            {
            new ExtensionFilter("JSON Files", "json"),
            new ExtensionFilter("All Files", "*")
            };

            // Открываем диалог выбора файла (возвращает массив строк)
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Load Mission", _savePath, extensions, false);

            // Проверяем, выбрал ли пользователь файл
            if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                Debug.Log("[MissionSerializer] Загрузка отменена пользователем.");
                return null;
            }

            string filePath = paths[0];

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.Log("File selection cancelled");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var wrapper = JsonUtility.FromJson<MissionWrapper>(json);

                if (wrapper?.points == null || wrapper.points.Count == 0)
                {
                    Debug.LogWarning("Loaded file contains no mission data");
                    return null;
                }

                Debug.Log($"Loaded {wrapper.points.Count} points from: {Path.GetFileName(filePath)}");
                return wrapper.points;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load mission: {e.Message}");
                return null;
            }
        }

        public static void SaveMissionWithDialog()
        {
            var manager = MissionManager.Instance;
            if (manager == null)
            {
                Debug.LogError("MissionManager instance not found");
                return;
            }

            var mission = manager.GetMission();
            if (mission == null || mission.Count == 0)
            {
                Debug.Log(
                    "Cannot Save: Mission is empty. Add some points first"
                );
                return;
            }

            // Открываем диалог сохранения
            string defaultFileName = $"mission_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath = StandaloneFileBrowser.SaveFilePanel("Save Mission", 
                    _savePath, 
                    defaultFileName, 
                    "json");

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.Log("Save cancelled by user");
                return;
            }

            string fileName = Path.GetFileName(filePath);
            SaveToJson(mission, fileName);
        }

        public static void LoadMissionWithDialog()
        {
            var manager = MissionManager.Instance;
            if (manager == null)
            {
                Debug.LogError("MissionManager instance not found");
                return;
            }

            var mission = LoadFromJson();
            if (mission != null && mission.Count > 0)
            {
                manager.ClearAllPoints();
                foreach (var point in mission)
                {
                    manager.AddPoint(point.position, point.targetVelocity);
                }
                Debug.Log($"Loaded {mission.Count} points from JSON");
            }
            else
            {
                Debug.LogWarning("No mission data found to load");
            }
        }

        [System.Serializable]
        private class MissionWrapper
        {
            public List<TrajectoryPoint> points;
        }
    }
}