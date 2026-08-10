using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RSMA.uDTP.Topics;

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
            string json = JsonUtility.ToJson(new MissionWrapper { points = mission }, true);
            string filePath = Path.Combine(_savePath, $"mission_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(filePath, json);
            Debug.Log($"Mission saved to: {filePath}");
        }

        public static List<TrajectoryPoint> LoadFromJson()
        {
            // Простой диалог - используем последний файл
            var files = Directory.GetFiles(_savePath, "*.json");
            if (files.Length == 0) return null;

            string filePath = files[files.Length - 1];
            string json = File.ReadAllText(filePath);
            var wrapper = JsonUtility.FromJson<MissionWrapper>(json);
            return wrapper?.points;
        }

        [System.Serializable]
        private class MissionWrapper
        {
            public List<TrajectoryPoint> points;
        }
    }
}